using System.Collections.Generic;
using Godot;
using Sentinel.Sim;
using Sentinel.Game;

namespace Sentinel.Render;

/// <summary>
/// Play-field renderer. Kenney CC0 sprites for ships / missiles / meteors, a
/// shader Earth (drawn by PlanetView behind this), plus procedural weapon and
/// ability VFX — beams, tracers, lightning, shock rings, screen flashes.
/// </summary>
public sealed partial class SimRenderer : Node2D
{
    public GameRoot Root = null!;
    public SimWorld World = null!;
    public int SelectedSlot = -1;

    private enum FxKind : byte { Muzzle, Tracer, Spark, Boom, Shock, Lightning, Text, CastRing, Warp, Smoke }
    private struct Fx
    {
        public FxKind Kind;
        public Vector2 A, B;
        public float R, Age, Life;
        public Color Col;
        public string Text;
    }
    private readonly List<Fx> _fx = new(256);
    private readonly List<(Vector2 a, Vector2 b, float w, Color c)> _beams = new(32);

    private Vector2 _shakeOffset;
    private float _shake;
    private Vector2 _lastHeroPos;
    private float _puffTimer;

    private static readonly Dictionary<string, Color> EnemyTint = new()
    {
        ["skiff"] = new(1.00f, 0.55f, 0.52f),
        ["hauler"] = new(0.75f, 0.80f, 1.00f),
        ["interceptor"] = new(1.00f, 0.82f, 0.45f),
        ["aegis_cruiser"] = new(0.60f, 0.95f, 1.00f),
        ["bombard"] = new(1.00f, 0.60f, 0.45f),
        ["carrier"] = new(0.80f, 0.70f, 1.00f),
        ["phase_runner"] = new(0.90f, 0.60f, 1.00f),
        ["leech"] = new(0.80f, 1.00f, 0.65f),
        ["warden"] = new(0.65f, 1.00f, 0.78f),
        ["siege_crawler"] = new(1.00f, 0.72f, 0.55f),
    };
    private static Color Tint(string id) => EnemyTint.TryGetValue(id, out var c) ? c : new Color(1f, 0.6f, 0.6f);

    public void AddShake(float a) => _shake = Mathf.Min(16f, _shake + a);

    // --- Shop cosmetics (read live from the save; purely visual) ---
    private string HullId => Sentinel.Game.AppRoot.Instance?.Save.Options.HullSkin ?? "standard";
    private (Color muzzle, Color trail, Color bloom) Ord =>
        Art.Ordnance(Sentinel.Game.AppRoot.Instance?.Save.Options.OrdnancePalette ?? "ember");

    public void OnSimEvent(in SimEvent e)
    {
        switch (e.Kind)
        {
            case SimEventKind.TurretFired:
                Push(FxKind.Muzzle, e.Pos, e.Pos, 8f, 0.09f, new Color(1f, 0.95f, 0.7f));
                Push(FxKind.Tracer, e.Pos, e.PosB, 2f, 0.09f, new Color(1f, 0.9f, 0.6f, 0.9f));
                break;
            case SimEventKind.BeamTick:
                _beams.Add((e.Pos, e.PosB, 3f + e.A * 1.5f, new Color(1f, 0.25f, 0.35f)));
                if (GD.Randf() < 0.2f) Push(FxKind.Spark, e.PosB, e.PosB, 7f, 0.15f, new Color(1f, 0.4f, 0.4f));
                break;
            case SimEventKind.ChainArc:
                Push(FxKind.Lightning, e.Pos, e.PosB, 0f, 0.16f, new Color(0.6f, 0.85f, 1f));
                break;
            case SimEventKind.BarrageTick:
                Push(FxKind.Spark, e.Pos, e.Pos, 7f, 0.18f, new Color(1f, 0.85f, 0.4f));
                break;
            case SimEventKind.EnemyHit:
                if (e.A > 0f) Push(FxKind.Spark, e.Pos, e.Pos, 5f + Mathf.Min(e.A * 0.12f, 8f), 0.15f, new Color(1f, 0.85f, 0.5f));
                break;
            case SimEventKind.EnemyKilled:
                Push(FxKind.Boom, e.Pos, e.Pos, Mathf.Max(14f, e.A * 3f), 0.4f, new Color(1f, 0.7f, 0.35f));
                for (int s = 0; s < 5; s++)
                {
                    var d = Vector2.FromAngle(GD.Randf() * Mathf.Tau) * (e.A + 8f);
                    Push(FxKind.Tracer, e.Pos, e.Pos + d, 2f, 0.25f, new Color(1f, 0.6f, 0.3f, 0.8f));
                }
                break;
            case SimEventKind.MissileImpact:
                Push(FxKind.Boom, e.Pos, e.Pos, Mathf.Max(20f, e.A * 1.6f), 0.5f, Ord.bloom);
                AddShake(2.5f);
                break;
            case SimEventKind.VolleyLaunched:
                Push(FxKind.Muzzle, e.Pos, e.Pos, 14f, 0.14f, Ord.muzzle);
                Root.Fx?.Flash(Ord.muzzle, 0.05f);
                break;
            case SimEventKind.EnemySpawned:
                Push(FxKind.Warp, e.Pos, e.Pos, e.A + 10f, 0.35f, new Color(0.7f, 0.5f, 1f));
                break;
            case SimEventKind.PlanetHit:
                AddShake(Mathf.Min(9f, e.A * 0.08f));
                Root.Fx?.Flash(new Color(1f, 0.3f, 0.25f), Mathf.Min(0.18f, e.A * 0.002f));
                break;
            case SimEventKind.NovaPulse:
                Push(FxKind.Shock, Vector2.Zero, Vector2.Zero, e.A, 0.6f, new Color(0.7f, 0.9f, 1f));
                Push(FxKind.Shock, Vector2.Zero, Vector2.Zero, e.A * 0.7f, 0.5f, Colors.White);
                AddShake(10f);
                Root.Fx?.Flash(Colors.White, 0.35f);
                break;
            case SimEventKind.HeroDown:
                Push(FxKind.Boom, e.Pos, e.Pos, 34f, 0.6f, new Color(0.6f, 0.8f, 1f));
                AddShake(8f);
                break;
            case SimEventKind.AbilityCast:
                if (e.I >= 0)
                {
                    var w = World.AbilityView;
                    var col = new Color(0.6f, 0.9f, 1f);
                    string nm = "";
                    if (e.I < w.Length && w[e.I].DefIndex >= 0)
                    {
                        var d = World.AbilityDefs[w[e.I].DefIndex];
                        nm = d.Name.ToUpperInvariant();
                        col = RoleColor(d.Role);
                    }
                    Vector2 at = e.Pos == Vector2.Zero ? new Vector2(0, -World.B.PlanetRadius - 34f) : e.Pos;
                    Push(FxKind.CastRing, at, at, 42f, 0.5f, col);
                    Push(FxKind.Text, at, at, 0f, 1.1f, col, nm);
                    Root.Fx?.Flash(col, 0.10f);
                    AddShake(3f);
                }
                else if (e.I == -3) Root.Fx?.Flash(new Color(1f, 0.85f, 0.4f), 0.15f);
                break;
        }
    }

    private void Push(FxKind k, Vector2 a, Vector2 b, float r, float life, Color c, string text = "")
        => _fx.Add(new Fx { Kind = k, A = a, B = b, R = r, Life = life, Col = c, Text = text });

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = _fx.Count - 1; i >= 0; i--)
        {
            var f = _fx[i];
            f.Age += dt;
            if (f.Age >= f.Life) _fx.RemoveAt(i); else _fx[i] = f;
        }
        _beams.Clear();

        // exhaust trails for guided missiles — render-only, sampled off the sim state
        _puffTimer += dt;
        if (_puffTimer >= 0.03f && !Root.IsPaused && _fx.Count < 900)
        {
            _puffTimer = 0f;
            var pv = World.ProjectileView;
            for (int i = 0; i < pv.Length; i++)
            {
                ref readonly var p = ref pv[i];
                if (!p.Alive || p.Age < 0.04f) continue;
                bool guided = p.Kind == 1 || p.Kind == 3 || (p.Kind == 0 && p.Target.Index >= 0);
                if (!guided) continue;
                Vector2 back = p.Vel.LengthSquared() > 1f ? p.Vel.Normalized() : Vector2.Right;
                Color c = p.Kind == 1 ? new Color(1f, 0.72f, 0.42f) : new Color(0.72f, 0.9f, 1f);
                Push(FxKind.Smoke, p.Pos - back * 5f, Vector2.Zero,
                     2.4f + GD.Randf() * 2.2f, 0.45f + GD.Randf() * 0.4f, c);
            }
        }

        if (_shake > 0.05f)
        {
            _shakeOffset = new Vector2(GD.Randf() * 2f - 1f, GD.Randf() * 2f - 1f) * _shake;
            _shake = Mathf.MoveToward(_shake, 0f, 55f * dt);
        }
        else _shakeOffset = Vector2.Zero;
        Position = Root.WorldOrigin + _shakeOffset;

        if (Root.Fx != null)
            Root.Fx.IntegrityFrac = World.PlanetIntegrityMax > 0 ? World.PlanetIntegrity / World.PlanetIntegrityMax : 0f;
    }

    public override void _Draw()
    {
        DrawGuides();
        DrawPlanetRings();
        DrawAbilityZones();
        DrawTurrets();
        DrawSentinels();
        DrawOrbitalWeapons();
        DrawFxLayer(back: true);
        DrawEnemies();
        DrawProjectiles();
        DrawHero();
        DrawFxLayer(back: false);
    }

    // ---- sprite helper: draw a texture centred at pos, rotated, sized to `size` px wide ----
    private void Blit(Texture2D tex, Vector2 pos, float rot, float sizePx, Color mod)
    {
        var ts = tex.GetSize();
        float sc = sizePx / Mathf.Max(ts.X, ts.Y);
        DrawSetTransform(pos, rot, new Vector2(sc, sc));
        DrawTexture(tex, -ts * 0.5f, mod);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawGuides()
    {
        var b = World.B;
        DrawArc(Vector2.Zero, b.HeroOrbitMin, 0, Mathf.Tau, 48, new Color(0.4f, 0.7f, 1f, 0.035f), 1.5f);
        DrawArc(Vector2.Zero, b.HeroOrbitMax, 0, Mathf.Tau, 48, new Color(0.4f, 0.7f, 1f, 0.035f), 1.5f);
    }

    private void DrawPlanetRings()
    {
        var b = World.B;
        float pr = b.PlanetRadius;
        float integ = World.PlanetIntegrityMax > 0 ? World.PlanetIntegrity / World.PlanetIntegrityMax : 0f;

        if (World.PlanetShield > 0.5f)
            DrawArc(Vector2.Zero, pr + 9f, 0, Mathf.Tau, 64, new Color(0.4f, 0.85f, 1f, 0.5f + 0.2f * Mathf.Sin(World.GameTime * 6f)), 3f);

        var ic = integ > 0.5f ? new Color(0.4f, 0.95f, 0.55f)
               : integ > 0.25f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.35f, 0.3f);
        DrawArc(Vector2.Zero, pr + 5f, 0, Mathf.Tau, 72, new Color(1, 1, 1, 0.08f), 5f);
        DrawArc(Vector2.Zero, pr + 5f, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * Mathf.Clamp(integ, 0, 1), 80, ic, 5f);
    }

    private void DrawTurrets()
    {
        var turrets = World.TurretView;
        for (int i = 0; i < turrets.Length; i++)
        {
            ref readonly var t = ref turrets[i];
            bool sel = i == SelectedSlot;
            if (!t.Built)
            {
                float rr = sel ? 14f : 10f;
                DrawArc(t.Pos, rr, 0, Mathf.Tau, 18, sel ? new Color(1f, 0.9f, 0.4f) : new Color(1, 1, 1, 0.18f), sel ? 2.5f : 1.5f);
                if (sel) DrawCircle(t.Pos, 3f, new Color(1f, 0.9f, 0.4f));
                continue;
            }

            var def = World.TurretDefs[t.DefIndex];
            var col = Color.FromHsv(def.Color, 0.55f, 1f);
            bool disabled = t.DisabledLeft > 0f;
            bool firing = !t.Target.IsNone && t.CooldownLeft > 0.02f;
            bool support = def.Fire == "support";

            if (!support)
            {
                float half = Mathf.DegToRad(def.ArcDegrees) * 0.5f;
                DrawArc(t.Pos, def.Range, t.Angle - half, t.Angle + half, 24,
                        new Color(col, disabled ? 0.02f : firing ? 0.12f : 0.05f), 1.5f);
            }
            else
                DrawArc(t.Pos, def.SupportRange, 0, Mathf.Tau, 26, new Color(col, 0.10f), 1.5f);

            // sprite turret: tinted base platform + a gun head that aims at the target.
            // rank reads at a glance: a bigger, brighter gun and, at L3, an accent ring.
            float lvlScale = 1f + (t.Level - 1) * 0.16f;
            float baseR = 22f * (1f + (t.Level - 1) * 0.06f);
            DrawCircle(t.Pos, baseR, new Color(col, firing ? 0.24f : 0.13f));
            var baseTint = disabled ? new Color(0.52f, 0.42f, 0.42f) : col.Lightened(0.10f + (t.Level - 1) * 0.06f);
            Blit(Art.TurretBase, t.Pos, 0f, 42f * (1f + (t.Level - 1) * 0.05f), baseTint);

            if (t.Level >= 3 && !disabled)
                DrawArc(t.Pos, baseR + 3.5f, 0, Mathf.Tau, 30, new Color(col.Lightened(0.35f), 0.8f), 2f);

            float gunRot = support ? World.GameTime * 0.6f : t.Angle + Mathf.Pi / 2f;
            var gunTint = disabled ? new Color(0.6f, 0.55f, 0.55f) : Colors.White;
            Blit(Art.TurretGun(def.Id), t.Pos, gunRot, 40f * lvlScale, gunTint);

            if (firing && !support && def.Fire != "beam")
            {
                var muzzle = t.Pos + Vector2.FromAngle(t.Angle) * (23f * lvlScale);
                Blit(Art.Flare, muzzle, 0f, 17f * lvlScale, new Color(Ord.muzzle, 0.85f));
            }

            for (int l = 0; l < t.Level; l++)
                DrawCircle(t.Pos + new Vector2(-6f + l * 6f, -25f), 2.8f, Colors.White);
            if (t.Fork >= 0)
            {
                var fc = t.Fork == 0 ? new Color(1f, 0.85f, 0.3f) : new Color(0.4f, 0.85f, 1f);
                DrawRect(new Rect2(t.Pos + new Vector2(-7, 21), new Vector2(14, 4f)), fc);
                var tip = t.Pos + new Vector2(0, 27);
                DrawColoredPolygon(new[] { tip + new Vector2(-4, 0), tip + new Vector2(4, 0), tip + new Vector2(0, 5) }, fc);
            }
            if (disabled)
            {
                DrawLine(t.Pos + new Vector2(-8, -8), t.Pos + new Vector2(8, 8), new Color(1f, 0.35f, 0.35f), 3.5f);
                DrawLine(t.Pos + new Vector2(8, -8), t.Pos + new Vector2(-8, 8), new Color(1f, 0.35f, 0.35f), 3.5f);
            }
            if (sel) DrawArc(t.Pos, 25f, 0, Mathf.Tau, 24, new Color(1f, 0.9f, 0.4f), 2.5f);
        }
    }

    private void DrawSentinels()
    {
        var ss = World.SentinelView;
        for (int i = 0; i < ss.Length; i++)
        {
            var s = ss[i];
            float rot = s.Angle + Mathf.Pi;   // face along orbit
            DrawCircle(s.Pos, 14f, new Color(0.6f, 1f, 0.85f, 0.14f));
            Blit(Art.Ship, s.Pos, rot, 24f, new Color(0.7f, 1f, 0.9f));
        }
    }

    private static readonly Color[] OrbitalCols =
    {
        new(0.96f, 0.55f, 0.15f),  // cannon
        new(0.95f, 0.28f, 0.24f),  // laser
        new(0.98f, 0.82f, 0.20f),  // lightning
        new(0.55f, 0.92f, 0.25f),  // rad_line
        new(0.98f, 0.80f, 0.22f),  // shock_orb
        new(0.55f, 0.92f, 0.25f),  // rad_zone
    };

    private void DrawOrbitalWeapons()
    {
        float gt = World.GameTime;
        int n = World.OrbitalWeaponCount;

        // faint shared orbit ring the sentinels ride
        if (n > 0 && World.OrbitalWeaponLevel(0) >= 0)
        {
            float any = 0f;
            for (int i = 0; i < n; i++) if (World.OrbitalWeaponLevel(i) > 0) { any = World.OrbitalPlatformPos(i).Length(); break; }
            if (any > 0f) DrawArc(Vector2.Zero, any, 0, Mathf.Tau, 72, new Color(0.5f, 0.7f, 1f, 0.05f), 1.5f);
        }

        // the orbiting weapon platforms — chunky ringed sentinel stations (PDTD look)
        for (int i = 0; i < n; i++)
        {
            if (World.OrbitalWeaponLevel(i) <= 0) continue;
            var c = OrbitalCols[i % OrbitalCols.Length];
            var p = World.OrbitalPlatformPos(i);
            float ang = p.Angle() + Mathf.Pi / 2f;
            float lvl = World.OrbitalWeaponLevel(i);
            float sc = 1.5f + Mathf.Min(0.9f, lvl * 0.06f);
            Vector2 R(float x, float y) => p + new Vector2(x, y).Rotated(ang) * sc;

            DrawCircle(p, 20f * sc, new Color(c, 0.10f));
            DrawArc(p, 15f * sc, 0, Mathf.Tau, 22, new Color(c, 0.28f), 1.5f);   // station ring
            // hull disc
            DrawColoredPolygon(new[] { R(-11, -5), R(11, -5), R(14, 4), R(0, 10), R(-14, 4) }, new Color(0.09f, 0.11f, 0.16f));
            DrawPolyline(new[] { R(-11, -5), R(11, -5), R(14, 4), R(0, 10), R(-14, 4), R(-11, -5) }, new Color(c, 0.9f), 1.8f);
            // spires
            DrawLine(R(-6, -5), R(-6, -14), new Color(c, 0.8f), 1.8f);
            DrawLine(R(0, -6), R(0, -18), new Color(c, 0.95f), 2.2f);
            DrawLine(R(6, -5), R(6, -14), new Color(c, 0.8f), 1.8f);
            // core glow
            DrawCircle(p, 3.6f * sc * (0.8f + 0.2f * Mathf.Sin(gt * 5f + i)), new Color(c, 0.95f));
            DrawCircle(p, 1.8f, Colors.White);
        }

        // instant strike (cannon / laser) — beam + a radial impact shockwave at the mark
        if (World.FxOrbitalBeamLeft > 0f)
        {
            bool laser = World.FxOrbitalBeamKind == 1;
            var c = laser ? OrbitalCols[1] : OrbitalCols[0];
            float span = laser ? 0.16f : 0.12f;
            float k = Mathf.Clamp(World.FxOrbitalBeamLeft / span, 0f, 1f);
            var from = World.FxOrbitalBeamFrom;
            var to = World.FxOrbitalBeamTo;
            DrawLine(from, to, new Color(c, 0.32f * k), laser ? 7f : 5f);
            DrawLine(from, to, new Color(1f, 0.96f, 0.9f, 0.9f * k), laser ? 3f : 2f);
            // impact rings expanding on the ground
            float grow = (1f - k);
            DrawArc(to, 8f + grow * 46f, 0, Mathf.Tau, 28, new Color(c, 0.7f * k), 3f);
            DrawArc(to, 4f + grow * 26f, 0, Mathf.Tau, 22, new Color(1f, 0.95f, 0.9f, 0.6f * k), 2f);
        }

        // active field effects
        foreach (ref readonly var fx in World.OrbitalEffects)
        {
            if (fx.Kind == 1) // radiation line — PDTD Radiation Link: a fixed lethal corridor
            {
                var d = Vector2.FromAngle(fx.P0);
                Vector2 a = d * World.B.PlanetRadius;
                Vector2 b = d * World.B.DespawnRadius;
                var gc = OrbitalCols[3];
                DrawLine(a, b, new Color(gc, 0.22f), 30f);
                DrawLine(a, b, new Color(gc, 0.7f), 9f);
                DrawLine(a, b, new Color(0.92f, 1f, 0.82f, 0.95f), 3f);
                // travelling energy nodes along the corridor
                for (int s = 0; s < 6; s++)
                {
                    float ph = Mathf.PosMod(gt * 0.9f + s * 0.18f, 1f);
                    DrawCircle(a.Lerp(b, ph), 4f, new Color(0.9f, 1f, 0.8f, 0.8f));
                }
            }
            else if (fx.Kind == 4) // beam laser — PDTD Beam sentinel: continuous locked-on burn
            {
                var lc = OrbitalCols[1];
                DrawLine(fx.From, fx.Pos, new Color(lc, 0.35f), 8f);
                DrawLine(fx.From, fx.Pos, new Color(lc, 0.8f), 3.5f);
                DrawLine(fx.From, fx.Pos, new Color(1f, 0.95f, 0.9f, 0.9f), 1.4f);
                float pulse = 0.6f + 0.4f * Mathf.Sin(gt * 20f);
                DrawCircle(fx.Pos, 6f * pulse, new Color(lc, 0.8f));
                DrawArc(fx.Pos, 10f, 0, Mathf.Tau, 20, new Color(lc, 0.5f), 2f);
            }
            else // shock orb (2) / radiation zone (3) — concentric radial shockwaves (PDTD SHOCK ORB look)
            {
                var col = fx.Kind == 2 ? OrbitalCols[4] : OrbitalCols[5];
                DrawCircle(fx.Pos, fx.Radius, new Color(col, 0.07f));
                for (int ring = 0; ring < 4; ring++)
                {
                    float ph = Mathf.PosMod(gt * (fx.Kind == 2 ? 1.6f : 0.9f) + ring * 0.25f, 1f);
                    DrawArc(fx.Pos, fx.Radius * ph, 0, Mathf.Tau, 44, new Color(col, (1f - ph) * (fx.Kind == 2 ? 0.7f : 0.4f)), fx.Kind == 2 ? 3f : 2f);
                }
                DrawArc(fx.Pos, fx.Radius, 0, Mathf.Tau, 44, new Color(col, 0.45f), 2f);
                if (fx.Kind == 2)
                {
                    DrawCircle(fx.Pos, 8f, new Color(col, 0.95f));
                    DrawCircle(fx.Pos, 4f, Colors.White);
                    for (int s = 0; s < 6; s++)
                    {
                        float aa = gt * 11f + s * Mathf.Tau / 6f;
                        var e = fx.Pos + Vector2.FromAngle(aa) * fx.Radius * (0.6f + 0.35f * Mathf.Sin(gt * 7f + s));
                        DrawLine(fx.Pos, e, new Color(col, 0.35f), 1.4f);
                    }
                }
                else
                    for (int s = 0; s < 12; s++)
                    {
                        float aa = s * Mathf.Tau / 12f + gt * 0.4f;
                        DrawCircle(fx.Pos + Vector2.FromAngle(aa) * fx.Radius * (0.5f + 0.4f * Mathf.Sin(gt * 2f + s)), 2.4f, new Color(col, 0.55f));
                    }
            }
        }
    }

    private void DrawEnemies()
    {
        var enemies = World.EnemyView;
        for (int i = 0; i < enemies.Length; i++)
        {
            ref readonly var e = ref enemies[i];
            if (!e.Alive) continue;
            var def = World.EnemyDefAt(e.DefIndex);
            bool boss = def.Class == "boss";
            var tint = boss ? new Color(1f, 0.55f, 0.85f) : Tint(def.Id);
            var tex = Art.Enemy(boss ? "boss_threshing_gate" : def.Id);

            float sizePx = (boss ? e.Radius * 2.8f : e.Radius * 3.3f) + 10f;
            float rot = e.Vel.LengthSquared() > 1f ? e.Vel.Angle() + Mathf.Pi / 2f : e.Pos.Angle() + Mathf.Pi / 2f;

            DrawCircle(e.Pos, sizePx * 0.75f, new Color(tint, 0.16f));
            Blit(tex, e.Pos, boss && e.MechanicActive ? 0f : rot, sizePx,
                 boss && e.MechanicActive ? new Color(0.7f, 0.7f, 0.8f) : tint);

            if (boss && e.MechanicActive)
            {
                DrawArc(e.Pos, sizePx * 0.55f, 0, Mathf.Tau, 40, new Color(0.85f, 0.85f, 0.95f), 4f);
                float seam = World.GameTime * 1.5f;
                DrawLine(e.Pos, e.Pos + Vector2.FromAngle(seam) * (sizePx * 0.6f), new Color(1f, 0.9f, 0.4f), 4f);
            }
            if (e.Shield > 0.5f)
                DrawArc(e.Pos, sizePx * 0.6f, 0, Mathf.Tau, 22, new Color(0.4f, 0.9f, 1f, 0.85f), 2.5f);
            if (def.AuraRadius > 0f)
                DrawArc(e.Pos, def.AuraRadius, 0, Mathf.Tau, 40, new Color(0.5f, 1f, 0.6f, 0.08f), 2f);

            float hpf = e.MaxHp > 0 ? e.Hp / e.MaxHp : 1f;
            if (hpf < 0.999f && !boss)
            {
                float br = sizePx * 0.62f;
                DrawArc(e.Pos, br, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau, 20, new Color(0, 0, 0, 0.4f), 2.5f);
                DrawArc(e.Pos, br, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * hpf, 20, new Color(0.5f, 1f, 0.5f), 2.5f);
            }
        }
    }

    private void DrawProjectiles()
    {
        var projs = World.ProjectileView;
        var missile = Art.Missile;
        var bullet = Art.Bullet;
        for (int i = 0; i < projs.Length; i++)
        {
            ref readonly var p = ref projs[i];
            if (!p.Alive) continue;
            float rot = p.Vel.Angle() + Mathf.Pi / 2f;
            Vector2 back = p.Vel.LengthSquared() > 1f ? p.Vel.Normalized() : Vector2.Right;

            if (p.Kind == 1) // hero missile
            {
                var (om, ot, _) = Ord;
                float fl = 16f + Mathf.Min(p.Vel.Length() * 0.05f, 26f);
                DrawLine(p.Pos, p.Pos - back * fl, new Color(ot, 0.5f), 5f);
                DrawLine(p.Pos, p.Pos - back * (fl * 0.5f), new Color(om, 0.85f), 2.5f);
                Blit(missile, p.Pos, rot, 20f, om.Lightened(0.3f));
            }
            else if (p.Kind == 2) // enemy shell
            {
                DrawLine(p.Pos, p.Pos - back * 14f, new Color(1f, 0.4f, 0.35f, 0.5f), 3f);
                DrawCircle(p.Pos, 4.5f, new Color(1f, 0.45f, 0.4f));
            }
            else if (p.Kind == 3 || p.Target.Index >= 0) // planet-battery missile (homing turret shot)
            {
                float fl = 14f + Mathf.Min(p.Vel.Length() * 0.045f, 22f);
                DrawLine(p.Pos, p.Pos - back * fl, new Color(0.7f, 0.9f, 1f, 0.5f), 3.5f);
                DrawLine(p.Pos, p.Pos - back * (fl * 0.5f), new Color(0.95f, 0.99f, 1f, 0.8f), 2f);
                Blit(missile, p.Pos, rot, 16f, new Color(0.85f, 0.95f, 1f));
            }
            else // turret bolt
            {
                DrawLine(p.Pos, p.Pos - back * 16f, new Color(0.8f, 0.95f, 1f, 0.55f), 3f);
                Blit(bullet, p.Pos, rot, 12f, new Color(0.9f, 0.98f, 1f));
            }
        }
    }

    private void DrawHero()
    {
        var h = World.HeroView;
        if (!h.Alive)
        {
            DrawArc(Vector2.Zero, World.B.PlanetRadius + 22f, 0, Mathf.Tau, 40, new Color(1f, 0.5f, 0.5f, 0.3f), 2f);
            DrawString(ThemeDB.FallbackFont, new Vector2(-42, -World.B.PlanetRadius - 28f),
                       $"SHIP DOWN — {Mathf.CeilToInt(h.RespawnLeft)}s", HorizontalAlignment.Center, 84, 18, new Color(1f, 0.5f, 0.5f));
            return;
        }

        // face the direction of travel, else point outward
        Vector2 vel = h.Pos - _lastHeroPos; _lastHeroPos = h.Pos;
        float heading = vel.LengthSquared() > 0.5f ? vel.Angle() : h.Pos.Angle() + Mathf.Pi;
        float speedFrac = Mathf.Clamp(vel.Length() / 6f, 0f, 1f);
        float gt = World.GameTime;

        // --- Plasma Field aura (always-on once unlocked) ---
        float pr = World.PlasmaFieldRadius;
        if (pr > 0f)
        {
            var pc = new Color(0.82f, 0.24f, 0.95f);
            DrawCircle(h.Pos, pr, new Color(pc, 0.05f));
            DrawArc(h.Pos, pr, 0, Mathf.Tau, 44, new Color(pc, 0.28f + 0.08f * Mathf.Sin(gt * 5f)), 2.5f);
            for (int s = 0; s < 10; s++)
            {
                float a = gt * 1.7f + s * Mathf.Tau / 10f;
                DrawCircle(h.Pos + Vector2.FromAngle(a) * pr * (0.7f + 0.25f * Mathf.Sin(gt * 3f + s)), 2.6f, new Color(pc, 0.5f));
            }
        }

        DrawCircle(h.Pos, 44f, new Color(0.35f, 0.75f, 1f, 0.09f));
        DrawShip(h.Pos, heading, 1f, speedFrac, gt);

        // --- Shields Boost hex barrier ---
        if (World.HeroShield > 0f && World.HeroShieldLeft > 0f)
        {
            float sr = 40f;
            var sc = new Color(0.30f, 0.72f, 1f);
            for (int ring = 0; ring < 2; ring++)
            {
                float rr = sr + ring * 5f;
                var pts = new Vector2[7];
                for (int k = 0; k < 7; k++)
                    pts[k] = h.Pos + Vector2.FromAngle(gt * 0.6f + k * Mathf.Tau / 6f) * rr;
                DrawPolyline(pts, new Color(sc, 0.5f - ring * 0.2f), 2.5f - ring, true);
            }
            DrawCircle(h.Pos, sr, new Color(sc, 0.07f));
        }

        // --- Laser Volley cone ---
        if (World.FxLaserLeft > 0f)
        {
            float k = World.FxLaserLeft / 0.14f;
            var lc = new Color(0.95f, 0.22f, 0.20f);
            int beams = Mathf.Max(2, World.FxLaserBeams);
            float spread = Mathf.DegToRad(38f);
            float baseA = World.FxLaserAim.Angle();
            for (int bnum = 0; bnum < beams; bnum++)
            {
                float a = baseA + Mathf.Lerp(-spread, spread, beams == 1 ? 0.5f : bnum / (float)(beams - 1));
                var dir = Vector2.FromAngle(a);
                DrawLine(h.Pos + dir * 14f, h.Pos + dir * 560f, new Color(lc, 0.28f * k), 5f);
                DrawLine(h.Pos + dir * 14f, h.Pos + dir * 560f, new Color(1f, 0.8f, 0.75f, 0.8f * k), 2f);
            }
        }

        // --- Yamato Cannon blast ---
        if (World.FxYamatoLeft > 0f)
        {
            float k = World.FxYamatoLeft / 0.5f;
            var yc = new Color(1f, 0.80f, 0.15f);
            DrawLine(h.Pos, World.FxYamatoPos, new Color(yc, 0.5f * k), 10f * k + 3f);
            DrawLine(h.Pos, World.FxYamatoPos, new Color(1f, 1f, 0.9f, 0.9f * k), 4f * k + 1f);
            DrawArc(World.FxYamatoPos, World.FxYamatoRadius * (1.2f - k), 0, Mathf.Tau, 48, new Color(yc, k), 5f * k + 1f);
            DrawCircle(World.FxYamatoPos, World.FxYamatoRadius * (1.1f - k) * 0.5f, new Color(yc, 0.15f * k));
        }

        DrawArc(h.Pos, World.Cfg.Hero.PointDefenseRange, 0, Mathf.Tau, 40, new Color(0.55f, 0.9f, 1f, 0.045f), 1.2f);

        // tapped focus target — a spinning reticle + a lead line from the ship
        if (World.HeroFocusPos is { } fp)
        {
            float t = World.GameTime * 4f;
            var rc = new Color(1f, 0.55f, 0.35f);
            DrawLine(h.Pos, fp, new Color(rc, 0.35f), 1.5f);
            for (int q = 0; q < 4; q++)
            {
                float a = t + q * Mathf.Pi / 2f;
                var d = Vector2.FromAngle(a);
                DrawLine(fp + d * 10f, fp + d * 20f, rc, 2.5f);
            }
            DrawArc(fp, 15f, 0, Mathf.Tau, 20, new Color(rc, 0.5f), 1.5f);
        }

        if (h.OverdriveLeft > 0f)
            DrawArc(h.Pos, 24f + 4f * Mathf.Sin(World.GameTime * 20f), 0, Mathf.Tau, 22, new Color(1f, 0.55f, 0.2f), 2.5f);
        if (World.DronesActiveLeft > 0f)
            for (int d = 0; d < World.DroneCount; d++)
            {
                float a = World.GameTime * 3.5f + d * Mathf.Tau / Mathf.Max(1, World.DroneCount);
                var dp = h.Pos + Vector2.FromAngle(a) * 32f;
                DrawCircle(dp, 4f, new Color(0.7f, 1f, 0.9f));
            }

        float hf = h.MaxHull > 0 ? h.Hull / h.MaxHull : 1f;
        var bp = h.Pos + new Vector2(-22, -34);
        DrawRect(new Rect2(bp, new Vector2(44, 4)), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(bp, new Vector2(44 * hf, 4)), new Color(0.5f, 0.85f, 1f));
    }

    /// <summary>The player's ship — a heavy capital cruiser in the spirit of a
    /// Terran battlecruiser: long armoured hull, forward prow gun, a raised bridge,
    /// side sponsons and a bank of engine nozzles. Beyond's teal/magenta palette,
    /// drawn from polygons. Nose points along <paramref name="ang"/> (local +X).</summary>
    private void DrawShip(Vector2 c, float ang, float scale, float thrust, float gt)
    {
        Vector2 P(float x, float y) => c + new Vector2(x, y).Rotated(ang) * scale;

        var hull     = new Color(0.12f, 0.14f, 0.19f);
        var hullDark = new Color(0.08f, 0.09f, 0.13f);
        var hullLit  = new Color(0.19f, 0.24f, 0.33f);
        var edge     = new Color(0.32f, 0.74f, 0.98f);
        var mag      = new Color(0.86f, 0.30f, 0.62f);
        var glow     = new Color(0.40f, 0.86f, 1f);

        // ---- engine trail + nozzle wash ----
        if (thrust > 0.04f)
        {
            float tl = 30f + thrust * 46f;
            for (int e = -1; e <= 1; e++)
            {
                DrawLine(P(-26, e * 8f), P(-26 - tl, e * 8f), new Color(glow, 0.26f * thrust), 4.5f);
                DrawLine(P(-26, e * 8f), P(-26 - tl * 1.25f, e * 8f), new Color(1f, 1f, 1f, 0.18f * thrust), 2f);
            }
        }

        // ---- side sponsons (under the hull) ----
        foreach (int s in new[] { -1, 1 })
        {
            Vector2[] pod = { P(4, s * 11f), P(-10, s * 15f), P(-20, s * 14f), P(-16, s * 10f), P(-2, s * 9f) };
            DrawColoredPolygon(pod, hullDark);
            DrawPolyline(new[] { pod[1], pod[2], pod[3] }, new Color(edge, 0.5f), 1.4f);
            DrawCircle(P(-6, s * 13f), 2.2f, new Color(edge, 0.9f));   // point-defense turret
        }

        // ---- rear engine block ----
        DrawColoredPolygon(new[] { P(-18, -13f), P(-30, -11f), P(-30, 11f), P(-18, 13f) }, hullDark);

        // ---- main hull (long tapered slab) ----
        Vector2[] body =
        {
            P(34, -3f), P(26, -10f), P(-6, -12f), P(-22, -11f),
            P(-26, 0f),
            P(-22, 11f), P(-6, 12f), P(26, 10f), P(34, 3f),
        };
        DrawColoredPolygon(body, hull);
        // lit top-quarter panel
        DrawColoredPolygon(new[] { P(30, -2f), P(24, -8f), P(-4, -9f), P(-18, -8f), P(-16, -1f), P(4, -2f) }, hullLit);
        DrawPolyline(new[]
        {
            body[0], body[1], body[2], body[3], body[4], body[5], body[6], body[7], body[8], body[0],
        }, new Color(edge, 0.85f), 1.8f);
        // hull plating seams
        DrawLine(P(20, -9f), P(20, 9f), new Color(edge, 0.22f), 1f);
        DrawLine(P(4, -11f), P(4, 11f), new Color(edge, 0.22f), 1f);
        DrawLine(P(-12, -11f), P(-12, 11f), new Color(edge, 0.22f), 1f);
        // magenta racing stripe
        DrawLine(P(30, -5.5f), P(-20, -8.5f), new Color(mag, 0.55f), 1.6f);

        // ---- armoured prow + forward (Yamato) gun ----
        DrawColoredPolygon(new[] { P(34, -3f), P(43, 0f), P(34, 3f) }, hullDark);
        DrawLine(P(38, 0f), P(50, 0f), new Color(0.85f, 0.78f, 0.35f), 3.5f);   // main gun barrel
        DrawCircle(P(38, 0f), 2.4f, new Color(1f, 0.9f, 0.5f, 0.8f));

        // ---- raised bridge / command tower (forward-mid) ----
        Vector2[] bridge = { P(16, -5f), P(22, -4f), P(21, 4f), P(14, 5f) };
        DrawColoredPolygon(bridge, hullLit);
        DrawPolyline(new[] { bridge[0], bridge[1], bridge[2], bridge[3], bridge[0] }, edge, 1.4f);
        DrawColoredPolygon(new[] { P(20, -2.5f), P(23, 0f), P(20, 2.5f) }, new Color(0.7f, 0.95f, 1f, 0.9f));  // bridge glass

        // ---- engine nozzles ----
        float pulse = 0.72f + 0.28f * Mathf.Sin(gt * 16f);
        foreach (int e in new[] { -1, 0, 1 })
        {
            DrawCircle(P(-27, e * 8f), 4.4f * (0.7f + 0.3f * pulse), new Color(glow, 0.85f));
            DrawCircle(P(-27, e * 8f), 2.0f, Colors.White);
        }

        // ---- running lights ----
        float blink = Mathf.Sin(gt * 3f) > 0f ? 1f : 0.25f;
        DrawCircle(P(-2, -12f), 1.7f, new Color(1f, 0.35f, 0.35f, blink));
        DrawCircle(P(-2, 12f), 1.7f, new Color(0.35f, 1f, 0.45f, blink));
    }

    private void DrawAbilityZones()
    {
        var ab = World.AbilityView;
        float t = World.GameTime;
        for (int i = 0; i < ab.Length; i++)
        {
            ref readonly var a = ref ab[i];
            if (a.DefIndex < 0 || a.ActiveLeft <= 0f) continue;
            var def = World.AbilityDefs[a.DefIndex];
            switch (def.Kind)
            {
                case "barrage":
                    for (int s = 0; s <= 8; s++)
                    {
                        float an = a.P0 - a.P1 + 2f * a.P1 * s / 8f;
                        var dir = Vector2.FromAngle(an);
                        float phase = (t * 3f + s * 0.3f) % 1f;
                        DrawLine(dir * (World.B.PlanetRadius + 8f), dir * World.B.SpawnRadius, new Color(1f, 0.85f, 0.35f, 0.22f), 2f);
                        DrawCircle(dir * Mathf.Lerp(World.B.SpawnRadius, World.B.PlanetRadius + 8f, phase), 4f, new Color(1f, 0.9f, 0.5f, 0.8f));
                    }
                    break;
                case "lance":
                    DrawLine(World.HeroView.Pos, a.Anchor, new Color(1f, 0.4f, 0.95f, 0.7f), 5f + 2f * Mathf.Sin(t * 30f));
                    DrawLine(World.HeroView.Pos, a.Anchor, new Color(1, 1, 1, 0.6f), 2f);
                    DrawArc(a.Anchor, def.Radius, t * 4f, t * 4f + 4f, 16, new Color(1f, 0.6f, 1f), 3f);
                    break;
                case "slow":
                    DrawCircle(a.Anchor, def.Radius, new Color(0.4f, 0.6f, 1f, 0.10f));
                    DrawArc(a.Anchor, def.Radius * (0.7f + 0.3f * Mathf.Sin(t * 4f)), 0, Mathf.Tau, 36, new Color(0.5f, 0.7f, 1f, 0.6f), 2.5f);
                    break;
                case "snare":
                    for (int s = 0; s < 8; s++)
                    {
                        float an = s * Mathf.Pi / 4f + t;
                        DrawLine(a.Anchor + Vector2.FromAngle(an) * def.Radius, a.Anchor + Vector2.FromAngle(an) * 6f, new Color(0.8f, 0.4f, 1f, 0.5f), 2f);
                    }
                    DrawArc(a.Anchor, def.Radius, 0, Mathf.Tau, 28, new Color(0.85f, 0.45f, 1f, 0.6f), 2.5f);
                    break;
                case "pointdef":
                    DrawArc(Vector2.Zero, World.PdgRadius, 0, Mathf.Tau, 56, new Color(0.5f, 0.9f, 1f, 0.14f + 0.06f * Mathf.Sin(t * 6f)), 3f);
                    break;
                case "salvage":
                    DrawCircle(World.SalvageAnchor, World.SalvageRadius, new Color(1f, 0.85f, 0.3f, 0.08f));
                    DrawArc(World.SalvageAnchor, World.SalvageRadius, 0, Mathf.Tau, 32, new Color(1f, 0.85f, 0.35f, 0.5f), 2f);
                    break;
                case "barrier":
                    if (a.P2 > 0f)
                        DrawArc(Vector2.Zero, World.B.PlanetRadius + 14f, 0, Mathf.Tau, 64, new Color(0.4f, 0.85f, 1f, 0.5f + 0.2f * Mathf.Sin(t * 8f)), 4f);
                    break;
                case "repair":
                    for (int s = 0; s < 6; s++)
                    {
                        float ph = (t * 1.5f + s * 0.16f) % 1f;
                        var pos = Vector2.FromAngle(s * 1.05f + t) * Mathf.Lerp(World.B.PlanetRadius + 60f, World.B.PlanetRadius, ph);
                        DrawCircle(pos, 3f, new Color(0.5f, 1f, 0.6f, 1f - ph));
                    }
                    break;
            }
        }
    }

    private void DrawFxLayer(bool back)
    {
        if (back)
        {
            foreach (var f in _fx)
            {
                if (f.Kind != FxKind.Smoke) continue;
                float sk = Mathf.Clamp(f.Age / f.Life, 0f, 1f);
                DrawCircle(f.A, f.R * (0.6f + sk * 1.9f), new Color(f.Col, (1f - sk) * 0.28f));
            }
            foreach (var (a, b, w, c) in _beams)
            {
                DrawLine(a, b, new Color(c, 0.25f), w * 2.5f);
                DrawLine(a, b, c, w);
                DrawLine(a, b, new Color(1, 1, 1, 0.7f), w * 0.4f);
                DrawCircle(b, w, new Color(c, 0.6f));
            }
            return;
        }

        var flare = Art.Flare;
        foreach (var f in _fx)
        {
            float k = Mathf.Clamp(f.Age / f.Life, 0f, 1f);
            switch (f.Kind)
            {
                case FxKind.Muzzle:
                    Blit(flare, f.A, 0f, f.R * (2f + k * 2f), new Color(f.Col, (1f - k) * 0.9f));
                    break;
                case FxKind.Tracer:
                    DrawLine(f.A, f.B, new Color(f.Col, (1f - k) * f.Col.A), f.R);
                    break;
                case FxKind.Spark:
                    Blit(flare, f.A, k * 3f, f.R * (1.5f + k), new Color(f.Col, 1f - k));
                    break;
                case FxKind.Boom:
                    DrawArc(f.A, f.R * (0.3f + k * 1.7f), 0, Mathf.Tau, 24, new Color(f.Col, 1f - k), 3f * (1f - k));
                    Blit(flare, f.A, 0f, f.R * (1f - k) * 2.4f, new Color(1f, 0.85f, 0.55f, (1f - k) * 0.7f));
                    break;
                case FxKind.Shock:
                    DrawArc(f.A, f.R * (0.1f + k), 0, Mathf.Tau, 48, new Color(f.Col, (1f - k) * 0.9f), 6f * (1f - k));
                    DrawArc(f.A, f.R * (0.1f + k) * 0.8f, 0, Mathf.Tau, 48, new Color(1, 1, 1, (1f - k) * 0.5f), 3f);
                    break;
                case FxKind.Lightning:
                    DrawLightning(f.A, f.B, f.Col, 1f - k);
                    break;
                case FxKind.Warp:
                    DrawArc(f.A, f.R * (1f - k), 0, Mathf.Tau, 16, new Color(f.Col, (1f - k) * 0.8f), 2f);
                    break;
                case FxKind.CastRing:
                    DrawArc(f.A, f.R * (0.2f + k * 1.6f), 0, Mathf.Tau, 32, new Color(f.Col, 1f - k), 4f * (1f - k));
                    break;
                case FxKind.Text:
                    DrawString(ThemeDB.FallbackFont, f.A + new Vector2(-70, -22 - k * 28f), f.Text,
                               HorizontalAlignment.Center, 140, 17, new Color(f.Col, 1f - k));
                    break;
            }
        }
    }

    private void DrawLightning(Vector2 a, Vector2 b, Color c, float alpha)
    {
        int seg = 6;
        Vector2 prev = a;
        var perp = (b - a).Normalized(); perp = new Vector2(-perp.Y, perp.X);
        for (int i = 1; i <= seg; i++)
        {
            float tt = i / (float)seg;
            var mid = a.Lerp(b, tt) + perp * (GD.Randf() * 2f - 1f) * 9f * (1f - tt);
            DrawLine(prev, mid, new Color(c, alpha), 2.5f);
            DrawLine(prev, mid, new Color(1, 1, 1, alpha * 0.6f), 1f);
            prev = mid;
        }
    }

    private static Color RoleColor(string role) => role switch
    {
        "offense" => new Color(1f, 0.5f, 0.4f),
        "control" => new Color(0.62f, 0.62f, 1f),
        "defense" => new Color(0.4f, 0.86f, 1f),
        _ => new Color(1f, 0.85f, 0.4f),
    };
}
