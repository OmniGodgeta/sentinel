using System.Collections.Generic;
using Godot;

namespace Sentinel.Audio;

/// <summary>
/// Autoloaded SFX player. Preloads the Kenney CC0 one-shots and plays them from a
/// small polyphony pool with a little random pitch. Volume comes from the save.
/// </summary>
public sealed partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; } = null!;

    private const int Voices = 14;
    private readonly AudioStreamPlayer[] _pool = new AudioStreamPlayer[Voices];
    private int _next;
    private readonly Dictionary<string, AudioStream> _streams = new();
    private double _lastPlay;
    private readonly Dictionary<string, double> _lastKey = new();

    public float SfxVolume = 0.9f;
    public bool Muted;

    private static readonly string[] Keys =
    {
        "turret_shot", "turret_shot_b", "sentinel_shot", "missile_launch", "battery_launch",
        "explosion", "explosion_b", "explosion_big", "hit", "hit_light", "planet_hit",
        "shield", "ability_cast", "wave_start", "wave_clear", "mission_lost", "mission_won",
        "ui_click", "ui_hover", "ui_confirm", "ui_back", "ui_error", "card_pick", "hero_thrust",
    };

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        for (int i = 0; i < Voices; i++)
        {
            var p = new AudioStreamPlayer { Bus = "Master" };
            AddChild(p);
            _pool[i] = p;
        }
        foreach (var k in Keys)
        {
            var s = GD.Load<AudioStream>($"res://assets/audio/{k}.ogg");
            if (s != null)
            {
                if (s is AudioStreamOggVorbis ogg) ogg.Loop = false;
                _streams[k] = s;
            }
        }
    }

    public void SetVolume(float v01, bool muted)
    {
        SfxVolume = Mathf.Clamp(v01, 0f, 1f);
        Muted = muted;
    }

    /// <summary>Play a one-shot. dbAdd trims individual sounds; minGap throttles spam.</summary>
    public void Play(string key, float dbAdd = 0f, float pitchVar = 0.08f, double minGap = 0.03)
    {
        if (Muted || SfxVolume <= 0.001f) return;
        if (!_streams.TryGetValue(key, out var stream)) return;

        double now = Time.GetTicksMsec() / 1000.0;
        if (_lastKey.TryGetValue(key, out double last) && now - last < minGap) return;
        _lastKey[key] = now;

        var p = _pool[_next];
        _next = (_next + 1) % Voices;
        p.Stream = stream;
        p.VolumeDb = Mathf.LinearToDb(SfxVolume) + dbAdd;
        p.PitchScale = 1f + (GD.Randf() * 2f - 1f) * pitchVar;
        p.Play();
    }

    public void Click() => Play("ui_click", -6f, 0.03f);
    public void Back() => Play("ui_back", -6f, 0.03f);
    public void Confirm() => Play("ui_confirm", -4f, 0.03f);
}
