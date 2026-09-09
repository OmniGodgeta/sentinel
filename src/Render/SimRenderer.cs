using System.Collections.Generic;
using Godot;
using Sentinel.Sim;
using Sentinel.Game;

namespace Sentinel.Render;

/// <summary>
/// Play-field renderer. Still shapes-only (no sprite art yet) but with real
/// weapon and ability VFX, high-contrast enemies, muzzle flashes, tracers,
/// lightning, shock rings, screen flashes and floating text so the game reads.
/// </summary>
public sealed partial class SimRenderer : Node2D
{
    public GameRoot Root = null!;
    public SimWorld World = null!;
    public int SelectedSlot = -1;

    // ---------------- fx ----------------
    private enum FxKind : byte { Muzzle, Tracer, Spark, Boom, Shock, Lightning, Text, CastRing, Warp }
    private struct Fx
    {
        public FxKind Kind;
        public Vector2 A, B;
        public float R, Age, Life, P;
        public Color Col;
        public string Text;
    }
    private readonly List<Fx> _fx = new(256);
    private readonly List<(Vector2 a, Vector2 b, float w, Color c)> _beams = new(32);

    private Vector2 _shakeOffset;
    private float _shake;

    // static starfield (world space)
    private Vector2[] _stars = System.Array.Empty<Vector2>();
    private float[] _starMag = System.Array.Empty<float>();

    private static readonly Color[] EnemyColors =
    {
        new(0.95f, 0.35f, 0.35f), // 0 warm red   (skiff)
        new(0.55f, 0.65f, 1.00f), // 1 blue        (hauler / heavy)
        new(1.00f, 0.75f, 0.30f), // 2 amber       (interceptor)
        new(0.45f, 0.95f, 0.95f), // 3 cyan        (shielded)
        new(0.80f, 0.45f, 1.00f), // 4 violet      (phase / special)
        new(0.60f, 1.00f, 0.55f), // 5 green       (warden)
        new(1.00f, 0.55f, 0.75f), // 6 pink        (siege / boss)
    };

    public void AddShake(float a) => _shake = Mathf.Min(16f, _shake + a);

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator { Seed = 0xBADCAB };
        int n = 130;
        _stars = new Vector2[n];
        _starMag = new float[n];
        float r = World.B.DespawnRadius * 1.1f;
        for (int i = 0; i < n; i++)
        {
            float a = rng.Randf() * Mathf.Tau;
            float d = Mathf.Sqrt(rng.Randf()) * r;
            _stars[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            _starMag[i] = 0.15f + rng.Randf() * 0.5f;
        }
    }

    public void OnSimEvent(in SimEvent e)
    {
        switch (e.Kind)
        {
            case SimEventKind.TurretFired:
                Push(FxKind.Muzzle, e.Pos, e.Pos, 7f, 0.09f, new Color(1f, 0.95f, 0.7f));
                Push(FxKind.Tracer, e.Pos, e.PosB, 2f, 0.10f, new Color(1f, 0.9f, 0.6f, 0.9f));
                break;
            case SimEventKind.BeamTick:
                _beams.Add((e.Pos, e.PosB, 3f + e.A * 1.5f, new Color(1f, 0.25f, 0.35f)));
                if (GD.Randf() < 0.25f) Push(FxKind.Spark, e.PosB, e.PosB, 6f, 0.15f, new Color(1f, 0.4f, 0.4f));
                break;
            case SimEventKind.ChainArc:
                Push(FxKind.Lightning, e.Pos, e.PosB, 0f, 0.16f, new Color(0.6f, 0.85f, 1f));
                break;
            case SimEventKind.BarrageTick:
                Push(FxKind.Spark, e.Pos, e.Pos, 6f, 0.18f, new Color(1f, 0.85f, 0.4f));
                break;
            case SimEventKind.EnemyHit:
                if (e.A > 0f) Push(FxKind.Spark, e.Pos, e.Pos, 5f + Mathf.Min(e.A * 0.15f, 8f), 0.16f, new Color(1f, 0.85f, 0.5f));
                break;
            case SimEventKind.EnemyKilled:
                Push(FxKind.Boom, e.Pos, e.Pos, Mathf.Max(10f, e.A * 2.2f), 0.38f, new Color(1f, 0.7f, 0.35f));
                for (int s = 0; s < 5; s++)
                {
                    var d = Vector2.FromAngle(GD.Randf() * Mathf.Tau) * (e.A + 6f);
                    Push(FxKind.Tracer, e.Pos, e.Pos + d, 2f, 0.25f, new Color(1f, 0.6f, 0.3f, 0.8f));
                }
                break;
            case SimEventKind.MissileImpact:
                Push(FxKind.Boom, e.Pos, e.Pos, Mathf.Max(16f, e.A * 1.4f), 0.45f, new Color(1f, 0.6f, 0.25f));
                AddShake(2.5f);
                break;
            case SimEventKind.VolleyLaunched:
                Push(FxKind.Muzzle, e.Pos, e.Pos, 14f, 0.14f, new Color(1f, 0.8f, 0.5f));
                Root.Fx?.Flash(new Color(1f, 0.8f, 0.5f), 0.06f);
                break;
            case SimEventKind.EnemySpawned:
                Push(FxKind.Warp, e.Pos, e.Pos, e.A + 8f, 0.35f, new Color(0.7f, 0.5f, 1f));
                break;
            case SimEventKind.PlanetHit:
                AddShake(Mathf.Min(9f, e.A * 0.08f));
                Root.Fx?.Flash(new Color(1f, 0.3f, 0.25f), Mathf.Min(0.18f, e.A * 0.002f));
                break;
            case SimEventKind.NovaPulse:
                Push(FxKind.Shock, Vector2.Zero, Vector2.Zero, e.A, 0.6f, new Color(0.7f, 0.9f, 1f));
                Push(FxKind.Shock, Vector2.Zero, Vector2.Zero, e.A * 0.7f, 0.5f, new Color(1f, 1f, 1f));
                AddShake(10f);
                Root.Fx?.Flash(Colors.White, 0.35f);
                break;
            case SimEventKind.HeroDown:
                Push(FxKind.Boom, e.Pos, e.Pos, 30f, 0.6f, new Color(0.6f, 0.8f, 1f));
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
                    Vector2 at = e.Pos == Vector2.Zero ? new Vector2(0, -World.B.PlanetRadius - 30f) : e.Pos;
                    Push(FxKind.CastRing, at, at, 40f, 0.5f, col);
                    Push(FxKind.Text, at, at, 0f, 1.1f, col, nm);
                    Root.Fx?.Flash(col, 0.12f);
                    AddShake(3f);
                }
                else if (e.I == -3)
                {
                    Root.Fx?.Flash(new Color(1f, 0.85f, 0.4f), 0.15f);
                }
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
        DrawStars();
        DrawGuides();
        DrawPlanet();
        DrawAbilityZones();
        DrawTurrets();
        DrawBeamsAndFx(back: true);
        DrawEnemies();
        DrawProjectiles();
        DrawHero();
        DrawBeamsAndFx(back: false);
    }

    // ---------------- background ----------------
    private void DrawStars()
    {
        for (int i = 0; i < _stars.Length; i++)
            DrawCircle(_stars[i], _starMag[i] * 1.6f, new Color(1, 1, 1, _starMag[i] * 0.35f));
    }

    private void DrawGuides()
    {
        var b = World.B;
        DrawArc(Vector2.Zero, b.HeroOrbitMin, 0, Mathf.Tau, 48, new Color(0.4f, 0.7f, 1f, 0.04f), 1.5f);
        DrawArc(Vector2.Zero, b.HeroOrbitMax, 0, Mathf.Tau, 48, new Color(0.4f, 0.7f, 1f, 0.04f), 1.5f);
    }

    private void DrawPlanet()
    {
        var b = World.B;
        float pr = b.PlanetRadius;
        float integ = World.PlanetIntegrityMax > 0 ? World.PlanetIntegrity / World.PlanetIntegrityMax : 0f;

        // atmosphere glow
        for (int g = 4; g >= 1; g--)
            DrawCircle(Vector2.Zero, pr + g * 10f, new Color(0.35f, 0.55f, 0.9f, 0.04f));

        DrawCircle(Vector2.Zero, pr, new Color(0.10f, 0.14f, 0.22f));
        DrawCircle(Vector2.Zero, pr, new Color(0.16f, 0.22f, 0.34f)); // flat; keep simple
        // surface bands
        DrawArc(Vector2.Zero, pr * 0.7f, 0.4f, 2.6f, 20, new Color(1, 1, 1, 0.05f), 3f);
        DrawArc(Vector2.Zero, pr * 0.45f, 3.2f, 5.2f, 20, new Color(1, 1, 1, 0.04f), 4f);

        // planet shield
        if (World.PlanetShield > 0.5f)
            DrawArc(Vector2.Zero, pr + 8f, 0, Mathf.Tau, 64, new Color(0.4f, 0.85f, 1f, 0.55f), 3f);

        // integrity ring
        var ic = integ > 0.5f ? new Color(0.4f, 0.95f, 0.55f)
               : integ > 0.25f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.35f, 0.3f);
        DrawArc(Vector2.Zero, pr + 3f, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.08f), 5f);
        DrawArc(Vector2.Zero, pr + 3f, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * Mathf.Clamp(integ, 0, 1), 72, ic, 5f);
    }

    // ---------------- turrets ----------------
    private void DrawTurrets()
    {
        var turrets = World.TurretView;
        for (int i = 0; i < turrets.Length; i++)
        {
            ref readonly var t = ref turrets[i];
            bool sel = i == SelectedSlot;

            if (!t.Built)
            {
                float rr = sel ? 13f : 9f;
                DrawArc(t.Pos, rr, 0, Mathf.Tau, 18, sel ? new Color(1f, 0.9f, 0.4f) : new Color(1, 1, 1, 0.18f), sel ? 2.5f : 1.5f);
                if (sel) DrawCircle(t.Pos, 3f, new Color(1f, 0.9f, 0.4f));
                continue;
            }

            var def = World.TurretDefs[t.DefIndex];
            var col = Color.FromHsv(def.Color, 0.45f, 1f);
            bool disabled = t.DisabledLeft > 0f;
            bool firing = !t.Target.IsNone && t.CooldownLeft > 0.02f;

            // range arc (brighter when firing)
            if (def.Fire != "support")
            {
                float half = Mathf.DegToRad(def.ArcDegrees) * 0.5f;
                DrawArc(t.Pos, def.Range, t.Angle - half, t.Angle + half, 24,
                        new Color(col, disabled ? 0.02f : firing ? 0.12f : 0.05f), 1.5f);
            }
            else
            {
                DrawArc(t.Pos, def.SupportRange, 0, Mathf.Tau, 26, new Color(col, 0.10f), 1.5f);
            }

            // body
            var bodyCol = disabled ? new Color(0.4f, 0.24f, 0.24f) : col;
            DrawCircle(t.Pos, 20f, new Color(bodyCol, firing ? 0.32f : 0.18f));   // glow
            DrawCircle(t.Pos, 13f, bodyCol);
            DrawCircle(t.Pos, 13f, new Color(bodyCol.Lightened(0.2f), 1f));
            DrawArc(t.Pos, 13f, 0, Mathf.Tau, 16, new Color(0, 0, 0, 0.55f), 2.5f);
            DrawArc(t.Pos, 13f, 0, Mathf.Tau, 16, Colors.White, 1.5f);
            if (!disabled && def.Fire != "support")
            {
                Vector2 tip = t.Pos + Vector2.FromAngle(t.Angle) * 22f;
                DrawLine(t.Pos, tip, new Color(0, 0, 0, 0.6f), 6f);
                DrawLine(t.Pos, tip, Colors.White, 3.5f);
                DrawCircle(tip, 3f, Colors.White);
            }

            // level pips
            for (int l = 0; l < t.Level; l++)
                DrawCircle(t.Pos + new Vector2(-5f + l * 5f, -19f), 2.2f, Colors.White);
            if (t.Fork >= 0)
                DrawRect(new Rect2(t.Pos + new Vector2(-5, 15), new Vector2(10, 3)), new Color(1f, 0.85f, 0.3f));

            if (disabled)
            {
                DrawLine(t.Pos + new Vector2(-6, -6), t.Pos + new Vector2(6, 6), new Color(1f, 0.35f, 0.35f), 2.5f);
                DrawLine(t.Pos + new Vector2(6, -6), t.Pos + new Vector2(-6, 6), new Color(1f, 0.35f, 0.35f), 2.5f);
            }
            if (sel) DrawArc(t.Pos, 17f, 0, Mathf.Tau, 22, new Color(1f, 0.9f, 0.4f), 2.5f);
        }
    }

    // ---------------- enemies ----------------
    private void DrawEnemies()
    {
        var enemies = World.EnemyView;
        for (int i = 0; i < enemies.Length; i++)
        {
            ref readonly var e = ref enemies[i];
            if (!e.Alive) continue;
            var def = World.EnemyDefAt(e.DefIndex);
            var col = EnemyColors[System.Math.Clamp(Mathf.RoundToInt(def.Color * (EnemyColors.Length - 1)), 0, EnemyColors.Length - 1)];
            float vr = e.Radius * 2.0f + 5f;
            bool boss = def.Class == "boss";
            if (boss) vr = e.Radius + 10f;

            // glow
            DrawCircle(e.Pos, vr * 2.3f, new Color(col, 0.14f));
            DrawCircle(e.Pos, vr * 1.5f, new Color(col, 0.20f));

            if (boss)
            {
                DrawCircle(e.Pos, vr, e.MechanicActive ? new Color(0.55f, 0.55f, 0.62f) : col);
                DrawArc(e.Pos, vr + 5f, 0, Mathf.Tau, 40, e.MechanicActive ? new Color(0.8f, 0.8f, 0.9f) : new Color(1f, 0.35f, 0.4f), 4f);
                if (e.MechanicActive)
                {
                    float seam = World.GameTime * 1.5f;
                    DrawLine(e.Pos, e.Pos + Vector2.FromAngle(seam) * (vr + 14f), new Color(1f, 0.9f, 0.4f), 4f);
                    DrawArc(e.Pos, vr + 5f, seam - 0.25f, seam + 0.25f, 8, new Color(1f, 0.9f, 0.4f), 5f);
                }
            }
            else
            {
                DrawEnemyShape(e.Pos, vr, def.Id, col);
                DrawArc(e.Pos, vr, 0, Mathf.Tau, 16, new Color(0, 0, 0, 0.55f), 2f);
            }

            // shield
            if (e.Shield > 0.5f)
                DrawArc(e.Pos, vr + 4f, 0, Mathf.Tau, 20, new Color(0.4f, 0.9f, 1f, 0.85f), 2.5f);
            // warden aura
            if (def.AuraRadius > 0f)
                DrawArc(e.Pos, def.AuraRadius, 0, Mathf.Tau, 40, new Color(0.5f, 1f, 0.6f, 0.08f), 2f);

            // health ring
            float hpf = e.MaxHp > 0 ? e.Hp / e.MaxHp : 1f;
            if (hpf < 0.999f)
            {
                DrawArc(e.Pos, vr + 7f, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau, 20, new Color(0, 0, 0, 0.4f), 2.5f);
                DrawArc(e.Pos, vr + 7f, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * hpf, 20, new Color(0.5f, 1f, 0.5f), 2.5f);
            }
        }
    }

    private void DrawEnemyShape(Vector2 p, float r, string id, Color col)
    {
        switch (id)
        {
            case "skiff":
            case "interceptor":
            {
                var v = new[] { p + new Vector2(0, -r * 1.2f), p + new Vector2(r, r * 0.9f), p + new Vector2(-r, r * 0.9f) };
                DrawColoredPolygon(v, col);
                break;
            }
            case "hauler":
            case "carrier":
                DrawColoredPolygon(Ngon(p, r, 6, 0f), col);
                break;
            case "phase_runner":
            {
                var pts = new Vector2[8];
                for (int k = 0; k < 8; k++)
                    pts[k] = p + Vector2.FromAngle(k * Mathf.Pi / 4f) * (k % 2 == 0 ? r * 1.2f : r * 0.5f);
                DrawColoredPolygon(pts, col);
                break;
            }
            case "siege_crawler":
                DrawColoredPolygon(Ngon(p, r, 8, Mathf.Pi / 8f), col);
                break;
            case "bombard":
                DrawRect(new Rect2(p - new Vector2(r, r), new Vector2(r * 2, r * 2)), col);
                break;
            case "warden":
                DrawCircle(p, r, col);
                DrawLine(p + new Vector2(-r, 0), p + new Vector2(r, 0), Colors.White, 2f);
                DrawLine(p + new Vector2(0, -r), p + new Vector2(0, r), Colors.White, 2f);
                break;
            default:
                DrawCircle(p, r, col);
                break;
        }
    }

    private static Vector2[] Ngon(Vector2 c, float r, int n, float rot)
    {
        var v = new Vector2[n];
        for (int i = 0; i < n; i++) v[i] = c + Vector2.FromAngle(rot + i * Mathf.Tau / n) * r;
        return v;
    }

    // ---------------- projectiles ----------------
    private void DrawProjectiles()
    {
        var projs = World.ProjectileView;
        for (int i = 0; i < projs.Length; i++)
        {
            ref readonly var p = ref projs[i];
            if (!p.Alive) continue;
            Vector2 back = p.Vel.LengthSquared() > 1f ? p.Vel.Normalized() : Vector2.Right;

            if (p.Kind == 1) // hero missile
            {
                DrawLine(p.Pos, p.Pos - back * 22f, new Color(1f, 0.55f, 0.25f, 0.5f), 4f);
                DrawCircle(p.Pos, 5.5f, new Color(1f, 0.9f, 0.6f));
                DrawCircle(p.Pos, 9f, new Color(1f, 0.6f, 0.3f, 0.35f));
            }
            else if (p.Kind == 2) // enemy shell
            {
                DrawLine(p.Pos, p.Pos - back * 14f, new Color(1f, 0.4f, 0.35f, 0.5f), 3f);
                DrawCircle(p.Pos, 4.5f, new Color(1f, 0.45f, 0.4f));
            }
            else // turret shot
            {
                DrawLine(p.Pos, p.Pos - back * 16f, new Color(0.8f, 0.95f, 1f, 0.55f), 3f);
                DrawCircle(p.Pos, 3.5f, Colors.White);
                DrawCircle(p.Pos, 6f, new Color(0.7f, 0.9f, 1f, 0.35f));
            }
        }
    }

    // ---------------- hero ----------------
    private void DrawHero()
    {
        var h = World.HeroView;
        if (!h.Alive)
        {
            DrawArc(Vector2.Zero, World.B.PlanetRadius + 20f, 0, Mathf.Tau, 40, new Color(1f, 0.5f, 0.5f, 0.3f), 2f);
            DrawString(ThemeDB.FallbackFont, new Vector2(-40, -World.B.PlanetRadius - 26f),
                       $"SHIP DOWN — {Mathf.CeilToInt(h.RespawnLeft)}s", HorizontalAlignment.Center, 80, 18, new Color(1f, 0.5f, 0.5f));
            return;
        }

        float ang = h.Pos.Angle() + Mathf.Pi / 2f;
        var fwd = Vector2.FromAngle(ang);
        var side = new Vector2(-fwd.Y, fwd.X);
        var col = new Color(0.55f, 0.9f, 1f);

        DrawCircle(h.Pos, 26f, new Color(col, 0.10f));
        // engine glow
        DrawCircle(h.Pos - fwd * 14f, 6f, new Color(0.6f, 0.8f, 1f, 0.6f));

        var hull = new[]
        {
            h.Pos + fwd * 20f,
            h.Pos + side * 12f + fwd * 2f,
            h.Pos + side * 8f - fwd * 14f,
            h.Pos - side * 8f - fwd * 14f,
            h.Pos - side * 12f + fwd * 2f,
        };
        DrawColoredPolygon(hull, col);
        DrawPolyline(hull, new Color(0.85f, 0.97f, 1f), 2f, true);

        DrawArc(h.Pos, World.Cfg.Hero.PointDefenseRange, 0, Mathf.Tau, 40, new Color(col, 0.05f), 1.2f);

        if (h.OverdriveLeft > 0f)
        {
            float pulse = 20f + 4f * Mathf.Sin(World.GameTime * 20f);
            DrawArc(h.Pos, pulse, 0, Mathf.Tau, 22, new Color(1f, 0.55f, 0.2f), 2.5f);
        }
        if (World.DronesActiveLeft > 0f)
            for (int d = 0; d < World.DroneCount; d++)
            {
                float a = World.GameTime * 3.5f + d * Mathf.Tau / Mathf.Max(1, World.DroneCount);
                var dp = h.Pos + Vector2.FromAngle(a) * 30f;
                DrawCircle(dp, 4f, new Color(0.7f, 1f, 0.9f));
                DrawCircle(dp, 7f, new Color(0.7f, 1f, 0.9f, 0.3f));
            }

        // hull bar
        float hf = h.MaxHull > 0 ? h.Hull / h.MaxHull : 1f;
        var bp = h.Pos + new Vector2(-20, -28);
        DrawRect(new Rect2(bp, new Vector2(40, 4)), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(bp, new Vector2(40 * hf, 4)), col);
    }

    // ---------------- ability zones (live from sim state) ----------------
    private void DrawAbilityZones()
    {
        var ab = World.AbilityView;
        for (int i = 0; i < ab.Length; i++)
        {
            ref readonly var a = ref ab[i];
            if (a.DefIndex < 0 || a.ActiveLeft <= 0f) continue;
            var def = World.AbilityDefs[a.DefIndex];
            float t = World.GameTime;
            switch (def.Kind)
            {
                case "barrage":
                {
                    for (int s = 0; s <= 8; s++)
                    {
                        float an = a.P0 - a.P1 + 2f * a.P1 * s / 8f;
                        var dir = Vector2.FromAngle(an);
                        float phase = (t * 3f + s * 0.3f) % 1f;
                        DrawLine(dir * (World.B.PlanetRadius + 8f), dir * World.B.SpawnRadius, new Color(1f, 0.85f, 0.35f, 0.22f), 2f);
                        DrawCircle(dir * Mathf.Lerp(World.B.SpawnRadius, World.B.PlanetRadius + 8f, phase), 4f, new Color(1f, 0.9f, 0.5f, 0.8f));
                    }
                    break;
                }
                case "lance":
                    DrawLine(World.HeroView.Pos, a.Anchor, new Color(1f, 0.4f, 0.95f, 0.7f), 5f + 2f * Mathf.Sin(t * 30f));
                    DrawLine(World.HeroView.Pos, a.Anchor, new Color(1f, 1f, 1f, 0.6f), 2f);
                    DrawCircle(a.Anchor, def.Radius * Mathf.Max(0.3f, World.Mods.AbilityRadiusMult), new Color(1f, 0.4f, 0.95f, 0.14f));
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
                        DrawArc(Vector2.Zero, World.B.PlanetRadius + 12f, 0, Mathf.Tau, 64, new Color(0.4f, 0.85f, 1f, 0.5f + 0.2f * Mathf.Sin(t * 8f)), 4f);
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

    // ---------------- fx + beams ----------------
    private void DrawBeamsAndFx(bool back)
    {
        if (back)
        {
            foreach (var (a, b, w, c) in _beams)
            {
                DrawLine(a, b, new Color(c, 0.25f), w * 2.5f);
                DrawLine(a, b, c, w);
                DrawLine(a, b, new Color(1, 1, 1, 0.7f), w * 0.4f);
                DrawCircle(b, w, new Color(c, 0.6f));
            }
            return;
        }

        foreach (var f in _fx)
        {
            float k = Mathf.Clamp(f.Age / f.Life, 0f, 1f);
            switch (f.Kind)
            {
                case FxKind.Muzzle:
                    DrawCircle(f.A, f.R * (1f + k), new Color(f.Col, 1f - k));
                    DrawCircle(f.A, f.R * 0.5f * (1f + k), new Color(1, 1, 1, (1f - k) * 0.9f));
                    break;
                case FxKind.Tracer:
                    DrawLine(f.A, f.B, new Color(f.Col, (1f - k) * f.Col.A), f.R);
                    break;
                case FxKind.Spark:
                    for (int s = 0; s < 6; s++)
                    {
                        var d = Vector2.FromAngle(s * Mathf.Pi / 3f + f.Life * 13f) * f.R * (0.5f + k);
                        DrawLine(f.A, f.A + d, new Color(f.Col, 1f - k), 2f);
                    }
                    break;
                case FxKind.Boom:
                    DrawArc(f.A, f.R * (0.3f + k * 1.7f), 0, Mathf.Tau, 24, new Color(f.Col, 1f - k), 3f * (1f - k));
                    DrawCircle(f.A, f.R * (1f - k) * 0.8f, new Color(1f, 0.9f, 0.6f, (1f - k) * 0.6f));
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
                    DrawLine(f.A - new Vector2(0, f.R * 2f * (1f - k)), f.A, new Color(f.Col, (1f - k) * 0.5f), 2f);
                    break;
                case FxKind.CastRing:
                    DrawArc(f.A, f.R * (0.2f + k * 1.6f), 0, Mathf.Tau, 32, new Color(f.Col, 1f - k), 4f * (1f - k));
                    break;
                case FxKind.Text:
                {
                    var p = f.A + new Vector2(-60, -20 - k * 26f);
                    DrawString(ThemeDB.FallbackFont, p, f.Text, HorizontalAlignment.Center, 120, 16, new Color(f.Col, 1f - k));
                    break;
                }
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
            var mid = a.Lerp(b, tt) + perp * (GD.Randf() * 2f - 1f) * 8f * (1f - tt);
            DrawLine(prev, mid, new Color(c, alpha), 2.5f);
            DrawLine(prev, mid, new Color(1, 1, 1, alpha * 0.6f), 1f);
            prev = mid;
        }
    }

    private static Color RoleColor(string role) => role switch
    {
        "offense" => new Color(1f, 0.5f, 0.4f),
        "control" => new Color(0.6f, 0.6f, 1f),
        "defense" => new Color(0.4f, 0.85f, 1f),
        _ => new Color(1f, 0.85f, 0.4f),
    };
}
