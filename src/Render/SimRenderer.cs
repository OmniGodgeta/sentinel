using System.Collections.Generic;
using Godot;
using Sentinel.Sim;
using Sentinel.Game;

namespace Sentinel.Render;

/// <summary>
/// Greybox immediate-mode renderer. Reads sim state, draws shapes. No art yet —
/// this exists so the loop can be played and judged at 1x–4x. The MultiMesh /
/// sprite pass replaces the enemy and projectile loops later.
/// </summary>
public sealed partial class SimRenderer : Node2D
{
    public GameRoot Root = null!;
    public SimWorld World = null!;
    public int SelectedSlot = -1;

    private struct Boom { public Vector2 Pos; public float R; public float Age; public float Life; }
    private readonly List<Boom> _booms = new(64);

    private Vector2 _shakeOffset;
    private float _shake;

    public void AddBoom(Vector2 worldPos, float radius)
        => _booms.Add(new Boom { Pos = worldPos, R = radius, Age = 0f, Life = 0.35f });

    public void AddShake(float amount) => _shake = Mathf.Min(10f, _shake + amount);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = _booms.Count - 1; i >= 0; i--)
        {
            var b = _booms[i];
            b.Age += dt;
            if (b.Age >= b.Life) _booms.RemoveAt(i);
            else _booms[i] = b;
        }

        if (_shake > 0.01f)
        {
            _shakeOffset = new Vector2(
                (GD.Randf() * 2f - 1f) * _shake,
                (GD.Randf() * 2f - 1f) * _shake);
            _shake = Mathf.MoveToward(_shake, 0f, 40f * dt);
        }
        else _shakeOffset = Vector2.Zero;

        Position = Root.WorldOrigin + _shakeOffset;
    }

    public override void _Draw()
    {
        var b = World.B;

        // --- orbit / ring guides ---
        DrawArc(Vector2.Zero, b.SpawnRadius, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.04f), 2f);
        DrawArc(Vector2.Zero, b.HeroOrbitMin, 0, Mathf.Tau, 48, new Color(0.4f, 0.7f, 1f, 0.05f), 1.5f);
        DrawArc(Vector2.Zero, b.HeroOrbitMax, 0, Mathf.Tau, 48, new Color(0.4f, 0.7f, 1f, 0.05f), 1.5f);

        // --- planet ---
        float integ = World.PlanetIntegrityMax > 0 ? World.PlanetIntegrity / World.PlanetIntegrityMax : 0f;
        DrawCircle(Vector2.Zero, b.PlanetRadius, new Color(0.12f, 0.16f, 0.24f));
        DrawArc(Vector2.Zero, b.PlanetRadius, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * integ,
                80, IntegrityColor(integ), 5f);

        // --- barrage sectors / barrier (active ability VFX) ---
        DrawAbilityVfx();

        // --- turret slots ---
        var turrets = World.TurretView;
        for (int i = 0; i < turrets.Length; i++)
        {
            ref readonly var t = ref turrets[i];
            bool sel = i == SelectedSlot;
            if (!t.Built)
            {
                DrawArc(t.Pos, 12f, 0, Mathf.Tau, 16,
                        sel ? new Color(1f, 0.9f, 0.4f, 0.9f) : new Color(1, 1, 1, 0.16f), sel ? 2.5f : 1.5f);
                continue;
            }
            var def = World.TurretDefs[t.DefIndex];
            var col = Color.FromHsv(def.Color, 0.5f, 0.95f);

            // firing arc (faint wedge)
            float half = Mathf.DegToRad(def.ArcDegrees) * 0.5f;
            DrawArc(t.Pos, def.Range, t.Angle - half, t.Angle + half, 24, new Color(col, 0.06f), 1.5f);
            DrawLine(t.Pos, t.Pos + Vector2.FromAngle(t.Angle - half) * def.Range, new Color(col, 0.10f), 1f);
            DrawLine(t.Pos, t.Pos + Vector2.FromAngle(t.Angle + half) * def.Range, new Color(col, 0.10f), 1f);

            DrawCircle(t.Pos, 11f, col);
            DrawLine(t.Pos, t.Pos + Vector2.FromAngle(t.Angle) * 20f, Colors.White, 2.5f);
            if (sel) DrawArc(t.Pos, 16f, 0, Mathf.Tau, 20, new Color(1f, 0.9f, 0.4f), 2.5f);
        }

        // --- enemies ---
        var enemies = World.EnemyView;
        for (int i = 0; i < enemies.Length; i++)
        {
            ref readonly var e = ref enemies[i];
            if (!e.Alive) continue;
            var def = World.EnemyDefAt(e.DefIndex);
            var col = Color.FromHsv(def.Color, 0.65f, 0.95f);
            DrawCircle(e.Pos, e.Radius, col);
            DrawArc(e.Pos, e.Radius, 0, Mathf.Tau, 12, new Color(0, 0, 0, 0.5f), 1.2f);
            // hp bar
            float hpf = e.MaxHp > 0 ? e.Hp / e.MaxHp : 0f;
            if (hpf < 0.999f)
            {
                Vector2 bp = e.Pos + new Vector2(-e.Radius, -e.Radius - 6f);
                DrawRect(new Rect2(bp, new Vector2(e.Radius * 2f, 3f)), new Color(0, 0, 0, 0.6f));
                DrawRect(new Rect2(bp, new Vector2(e.Radius * 2f * hpf, 3f)), new Color(0.4f, 1f, 0.4f));
            }
        }

        // --- projectiles ---
        var projs = World.ProjectileView;
        for (int i = 0; i < projs.Length; i++)
        {
            ref readonly var p = ref projs[i];
            if (!p.Alive) continue;
            if (p.Kind == 1)
            {
                DrawCircle(p.Pos, 4f, new Color(1f, 0.8f, 0.5f));
                DrawLine(p.Pos, p.Pos - p.Vel.Normalized() * 14f, new Color(1f, 0.6f, 0.3f, 0.5f), 2f);
            }
            else
            {
                DrawLine(p.Pos, p.Pos - p.Vel.Normalized() * 10f, new Color(0.8f, 0.95f, 1f), 2f);
            }
        }

        // --- hero ---
        DrawHero();

        // --- booms ---
        foreach (var bo in _booms)
        {
            float k = bo.Age / bo.Life;
            DrawArc(bo.Pos, bo.R + k * bo.R * 2f, 0, Mathf.Tau, 20,
                    new Color(1f, 0.7f, 0.3f, 1f - k), 3f * (1f - k));
        }
    }

    private void DrawHero()
    {
        var h = World.HeroView;
        var col = h.Alive ? new Color(0.5f, 0.85f, 1f) : new Color(0.5f, 0.5f, 0.55f, 0.5f);
        float ang = h.Pos.Angle() + Mathf.Pi / 2f;
        Vector2[] tri =
        {
            h.Pos + Vector2.FromAngle(ang - Mathf.Pi / 2f) * 16f,
            h.Pos + Vector2.FromAngle(ang + Mathf.Pi * 0.75f) * 12f,
            h.Pos + Vector2.FromAngle(ang - Mathf.Pi * 0.75f) * 12f,
        };
        DrawColoredPolygon(tri, col);

        if (h.Alive)
        {
            DrawArc(h.Pos, World.Cfg.Hero.PointDefenseRange, 0, Mathf.Tau, 32, new Color(0.5f, 0.85f, 1f, 0.05f), 1.2f);
            // hull bar
            float hf = h.MaxHull > 0 ? h.Hull / h.MaxHull : 0f;
            Vector2 bp = h.Pos + new Vector2(-18, -24);
            DrawRect(new Rect2(bp, new Vector2(36, 3)), new Color(0, 0, 0, 0.6f));
            DrawRect(new Rect2(bp, new Vector2(36 * hf, 3)), new Color(0.5f, 0.85f, 1f));
            if (h.OverdriveLeft > 0f)
                DrawArc(h.Pos, 22f, 0, Mathf.Tau, 20, new Color(1f, 0.5f, 0.2f), 2f);
        }
        else
        {
            // respawn pip near planet
            DrawString(ThemeDB.FallbackFont, new Vector2(-24, -World.B.PlanetRadius - 10),
                       $"RESPAWN {Mathf.CeilToInt(h.RespawnLeft)}", HorizontalAlignment.Center, -1, 16,
                       new Color(1f, 0.5f, 0.5f));
        }
    }

    private void DrawAbilityVfx()
    {
        var ab = World.AbilityView;
        for (int i = 0; i < ab.Length; i++)
        {
            ref readonly var a = ref ab[i];
            if (a.DefIndex < 0 || a.ActiveLeft <= 0f) continue;
            var def = World.AbilityDefs[a.DefIndex];
            if (def.Kind == "barrage")
            {
                float c = a.P0, half = a.P1;
                var pts = new List<Vector2> { Vector2.Zero };
                for (float t = -half; t <= half; t += half / 8f + 0.001f)
                    pts.Add(Vector2.FromAngle(c + t) * World.B.SpawnRadius);
                DrawColoredPolygon(pts.ToArray(), new Color(1f, 0.85f, 0.3f, 0.10f));
            }
            else if (def.Kind == "barrier" && a.P2 > 0f)
            {
                DrawArc(Vector2.Zero, World.B.PlanetRadius + 14f, 0, Mathf.Tau, 64,
                        new Color(0.4f, 0.8f, 1f, 0.5f), 4f);
            }
        }
    }

    private static Color IntegrityColor(float f) =>
        f > 0.5f ? new Color(0.4f, 0.9f, 0.5f) :
        f > 0.25f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.35f, 0.3f);
}
