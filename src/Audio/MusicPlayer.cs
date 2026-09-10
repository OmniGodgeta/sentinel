using System.Collections.Generic;
using Godot;

namespace Sentinel.Audio;

/// <summary>
/// Crossfading music. The menu plays one looping theme (assets/music/menu/); each
/// battle plays one looping track chosen per stage from assets/music/game/
/// (eve_01.ogg … eve_13.ogg), picked by <see cref="PlayStage"/>. No files = silent.
/// Autoloaded alongside AudioManager.
/// </summary>
public sealed partial class MusicPlayer : Node
{
    public static MusicPlayer Instance { get; private set; } = null!;

    private AudioStreamPlayer _a = null!, _b = null!;
    private AudioStreamPlayer _cur = null!;
    private readonly List<(string key, AudioStream stream)> _menu = new();
    private readonly List<(string key, AudioStream stream)> _game = new();
    private string _nowPlaying = "";
    private float _fade;               // 0..1 toward _cur
    private float _volume = 0.6f;
    private bool _muted;
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        _a = New(); _b = New();
        _cur = _a;
        _rng.Randomize();

        LoadDir("res://assets/music/menu/", _menu);
        LoadDir("res://assets/music/game/", _game);
        _game.Sort((x, y) => string.CompareOrdinal(x.key, y.key));
    }

    private AudioStreamPlayer New()
    {
        var p = new AudioStreamPlayer { Bus = "Master", VolumeDb = -80f };
        AddChild(p);
        return p;
    }

    private static void LoadDir(string dir, List<(string, AudioStream)> into)
    {
        if (!DirAccess.DirExistsAbsolute(dir)) return;
        using var d = DirAccess.Open(dir);
        if (d == null) return;
        foreach (var f in d.GetFiles())
        {
            string n = f.EndsWith(".import") ? f[..^7] : f;
            if (!(n.EndsWith(".ogg") || n.EndsWith(".mp3") || n.EndsWith(".wav"))) continue;
            var s = GD.Load<AudioStream>(dir + n);
            if (s == null) continue;
            // in-game / menu themes loop; a stage keeps one theme for the whole hold
            switch (s)
            {
                case AudioStreamOggVorbis ogg: ogg.Loop = true; break;
                case AudioStreamMP3 mp3: mp3.Loop = true; break;
                case AudioStreamWav wav: wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward; break;
            }
            string key = n[..n.LastIndexOf('.')];
            if (!into.Exists(x => x.Item1 == key)) into.Add((key, s));
        }
    }

    public void SetVolume(float v01, bool muted)
    {
        _volume = Mathf.Clamp(v01, 0f, 1f);
        _muted = muted;
    }

    public void PlayMenu()
    {
        if (_menu.Count == 0) { StopAll(); return; }
        CrossfadeTo("menu:" + _menu[0].key, _menu[0].stream);
    }

    /// <summary>Play the battle track for a stage. <paramref name="stageKey"/> is a
    /// mission's `music` field ("eve_05") or empty for a deterministic pick from
    /// <paramref name="seed"/>.</summary>
    public void PlayStage(string stageKey, ulong seed = 0)
    {
        if (_game.Count == 0) { StopAll(); return; }
        int i = -1;
        if (!string.IsNullOrEmpty(stageKey))
            i = _game.FindIndex(x => x.key == stageKey || x.key.EndsWith(stageKey));
        if (i < 0) i = (int)(seed % (ulong)_game.Count);
        CrossfadeTo("stage:" + _game[i].key, _game[i].stream);
    }

    /// <summary>Legacy: shuffle the battle playlist (used if a stage has no track).</summary>
    public void PlayGame() => PlayStage("", _rng.Randi());

    private void CrossfadeTo(string tag, AudioStream stream)
    {
        if (_nowPlaying == tag && _cur.Playing) return;
        _nowPlaying = tag;
        var next = _cur == _a ? _b : _a;
        next.Stream = stream;
        next.VolumeDb = -80f;
        next.Play();
        _cur = next;
        _fade = 0f;
    }

    private void StopAll()
    {
        _nowPlaying = "";
        _a.Stop(); _b.Stop();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _fade = Mathf.MoveToward(_fade, 1f, dt * 0.7f);
        float target = _muted ? -80f : Mathf.LinearToDb(Mathf.Max(0.0001f, _volume));
        var other = _cur == _a ? _b : _a;
        _cur.VolumeDb = Mathf.Lerp(-80f, target, _fade);
        other.VolumeDb = Mathf.Lerp(target, -80f, _fade);
        if (_fade >= 1f && other.Playing) other.Stop();
    }
}
