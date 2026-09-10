using Godot;
using Sentinel.Config;
using Sentinel.Sim;
using Sentinel.Render;
using Sentinel.UI;

namespace Sentinel.Game;

public readonly record struct MissionOutcome(
    string MissionId, bool Won, int WavesCleared, int WaveCount,
    double ResearchData, double Xp, int Cores, float PlanetIntegrityPct, bool HeroSurvived);

/// <summary>
/// One mission: owns the config view, the sim, the clock, the renderer and the
/// HUD, and translates raw input into queued <see cref="SimCommand"/>s.
/// Instanced by <see cref="AppRoot"/>; reports back through <see cref="MissionEnded"/>.
/// </summary>
public sealed partial class GameRoot : Node2D
{
    [Export] public string MissionPath = "res://data/missions/m01.json";
    [Export] public string[] EquippedAbilities = System.Array.Empty<string>();
    public float[] AbilityEffect = System.Array.Empty<float>();
    public float[] AbilityCd = System.Array.Empty<float>();
    public Meta.ModifierSet Mods = new();
    public Config.AscensionTierDef? Ascension;

    public int StartSpeed = 1;
    /// <summary>When set, the sim loads this instead of reading <see cref="MissionPath"/> (weekly challenge).</summary>
    public Config.MissionDef? MissionOverride;
    public event System.Action<MissionOutcome>? MissionEnded;
    public event System.Action? ExitToMenu;
    private bool _outcomeReported;

    // world-to-screen transform for the play field, fitted to the viewport
    public float WorldScale { get; private set; } = 0.44f;
    public Vector2 WorldOrigin { get; private set; } = new(270, 360);

    // screen-space reserved strips (top status, bottom control panel).
    // TopReserve grows by the display's safe-area inset (notch / punch-hole) so the
    // status + speed row never sits under the camera cutout.
    public float TopReserve { get; private set; } = 172f;
    public const float BottomReserve = 306f;
    public float SafeTopInset { get; private set; }

    /// <summary>True while the sim is frozen only because a card draft is waiting —
    /// distinct from a user-requested pause.</summary>
    public bool DraftPause { get; private set; }

    private ConfigDb _cfg = null!;
    private SimWorld _world = null!;
    private readonly SimClock _clock = new();

    private SimRenderer _renderer = null!;
    private Render.Starfield _starfield = null!;
    private Render.PlanetView _planet = null!;
    private ScreenFx _fx = null!;
    private Hud _hud = null!;
    public ScreenFx Fx => _fx;

    // input state
    private bool _dragging;
    private Vector2 _pressPos;
    private double _pressTime;
    private int _pendingReticleSlot = -1; // ability slot awaiting a target tap

    public SimWorld World => _world;
    public SimClock Clock => _clock;

    public override void _Ready()
    {
        _cfg = AppRoot.Instance?.Cfg ?? ConfigDb.Load();
        _world = new SimWorld(_cfg);
        _world.Load(MissionOverride ?? _cfg.LoadMission(MissionPath), EquippedAbilities, Mods, AbilityEffect, AbilityCd, Ascension);

        AddBackdrop(_world.Mission.Backdrop);
        Sentinel.Audio.MusicPlayer.Instance?.PlayStage(_world.Mission.Music, _world.Mission.Seed);

        _starfield = new Render.Starfield { Radius = _world.B.DespawnRadius };
        AddChild(_starfield);
        _planet = new Render.PlanetView
        {
            Diameter = _world.B.PlanetRadius * 2f,
            Skin = AppRoot.Instance?.Save.Options.PlanetSkin ?? "earth",
        };
        AddChild(_planet);

        _renderer = new SimRenderer { Root = this, World = _world };
        AddChild(_renderer);

        _fx = new ScreenFx();
        AddChild(_fx);

        _hud = new Hud { Root = this };
        AddChild(_hud);

        FitViewport();
        GetViewport().SizeChanged += FitViewport;

        _clock.SetSpeed(StartSpeed);
        _hud.SyncSpeed(StartSpeed);
    }

    /// <summary>Space photo behind the play field (dimmed hard so gameplay stays readable).</summary>
    private void AddBackdrop(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        string path = $"res://assets/game/bg/{key}.jpg";
        if (!ResourceLoader.Exists(path)) return;

        var layer = new CanvasLayer { Layer = -5 };
        var tr = new TextureRect
        {
            Texture = GD.Load<Texture2D>(path),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Modulate = new Color(1f, 1f, 1f, 0.55f),
        };
        tr.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(tr);
        var scrim = new ColorRect { Color = new Color(0.01f, 0.012f, 0.03f, 0.42f) };
        scrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(scrim);
        AddChild(layer);
    }

    private void FitViewport()
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;

        // safe-area inset (Android notch / status bar) mapped into viewport units
        var safe = DisplayServer.GetDisplaySafeArea();
        var win = DisplayServer.WindowGetSize();
        float vScale = win.Y > 0 ? vp.Y / win.Y : 1f;
        SafeTopInset = Mathf.Clamp(safe.Position.Y * vScale, 0f, 240f);
        TopReserve = 172f + SafeTopInset;
        // the game is a portrait column; on a wide window it's centred and letterboxed
        float designW = Mathf.Min(vp.X, vp.Y * 0.62f);
        float availH = Mathf.Max(200f, vp.Y - TopReserve - BottomReserve);
        float fitRadius = _world.B.SpawnRadius * 0.92f;
        float sx = (designW * 0.98f) / (2f * fitRadius);
        float sy = availH / (2f * fitRadius);
        WorldScale = Mathf.Clamp(Mathf.Min(sx, sy), 0.12f, 1.4f);
        WorldOrigin = new Vector2(vp.X * 0.5f, TopReserve + availH * 0.5f);

        var sv = new Vector2(WorldScale, WorldScale);
        _renderer.Scale = sv; _renderer.Position = WorldOrigin;
        if (_starfield != null) { _starfield.Scale = sv; _starfield.Position = WorldOrigin; }
        if (_planet != null) { _planet.Scale = sv; _planet.Position = WorldOrigin; }
        _hud?.SetDesignWidth(designW, vp);
    }

    public override void _Process(double delta)
    {
        // speed hotkeys (desktop) — HUD buttons call SetSpeed too
        if (Input.IsActionJustPressed("speed_1")) { SetSpeed(1); _hud.SyncSpeed(1); }
        if (Input.IsActionJustPressed("speed_2")) { SetSpeed(2); _hud.SyncSpeed(2); }
        if (Input.IsActionJustPressed("speed_3")) { SetSpeed(3); _hud.SyncSpeed(3); }
        if (Input.IsActionJustPressed("speed_4")) { SetSpeed(4); _hud.SyncSpeed(4); }
        if (Input.IsActionJustPressed("ui_cancel")) TogglePause();

        // freeze the sim while a card draft is waiting; resume the instant it's picked
        bool wantDraft = _world.HasPendingDraft && _world.Phase == SimPhase.Wave;
        if (wantDraft && !DraftPause) { DraftPause = true; _clock.Paused = true; }
        else if (!wantDraft && DraftPause) { DraftPause = false; _clock.Paused = false; }

        _clock.Advance((float)delta, _world.StepTick);

        // auto-cancel an armed aimed-ability after a few seconds of no target tap
        if (_pendingReticleSlot >= 0)
        {
            _armTime += delta;
            if (_armTime > 4.0 || _world.Phase != SimPhase.Wave)
            {
                _pendingReticleSlot = -1;
                _hud.ClearReticlePrompt();
            }
        }

        ConsumeSimEvents();
        _renderer.QueueRedraw();
        _hud.Refresh();

        if (!_outcomeReported && _world.Phase is SimPhase.Won or SimPhase.Lost)
        {
            _outcomeReported = true;
            _clock.Paused = true;
            // for survival, report seconds-survived / hold-length instead of wave counts
            int progress = _world.IsSurvival ? Mathf.FloorToInt(_world.PhaseTimer) : _world.WavesCleared;
            int total = _world.IsSurvival ? Mathf.FloorToInt(_world.Mission.Duration) : _world.WaveCount;
            MissionEnded?.Invoke(new MissionOutcome(
                _world.Mission.Id,
                _world.Phase == SimPhase.Won,
                progress, total,
                _world.ResearchDataEarned, _world.XpEarned, _world.CoresEarned,
                _world.PlanetIntegrityMax > 0 ? _world.PlanetIntegrity / _world.PlanetIntegrityMax : 0f,
                _world.HeroView.Alive));
        }
    }

    public void TogglePause() => _clock.Paused = !_clock.Paused;
    public bool IsPaused => _clock.Paused;
    public void GoToMenu() => ExitToMenu?.Invoke();

    private void ConsumeSimEvents()
    {
        var sfx = Sentinel.Audio.AudioManager.Instance;
        foreach (var ev in _world.Events.Events)
        {
            _renderer.OnSimEvent(ev);
            switch (ev.Kind)
            {
                case SimEventKind.TurretFired:
                    if (ev.I == -5) sfx?.Play("sentinel_shot", -14f, 0.12f, 0.05);
                    else sfx?.Play(GD.Randf() < 0.5f ? "turret_shot" : "turret_shot_b", -13f, 0.14f, 0.04);
                    break;
                case SimEventKind.BeamTick:
                    if ((_world.Tick % 12) == 0) sfx?.Play("shield", -18f, 0.1f, 0.1);
                    break;
                case SimEventKind.ChainArc:
                    sfx?.Play("sentinel_shot", -12f, 0.2f, 0.06);
                    break;
                case SimEventKind.EnemyKilled:
                    sfx?.Play(GD.Randf() < 0.5f ? "explosion" : "explosion_b", -11f, 0.16f, 0.03);
                    break;
                case SimEventKind.MissileImpact:
                    sfx?.Play("explosion_big", -8f, 0.1f);
                    break;
                case SimEventKind.EnemyHit:
                    if (ev.A > 4f) sfx?.Play("hit_light", -20f, 0.2f, 0.05);
                    break;
                case SimEventKind.VolleyLaunched:
                    sfx?.Play(ev.Pos == Vector2.Zero ? "battery_launch" : "missile_launch", -7f, 0.08f);
                    Input.VibrateHandheld(25);
                    break;
                case SimEventKind.AbilityCast:
                    if (ev.I >= 0) sfx?.Play("ability_cast", -4f, 0.05f);
                    else if (ev.I == -3) sfx?.Play("card_pick", -3f);
                    break;
                case SimEventKind.PlanetHit:
                    if (ev.A > 8f) sfx?.Play("planet_hit", -9f, 0.1f, 0.08);
                    break;
                case SimEventKind.HeroWeaponFired:
                    switch (ev.I)
                    {
                        case 0: sfx?.Play("turret_shot_b", -10f, 0.16f, 0.02); break;      // laser
                        case 2: sfx?.Play("sentinel_shot", -8f, 0.1f); break;              // ion
                        case 3: sfx?.Play("explosion_big", 0f, 0f); Input.VibrateHandheld(90); break; // yamato
                    }
                    break;
                case SimEventKind.HeroShieldPop:
                    sfx?.Play("shield", -6f, 0.05f);
                    break;
                case SimEventKind.NovaPulse:
                    sfx?.Play("explosion_big", -3f, 0.05f);
                    break;
                case SimEventKind.EnemySpawned:
                    sfx?.Play("shield", -26f, 0.2f, 0.15);
                    break;
                case SimEventKind.MissionLost:
                    Input.VibrateHandheld(120);
                    _fx.Flash(new Color(1f, 0.3f, 0.25f), 0.5f);
                    sfx?.Play("mission_lost", 0f, 0f);
                    _hud.FlashBanner(ev.Kind);
                    break;
                case SimEventKind.MissionWon:
                    _fx.Flash(new Color(0.5f, 1f, 0.7f), 0.4f);
                    sfx?.Play("mission_won", -2f, 0f);
                    _hud.FlashBanner(ev.Kind);
                    break;
                case SimEventKind.WaveCleared:
                    sfx?.Play("wave_clear", -8f, 0f);
                    _hud.FlashBanner(ev.Kind);
                    break;
            }
        }
        _world.Events.Clear();
    }

    // ---- coordinate helpers ----
    public Vector2 ScreenToWorld(Vector2 s) => (s - WorldOrigin) / WorldScale;
    public Vector2 WorldToScreen(Vector2 w) => w * WorldScale + WorldOrigin;

    // ---- input ----
    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventScreenTouch touch)
        {
            if (touch.Pressed)
            {
                _pressPos = touch.Position;
                _pressTime = Time.GetTicksMsec() / 1000.0;
                _dragging = false;
            }
            else
            {
                OnRelease(touch.Position);
            }
        }
        else if (e is InputEventScreenDrag drag)
        {
            if (_pressPos.DistanceTo(drag.Position) > 12f) _dragging = true;
            // dragging on the field (not the joystick) also flies the ship there
            if (_dragging && _world.Phase == SimPhase.Wave && !_hud.BuildOpen && _pendingReticleSlot < 0)
                _world.Enqueue(SimCommand.HeroTarget(ScreenToWorld(drag.Position)));
        }
    }

    /// <summary>HUD joystick → continuous hero movement.</summary>
    public void HeroJoystick(Vector2 dir) => _world.Enqueue(SimCommand.HeroMove(dir));

    private void OnRelease(Vector2 pos)
    {
        // A HUD control that handled the press will have consumed the event; here
        // we only see taps on the play field.
        if (_world.Phase == SimPhase.Wave)
        {
            if (_dragging)
            {
                _world.Enqueue(SimCommand.HeroTarget(ScreenToWorld(pos)));
                return;
            }
            Vector2 w = ScreenToWorld(pos);
            if (_pendingReticleSlot >= 0)
            {
                _world.Enqueue(SimCommand.Cast(_pendingReticleSlot, w));
                _pendingReticleSlot = -1;
                _hud.ClearReticlePrompt();
            }
            else if (_hud.BuildOpen)
            {
                // real-time base management (survival): a tap picks the nearest slot
                int slot = NearestSlot(w, 110f);
                if (slot >= 0) _hud.SelectSlot(slot);
            }
            else
            {
                // tap the field → the hero locks that enemy and opens fire
                _world.Enqueue(SimCommand.HeroFocus(w));
            }
        }
        else if (_world.Phase == SimPhase.Build)
        {
            // tap near a slot selects it (HUD shows the build buttons)
            int slot = NearestSlot(ScreenToWorld(pos), 90f);
            if (slot >= 0) _hud.SelectSlot(slot);
        }
    }

    private int NearestSlot(Vector2 w, float maxDist)
    {
        int best = -1;
        float bestD = maxDist * maxDist;
        var turrets = _world.TurretView;
        for (int i = 0; i < turrets.Length; i++)
        {
            float d = turrets[i].Pos.DistanceSquaredTo(w);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    // ---- HUD callbacks ----
    public void SetSpeed(int s) { _clock.SetSpeed(s); AppRoot.Instance?.RememberSpeed(s); }

    public void RequestBuild(int slot, string turretId)
        => _world.Enqueue(SimCommand.Build(slot, turretId));

    public void RequestSell(int slot) => _world.Enqueue(SimCommand.Sell(slot));
    public void RequestUpgrade(int slot) => _world.Enqueue(SimCommand.Upgrade(slot));
    public void RequestFork(int slot, int fork) => _world.Enqueue(SimCommand.Fork(slot, fork));
    public void RequestPickCard(int cardIndex)
    {
        _world.Enqueue(SimCommand.Card(cardIndex));
        // while a draft has the clock frozen the sim isn't ticking, so pump one tick
        // by hand to apply the pick and let _Process lift the pause next frame
        if (DraftPause) _world.StepTick();
    }

    public void RequestFireWeapon(int weaponIndex) => _world.Enqueue(SimCommand.FireHeroWeapon(weaponIndex));
    public void RequestToggleAutoFire() => _world.Enqueue(SimCommand.ToggleAutoFire());

    public void RequestLaunchWave()
    {
        _world.Enqueue(SimCommand.Wave());
        Sentinel.Audio.AudioManager.Instance?.Play("wave_start", -4f, 0f);
    }

    public int PendingReticleSlot => _pendingReticleSlot;

    public void RequestAbility(int slot)
    {
        var abil = _world.AbilityView;
        if (slot < 0 || slot >= abil.Length || abil[slot].DefIndex < 0) return;
        if (abil[slot].CooldownLeft > 0f) return;
        var def = _world.AbilityDefs[abil[slot].DefIndex];

        if (def.Cast == "instant")
        {
            _world.Enqueue(SimCommand.Cast(slot, Vector2.Zero));
            _pendingReticleSlot = -1;
            _hud.ClearReticlePrompt();
            return;
        }

        // aimed ability: first tap arms it, second tap (or tapping the play area)
        // fires it — at the nearest enemy cluster if fired from the button.
        if (_pendingReticleSlot == slot)
        {
            _world.Enqueue(SimCommand.Cast(slot, AutoTargetPoint()));
            _pendingReticleSlot = -1;
            _hud.ClearReticlePrompt();
        }
        else
        {
            _pendingReticleSlot = slot;
            _armTime = 0.0;
            _hud.ShowReticlePrompt(def.Name);
        }
    }

    private double _armTime;

    private Vector2 AutoTargetPoint()
    {
        var en = _world.EnemyView;
        Vector2 best = new(0, -200f);
        float bestD = float.MaxValue;
        for (int i = 0; i < en.Length; i++)
        {
            if (!en[i].Alive) continue;
            float d = en[i].Pos.LengthSquared();
            if (d < bestD) { bestD = d; best = en[i].Pos; }
        }
        return best;
    }

    public void RestartMission()
    {
        _world.Load(_cfg.LoadMission(MissionPath), EquippedAbilities, Mods, AbilityEffect, AbilityCd, Ascension);
        _clock.Reset();
        _clock.SetSpeed(StartSpeed);
        _hud.SyncSpeed(StartSpeed);
        _pendingReticleSlot = -1;
        _outcomeReported = false;
        DraftPause = false;
    }
}
