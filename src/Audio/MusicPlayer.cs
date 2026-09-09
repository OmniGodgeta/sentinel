using System.Collections.Generic;
using Godot;

namespace Sentinel.Audio;

/// <summary>
/// Playlist music with crossfade. Drop .ogg files into res://assets/music/menu/
/// and res://assets/music/game/ — they're picked up automatically, shuffled, and
/// looped. No files = silent (no error). Autoloaded alongside AudioManager.
/// </summary>
public sealed partial class MusicPlayer : Node
{
    public static MusicPlayer Instance { get; private set; } = null!;

    private AudioStreamPlayer _a = null!, _b = null!;
    private AudioStreamPlayer _cur = null!;
    private readonly List<AudioStream> _menu = new();
    private readonly List<AudioStream> _game = new();
    private List<AudioStream> _active = new();
    private int _idx;
    private string _mode = "";
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
    }

    private AudioStreamPlayer New()
    {
        var p = new AudioStreamPlayer { Bus = "Master", VolumeDb = -80f };
        p.Finished += OnTrackEnd;
        AddChild(p);
        return p;
    }

    private static void LoadDir(string dir, List<AudioStream> into)
    {
        if (!DirAccess.DirExistsAbsolute(dir)) return;
        using var d = DirAccess.Open(dir);
        if (d == null) return;
        foreach (var f in d.GetFiles())
        {
            string n = f.EndsWith(".import") ? f[..^7] : f;
            if (!(n.EndsWith(".ogg") || n.EndsWith(".mp3") || n.EndsWith(".wav"))) continue;
            var s = GD.Load<AudioStream>(dir + n);
            if (s != null && !into.Exists(x => x == s)) into.Add(s);
        }
    }

    public void SetVolume(float v01, bool muted)
    {
        _volume = Mathf.Clamp(v01, 0f, 1f);
        _muted = muted;
    }

    public void PlayMenu() => Switch("menu", _menu);
    public void PlayGame() => Switch("game", _game);

    private void Switch(string mode, List<AudioStream> list)
    {
        if (_mode == mode) return;
        _mode = mode;
        _active = list;
        if (_active.Count == 0) { _cur.Stop(); (_cur == _a ? _b : _a).Stop(); return; }
        Shuffle();
        _idx = 0;
        var next = _cur == _a ? _b : _a;
        next.Stream = _active[_idx];
        next.Play();
        _cur = next;
        _fade = 0f; // fade toward the new _cur
    }

    private void Shuffle()
    {
        for (int i = _active.Count - 1; i > 0; i--)
        {
            int j = (int)(_rng.Randi() % (uint)(i + 1));
            (_active[i], _active[j]) = (_active[j], _active[i]);
        }
    }

    private void OnTrackEnd()
    {
        if (_active.Count == 0) return;
        _idx = (_idx + 1) % _active.Count;
        if (_idx == 0) Shuffle();
        _cur.Stream = _active[_idx];
        _cur.Play();
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
