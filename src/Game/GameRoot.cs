using Godot;
using Sentinel.Config;
using Sentinel.Sim;
using Sentinel.Render;
using Sentinel.UI;

namespace Sentinel.Game;

/// <summary>
/// Top-level mission node. Owns the config, the sim, the clock, the renderer and
/// the HUD, and translates raw input into queued <see cref="SimCommand"/>s.
/// </summary>
public sealed partial class GameRoot : Node2D
{
    [Export] public string MissionPath = "res://data/missions/mission_01.json";
    [Export] public string[] EquippedAbilities = { "kinetic_barrage", "aegis_barrier", "overdrive" };

    // world-to-screen transform for the play field, fitted to the viewport
    public float WorldScale { get; private set; } = 0.44f;
    public Vector2 WorldOrigin { get; private set; } = new(270, 360);

    // screen-space reserved strips (top status, bottom control panel)
    public const float TopReserve = 96f;
    public const float BottomReserve = 250f;

    private ConfigDb _cfg = null!;
    private SimWorld _world = null!;
    private readonly SimClock _clock = new();

    private SimRenderer _renderer = null!;
    private Hud _hud = null!;

    // input state
    private bool _dragging;
    private Vector2 _pressPos;
    private double _pressTime;
    private int _pendingReticleSlot = -1; // ability slot awaiting a target tap

    public SimWorld World => _world;
    public SimClock Clock => _clock;

    public override void _Ready()
    {
        _cfg = ConfigDb.Load();
        _world = new SimWorld(_cfg);
        _world.Load(_cfg.LoadMission(MissionPath), EquippedAbilities);

        _renderer = new SimRenderer { Root = this, World = _world };
        AddChild(_renderer);

        _hud = new Hud { Root = this };
        AddChild(_hud);

        FitViewport();
        GetViewport().SizeChanged += FitViewport;

        _clock.SetSpeed(1);
    }

    private void FitViewport()
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float availH = Mathf.Max(200f, vp.Y - TopReserve - BottomReserve);
        // fit ~90% of the spawn ring into the smaller available axis
        float fitRadius = _world.B.SpawnRadius * 0.92f;
        float sx = (vp.X * 0.98f) / (2f * fitRadius);
        float sy = availH / (2f * fitRadius);
        WorldScale = Mathf.Clamp(Mathf.Min(sx, sy), 0.12f, 1.2f);
        WorldOrigin = new Vector2(vp.X * 0.5f, TopReserve + availH * 0.5f);

        _renderer.Scale = new Vector2(WorldScale, WorldScale);
        _renderer.Position = WorldOrigin;
    }

    public override void _Process(double delta)
    {
        // speed hotkeys (desktop) — HUD buttons call SetSpeed too
        if (Input.IsActionJustPressed("speed_1")) _clock.SetSpeed(1);
        if (Input.IsActionJustPressed("speed_2")) _clock.SetSpeed(2);
        if (Input.IsActionJustPressed("speed_3")) _clock.SetSpeed(3);
        if (Input.IsActionJustPressed("speed_4")) _clock.SetSpeed(4);

        _clock.Advance((float)delta, _world.StepTick);

        ConsumeSimEvents();
        _renderer.QueueRedraw();
        _hud.Refresh();
    }

    private void ConsumeSimEvents()
    {
        foreach (var ev in _world.Events.Events)
        {
            switch (ev.Kind)
            {
                case SimEventKind.MissileImpact:
                case SimEventKind.EnemyKilled:
                    _renderer.AddBoom(ev.Pos, Mathf.Max(6f, ev.A));
                    break;
                case SimEventKind.VolleyLaunched:
                    Input.VibrateHandheld(20);
                    break;
                case SimEventKind.PlanetHit:
                    _renderer.AddShake(Mathf.Min(6f, ev.A * 0.05f));
                    break;
                case SimEventKind.WaveCleared:
                case SimEventKind.MissionWon:
                case SimEventKind.MissionLost:
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
            if (_dragging && _world.Phase == SimPhase.Wave)
                _world.Enqueue(SimCommand.HeroTarget(ScreenToWorld(drag.Position)));
        }
    }

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
            else
            {
                _world.Enqueue(SimCommand.Volley(w));
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
    public void SetSpeed(int s) => _clock.SetSpeed(s);

    public void RequestBuild(int slot, string turretId)
        => _world.Enqueue(SimCommand.Build(slot, turretId));

    public void RequestSell(int slot) => _world.Enqueue(SimCommand.Sell(slot));

    public void RequestLaunchWave() => _world.Enqueue(SimCommand.Wave());

    public void RequestAbility(int slot)
    {
        var abil = _world.AbilityView;
        if (slot < 0 || slot >= abil.Length || abil[slot].DefIndex < 0) return;
        var def = _world.AbilityDefs[abil[slot].DefIndex];
        if (def.Cast == "instant")
            _world.Enqueue(SimCommand.Cast(slot, Vector2.Zero));
        else
        {
            _pendingReticleSlot = slot;
            _hud.ShowReticlePrompt(def.Name);
        }
    }

    public void RestartMission()
    {
        _world.Load(_cfg.LoadMission(MissionPath), EquippedAbilities);
        _clock.Reset();
        _pendingReticleSlot = -1;
    }
}
