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

    private enum FxKind : byte { Muzzle, Tracer, Spark, Boom, Shock, Lightning, Text, CastRing, Warp, Smoke, AquaBolt }
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
    private float _heroRecoil;   // procedural fire-recoil kick, render-only (see OnSimEvent/HeroWeaponFired)

    // Near-white: every enemy sprite is now real PDTD art with its own baked-in
    // color identity (gold, red, blue-grey, black chitin, ...) — a strong tint
    // here would multiply-darken/recolor it. Kept as a dict (not a flat
    // Color.White) so future placeholder-art enemies can still opt into a
    // Kenney-style variety tint without touching the draw call.
    private static readonly Dictionary<string, Color> EnemyTint = new()
    {
        ["skiff"] = new(1f, 1f, 1f),
        ["hauler"] = new(1f, 1f, 1f),
        ["interceptor"] = new(1f, 1f, 1f),
        ["aegis_cruiser"] = new(1f, 1f, 1f),
        ["bombard"] = new(1f, 1f, 1f),
        ["carrier"] = new(1f, 1f, 1f),
        ["phase_runner"] = new(1f, 1f, 1f),
        ["leech"] = new(1f, 1f, 1f),
        ["warden"] = new(1f, 1f, 1f),
        ["siege_crawler"] = new(1f, 1f, 1f),
    };
    private static Color Tint(string id) => EnemyTint.TryGetValue(id, out var c) ? c : new Color(1f, 1f, 1f);

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
                // e.I picks the arc's color family — waterdrop's ricochet (2) reads as a cyan
                // water-bolt trail and Ball Lightning's zap (3) as a hot yellow spark, rather
                // than the shared electric-blue lightning-chain look every other chain uses.
                Push(e.I == 2 ? FxKind.AquaBolt : FxKind.Lightning, e.Pos, e.PosB, 0f, 0.2f, e.I switch
                {
                    2 => new Color(0.55f, 0.95f, 1f),
                    3 => new Color(0.98f, 0.82f, 0.20f),
                    _ => new Color(0.6f, 0.85f, 1f),
                });
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
                _heroRecoil = Mathf.Min(1f, _heroRecoil + 0.6f);
                break;
            case SimEventKind.HeroWeaponFired:
                // procedural fire-kick on the ship itself — only for the hero's own
                // ship weapons (I 0-3: laser/missile/ion/yamato); orbital weapons
                // reuse this same event with I=10+index and fire from the planet's
                // sentinel platforms, not the ship, so they shouldn't kick it
                if (e.I < 10) _heroRecoil = Mathf.Min(1f, _heroRecoil + 0.6f);
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

        _heroRecoil = Mathf.MoveToward(_heroRecoil, 0f, 4.5f * dt);

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

    /// <summary>Stretch a PDTD beam texture from <paramref name="a"/> to <paramref name="b"/> —
    /// the texture's long axis runs along the beam, its short axis becomes the beam width.
    /// Returns false (drawing nothing) when the named texture isn't present, so callers can
    /// fall back to the old procedural line.</summary>
    private bool BeamSprite(string vfxName, Vector2 a, Vector2 b, float width, Color mod)
    {
        var tex = Art.Vfx(vfxName);
        if (tex == null) return false;
        var ts = tex.GetSize();
        Vector2 d = b - a;
        float len = d.Length();
        if (len < 1f) return false;
        DrawSetTransform(a, d.Angle(), new Vector2(len / ts.X, width / ts.Y));
        DrawTexture(tex, new Vector2(0, -ts.Y * 0.5f), mod);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        return true;
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

        // Planet Shield — a proper animated force-dome (PDTD-style), not just a thin
        // ring: a soft filled bubble, glow rings, a bright rim, and slow rotating
        // shimmer chords across the surface. Fades out as the shield depletes.
        if (World.PlanetShieldMax > 0.5f)
        {
            float shFrac = Mathf.Clamp(World.PlanetShield / World.PlanetShieldMax, 0f, 1f);
            if (shFrac > 0.003f)
            {
                float rad = pr + 14f;
                var shieldCol = new Color(0.35f, 0.8f, 1f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(World.GameTime * 3.2f);

                DrawCircle(Vector2.Zero, rad, new Color(shieldCol, (0.05f + 0.03f * pulse) * shFrac));
                DrawHexShieldSurface(rad, new Color(shieldCol, (0.30f + 0.15f * pulse) * shFrac));
                for (int i = 1; i <= 3; i++)
                    DrawArc(Vector2.Zero, rad + i * 3f, 0, Mathf.Tau, 72, new Color(shieldCol, (0.22f + 0.12f * pulse) / i * shFrac), 2.2f);
                DrawArc(Vector2.Zero, rad, 0, Mathf.Tau, 80, new Color(shieldCol, (0.75f + 0.25f * pulse) * shFrac), 3.2f);

                const int chords = 8;
                for (int i = 0; i < chords; i++)
                {
                    float a0 = World.GameTime * 0.15f + i * Mathf.Tau / chords;
                    float a1 = a0 + Mathf.Tau / chords * 0.6f;
                    DrawArc(Vector2.Zero, rad - 3f, a0, a1, 6, new Color(1f, 1f, 1f, 0.12f * shFrac), 1.3f);
                }
            }
        }

        var ic = integ > 0.5f ? new Color(0.4f, 0.95f, 0.55f)
               : integ > 0.25f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.35f, 0.3f);
        DrawArc(Vector2.Zero, pr + 5f, 0, Mathf.Tau, 72, new Color(1, 1, 1, 0.08f), 5f);
        DrawArc(Vector2.Zero, pr + 5f, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * Mathf.Clamp(integ, 0, 1), 80, ic, 5f);
    }

    /// <summary>Tiles the hex-grid shield texture (pulled from Planet Defense TD's own
    /// force-shield material — see assets/game/CREDITS.txt) across a circular polygon
    /// clipped to the dome radius, tinted by <paramref name="tint"/>. Requires the
    /// renderer's own <c>TextureRepeat</c> enabled (set once in GameRoot) so UVs past
    /// [0,1] tile instead of clamping.</summary>
    private void DrawHexShieldSurface(float rad, Color tint)
    {
        const int n = 56;
        var pts = new Vector2[n];
        var uvs = new Vector2[n];
        var cols = new Color[n];
        var tex = Sentinel.Render.Art.ShieldHex;
        float texSpan = rad * 2f / 3.2f;   // ~3 hex tiles across the dome's diameter
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.Tau / n;
            var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
            pts[i] = p;
            uvs[i] = new Vector2(p.X / texSpan + 0.5f, p.Y / texSpan + 0.5f);
            cols[i] = tint;
        }
        DrawPolygon(pts, cols, uvs, tex);
    }

    private void DrawTurrets()
    {
        var turrets = World.TurretView;
        for (int i = 0; i < turrets.Length; i++)
        {
            ref readonly var t = ref turrets[i];
            // Empty slots draw nothing — the turret build flow was removed (PDTD has no
            // ground-turret slots; the planet's defence is the missile battery plus the
            // orbital sentinel roster), so the old ring of placement markers around the
            // planet was just visual noise.
            if (!t.Built) continue;

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

    /// <summary>Per-weapon accent color, keyed by <c>OrbitalWeaponDef.Kind</c> — deliberately
    /// NOT a positional array. `data/orbital_weapons.json`'s roster has already been reordered
    /// twice in one session (a weapon added, another removed); a parallel array index-aligned
    /// to that JSON order is a standing footgun (see the v0.26.4 commit notes) every time the
    /// roster changes. Keying by kind means adding/removing/reordering weapons never touches this.</summary>
    private static Color ColorForKind(string kind) => kind switch
    {
        "beam_laser" => new(0.95f, 0.28f, 0.24f),
        "lightning" => new(0.98f, 0.82f, 0.20f),
        "rad_line" => new(0.55f, 0.92f, 0.25f),
        "shock_orb" => new(0.98f, 0.80f, 0.22f),
        "rad_zone" => new(0.55f, 0.92f, 0.25f),
        "waterdrop" => new(0.18f, 0.78f, 0.91f),
        "space_bomb" => new(0.77f, 0.31f, 0.88f),
        "force_field" => new(0.61f, 0.36f, 0.90f),
        "laser" => new(1.00f, 0.36f, 0.54f),
        _ => new(0.96f, 0.55f, 0.15f),
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

        // the orbiting weapon platforms — PDTD's own sentinel models, one per weapon kind
        // (assets/game/pdtd/icons/<kind>.png, extracted from the game's sentinel skins).
        // Falls back to the old procedural station only if a kind has no art.
        for (int i = 0; i < n; i++)
        {
            if (World.OrbitalWeaponLevel(i) <= 0) continue;
            string kind = World.OrbitalWeaponKind(i);
            var c = ColorForKind(kind);
            var p = World.OrbitalPlatformPos(i);
            float ang = p.Angle() + Mathf.Pi / 2f;
            float lvl = World.OrbitalWeaponLevel(i);
            float sc = 1.5f + Mathf.Min(0.9f, lvl * 0.06f);

            var art = Art.SentinelArt(kind);
            if (art != null)
            {
                // soft ready-glow behind the hull, then the model itself
                DrawCircle(p, 26f * sc, new Color(c, 0.10f + 0.04f * Mathf.Sin(gt * 3f + i)));
                Blit(art, p, ang, 62f * sc, Colors.White);
                continue;
            }

            Vector2 R(float x, float y) => p + new Vector2(x, y).Rotated(ang) * sc;
            DrawCircle(p, 20f * sc, new Color(c, 0.10f));
            DrawArc(p, 15f * sc, 0, Mathf.Tau, 22, new Color(c, 0.28f), 1.5f);   // station ring
            DrawColoredPolygon(new[] { R(-11, -5), R(11, -5), R(14, 4), R(0, 10), R(-14, 4) }, new Color(0.09f, 0.11f, 0.16f));
            DrawPolyline(new[] { R(-11, -5), R(11, -5), R(14, 4), R(0, 10), R(-14, 4), R(-11, -5) }, new Color(c, 0.9f), 1.8f);
            DrawLine(R(-6, -5), R(-6, -14), new Color(c, 0.8f), 1.8f);
            DrawLine(R(0, -6), R(0, -18), new Color(c, 0.95f), 2.2f);
            DrawLine(R(6, -5), R(6, -14), new Color(c, 0.8f), 1.8f);
            DrawCircle(p, 3.6f * sc * (0.8f + 0.2f * Mathf.Sin(gt * 5f + i)), new Color(c, 0.95f));
            DrawCircle(p, 1.8f, Colors.White);
        }

        // instant strike (cannon / laser / waterdrop / space bomb) — beam + a radial impact shockwave
        if (World.FxOrbitalBeamLeft > 0f)
        {
            int bk = World.FxOrbitalBeamKind;
            var c = bk switch { 1 => ColorForKind("laser"), 2 => ColorForKind("waterdrop"), 3 => ColorForKind("space_bomb"), _ => ColorForKind("") };
            float span = bk switch { 1 => 0.16f, 2 => 0.14f, 3 => 0.24f, _ => 0.12f };
            float k = Mathf.Clamp(World.FxOrbitalBeamLeft / span, 0f, 1f);
            var from = World.FxOrbitalBeamFrom;
            var to = World.FxOrbitalBeamTo;
            bool bomb = bk == 3;
            float grow = (1f - k);
            if (bk == 1)   // Laser — PDTD's own beam texture
            {
                if (!BeamSprite("beam01", from, to, 26f, new Color(c, 0.95f * k)))
                {
                    DrawLine(from, to, new Color(c, 0.32f * k), 7f);
                    DrawLine(from, to, new Color(1f, 0.96f, 0.9f, 0.9f * k), 3f);
                }
                var fl = Art.Vfx("flare");
                if (fl != null) Blit(fl, to, 0f, 54f * (0.6f + grow), new Color(c, 0.85f * k));
            }
            else if (bk == 2)   // Waterdrop — PDTD's aqua bullet streaked along its path
            {
                if (!BeamSprite("aqua_bullet", from, to, 22f, new Color(1f, 1f, 1f, 0.95f * k)))
                    DrawLine(from, to, new Color(c, 0.6f * k), 5f);
                var sp = Art.Vfx("orb_point");
                if (sp != null) Blit(sp, to, 0f, 42f * (0.7f + grow * 0.6f), new Color(c, 0.9f * k));
            }
            else if (bomb)      // Space Bomb — PDTD's explosion + shockwave ring
            {
                var ex = Art.Vfx("explosion");
                var ring = Art.Vfx("ring");
                if (ex != null) Blit(ex, to, grow * 1.5f, (40f + grow * 130f), new Color(1f, 0.92f, 0.8f, 0.95f * k));
                if (ring != null) Blit(ring, to, 0f, (30f + grow * 190f), new Color(c, 0.75f * k));
                if (ex == null && ring == null)
                {
                    DrawArc(to, (8f + grow * 46f) * 2.1f, 0, Mathf.Tau, 28, new Color(c, 0.7f * k), 4f);
                    DrawCircle(to, 10f * k, new Color(c, 0.5f * k));
                }
            }
            else
            {
                DrawLine(from, to, new Color(c, 0.32f * k), 5f);
                DrawLine(from, to, new Color(1f, 0.96f, 0.9f, 0.9f * k), 2f);
                DrawArc(to, 8f + grow * 46f, 0, Mathf.Tau, 28, new Color(c, 0.7f * k), 3f);
            }
        }

        // active field effects
        foreach (ref readonly var fx in World.OrbitalEffects)
        {
            if (fx.Kind == 1) // radiation line — PDTD Radiation Link: relay stations + a linked beam
            {
                var gc = ColorForKind("rad_line");
                int nodeN = Mathf.Max(2, fx.NodeCount);
                System.Span<Vector2> nodes = stackalloc Vector2[10];
                for (int k = 0; k < nodeN; k++) nodes[k] = World.RadLineNode(in fx, k);

                for (int seg = 0; seg < nodeN - 1; seg++)
                {
                    Vector2 a = nodes[seg], b = nodes[seg + 1];
                    if (!BeamSprite("beam03", a, b, 30f, new Color(gc, 0.95f)))
                    {
                        DrawLine(a, b, new Color(gc, 0.22f), 26f);
                        DrawLine(a, b, new Color(gc, 0.7f), 8f);
                    }
                    DrawLine(a, b, new Color(0.92f, 1f, 0.82f, 0.85f), 2.6f);
                    for (int s = 0; s < 4; s++)
                    {
                        float ph = Mathf.PosMod(gt * 0.9f + s * 0.25f, 1f);
                        DrawCircle(a.Lerp(b, ph), 3.5f, new Color(0.9f, 1f, 0.8f, 0.8f));
                    }
                }
                // the relay stations themselves — PDTD's own radiation-point sprite
                var rp = Art.Vfx("radiation_point");
                for (int k = 0; k < nodeN; k++)
                {
                    if (rp != null)
                    {
                        DrawCircle(nodes[k], 16f, new Color(gc, 0.16f));
                        Blit(rp, nodes[k], gt * 0.8f + k, 40f, new Color(gc.Lightened(0.35f), 0.95f));
                        continue;
                    }
                    DrawCircle(nodes[k], 8f, new Color(gc, 0.18f));
                    DrawArc(nodes[k], 6f, 0, Mathf.Tau, 12, new Color(gc, 0.85f), 1.8f);
                    DrawCircle(nodes[k], 2.6f, new Color(0.9f, 1f, 0.85f, 0.95f));
                    if (k == 0 || k == nodeN - 1) DrawRelayStation(nodes[k], gc);
                }
            }
            else if (fx.Kind == 4) // beam laser — PDTD Beam sentinel: continuous locked-on burn
            {
                var lc = ColorForKind("beam_laser");
                float pulse = 0.6f + 0.4f * Mathf.Sin(gt * 20f);
                if (!BeamSprite("beam05", fx.From, fx.Pos, 30f * (0.85f + 0.15f * pulse), new Color(lc, 0.95f)))
                {
                    DrawLine(fx.From, fx.Pos, new Color(lc, 0.35f), 8f);
                    DrawLine(fx.From, fx.Pos, new Color(lc, 0.8f), 3.5f);
                }
                DrawLine(fx.From, fx.Pos, new Color(1f, 0.95f, 0.9f, 0.85f), 1.4f);
                var bf = Art.Vfx("flare");
                if (bf != null) Blit(bf, fx.Pos, 0f, 46f * pulse, new Color(lc, 0.9f));
                else DrawCircle(fx.Pos, 6f * pulse, new Color(lc, 0.8f));
            }
            else if (fx.Kind == 5) // force field — PDTD Force Field: a planet-hugging damage + slow dome
            {
                var fc = ColorForKind("force_field");
                float life = Mathf.Clamp((fx.DieAt - gt), 0f, 1f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(gt * 4f);
                var shield = Art.Vfx("orb_shield");
                if (shield != null)
                    Blit(shield, Vector2.Zero, gt * 0.25f, fx.Radius * 2.1f, new Color(fc, (0.55f + 0.2f * pulse) * life + 0.25f));
                DrawCircle(Vector2.Zero, fx.Radius, new Color(fc, (0.06f + 0.03f * pulse) * life + 0.02f));
                DrawArc(Vector2.Zero, fx.Radius, 0, Mathf.Tau, 64, new Color(fc, 0.55f + 0.2f * pulse), 2.4f);
                for (int s = 0; s < 10; s++)
                {
                    float aa = s * Mathf.Tau / 10f - gt * 0.6f;
                    var p2 = Vector2.FromAngle(aa) * fx.Radius;
                    DrawLine(p2, p2 - Vector2.FromAngle(aa) * 14f, new Color(fc, 0.7f), 2f);
                }
            }
            else if (fx.Kind == 6) // laser burn zone — a static scorched, sparking patch at the impact point
            {
                var lzc = ColorForKind("laser");
                float life = Mathf.Clamp((fx.DieAt - gt), 0f, 1f);
                DrawCircle(fx.Pos, fx.Radius, new Color(lzc, (0.10f + 0.08f * Mathf.Sin(gt * 9f)) * life + 0.03f));
                DrawArc(fx.Pos, fx.Radius, 0, Mathf.Tau, 24, new Color(lzc, 0.5f * life), 2f);
                for (int s = 0; s < 5; s++)
                {
                    float aa = s * Mathf.Tau / 5f + gt * 3f;
                    var e = fx.Pos + Vector2.FromAngle(aa) * fx.Radius * (0.3f + 0.5f * Mathf.Sin(gt * 6f + s * 1.7f));
                    DrawLine(fx.Pos, e, new Color(1f, 0.85f, 0.75f, 0.45f * life), 1.4f);
                }
            }
            else // shock orb (2) / radiation zone (3) — concentric radial shockwaves (PDTD SHOCK ORB look)
            {
                var col = fx.Kind == 2 ? ColorForKind("shock_orb") : ColorForKind("rad_zone");
                DrawCircle(fx.Pos, fx.Radius, new Color(col, 0.07f));
                for (int ring = 0; ring < 4; ring++)
                {
                    float ph = Mathf.PosMod(gt * (fx.Kind == 2 ? 1.6f : 0.9f) + ring * 0.25f, 1f);
                    DrawArc(fx.Pos, fx.Radius * ph, 0, Mathf.Tau, 44, new Color(col, (1f - ph) * (fx.Kind == 2 ? 0.7f : 0.4f)), fx.Kind == 2 ? 3f : 2f);
                }
                DrawArc(fx.Pos, fx.Radius, 0, Mathf.Tau, 44, new Color(col, 0.45f), 2f);
                if (fx.Kind == 2)
                {
                    // PDTD's Ball Lightning: a glowing energy core throwing real lightning arcs
                    var core = Art.Vfx("energyball");
                    var arc = Art.Vfx("lightning_arc");
                    if (core != null) Blit(core, fx.Pos, gt * 1.7f, 54f + 8f * Mathf.Sin(gt * 9f), new Color(col.Lightened(0.3f), 0.95f));
                    else { DrawCircle(fx.Pos, 8f, new Color(col, 0.95f)); DrawCircle(fx.Pos, 4f, Colors.White); }
                    for (int s = 0; s < 6; s++)
                    {
                        float aa = gt * 11f + s * Mathf.Tau / 6f;
                        var e = fx.Pos + Vector2.FromAngle(aa) * fx.Radius * (0.6f + 0.35f * Mathf.Sin(gt * 7f + s));
                        if (arc == null || !BeamSprite("lightning_arc", fx.Pos, e, 16f, new Color(col, 0.55f)))
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

    /// <summary>A tiny relay-pylon silhouette (hex base + outward mast + tip light) at
    /// each end of the Radiation Line chain — distinguishes the two terminal stations
    /// from the plainer beacon dots at any middle link nodes.</summary>
    private void DrawRelayStation(Vector2 p, Color gc)
    {
        float ang = p.Angle();
        Vector2 outward = Vector2.FromAngle(ang);
        Vector2 side = outward.Rotated(Mathf.Pi / 2f);

        var basePts = new Vector2[6];
        for (int i = 0; i < 6; i++) basePts[i] = p + Vector2.FromAngle(ang + i * Mathf.Tau / 6f) * 9f;
        DrawColoredPolygon(basePts, new Color(0.08f, 0.09f, 0.12f, 0.92f));
        var loop = new Vector2[7];
        basePts.CopyTo(loop, 0); loop[6] = basePts[0];
        DrawPolyline(loop, new Color(gc, 0.8f), 1.6f);

        Vector2 tip = p + outward * 16f;
        DrawLine(p, tip, new Color(gc, 0.9f), 2.2f);
        DrawLine(tip - side * 4f, tip + side * 4f, new Color(gc, 0.85f), 2f);
        DrawCircle(tip, 2.4f, new Color(1f, 1f, 0.9f, 0.95f));
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
            var tint = boss ? new Color(1f, 1f, 1f) : Tint(def.Id);
            var tex = Art.Enemy(boss ? "boss_threshing_gate" : def.Id);

            // Sprites are drawn well wider than the sim radius on purpose (PDTD's art is
            // generous the same way); bumped 2026-09-16 alongside the camera zoom-out so
            // enemies read bigger on screen, not merely the same size in a wider view.
            float sizePx = (boss ? e.Radius * 3.6f : e.Radius * 4.3f) + 12f;
            float rot = e.Vel.LengthSquared() > 1f ? e.Vel.Angle() + Mathf.Pi / 2f : e.Pos.Angle() + Mathf.Pi / 2f;

            // procedural idle motion — a slow lateral wobble (phase seeded off the
            // enemy's own slot so a field of the same enemy type doesn't move in
            // lockstep) plus a faint thrust trail scaled by actual speed. Render-only:
            // offsets only where the sprite is drawn, never e.Pos itself, so hp bars/
            // shield rings/aura circles below stay exactly where the sim says they are.
            float bobAmp = boss ? 1.2f : Mathf.Clamp(5f - e.Radius * 0.12f, 1.4f, 4f);
            float bobSpeed = 1.7f + (i % 7) * 0.31f;
            float bobPhase = i * 2.3f;
            Vector2 drawPos = e.Pos + Vector2.FromAngle(rot + Mathf.Pi / 2f) * (Mathf.Sin(World.GameTime * bobSpeed + bobPhase) * bobAmp);

            if (!boss && e.Vel.LengthSquared() > 100f)
            {
                Vector2 back = -e.Vel.Normalized();
                float trailLen = Mathf.Clamp(e.Vel.Length() * 0.14f, 5f, 20f);
                for (int t = 1; t <= 3; t++)
                {
                    float ft = t / 3f;
                    DrawCircle(drawPos + back * (trailLen * ft), 2.4f * (1f - ft) + 0.5f, new Color(tint.Lightened(0.35f), 0.3f * (1f - ft)));
                }
            }

            // No soft backing circle behind the sprite — it read as a plain white/grey
            // halo now that every enemy tint is near-white (real PDTD sprites carry their
            // own baked-in color, see EnemyTint above), and PDTD itself has no such halo.
            Blit(tex, drawPos, boss && e.MechanicActive ? 0f : rot, sizePx,
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

            // No per-enemy health ring — matches PDTD's own look (no floating hp bars on
            // regular enemies; damage reads through hit sparks/kill bursts instead). The
            // boss's own top-of-screen bar (Hud.cs's _bossBar) is unrelated and unaffected.
            //
            // Mini-bosses are the exception: PDTD's multi-bar elites carry a stack of
            // segments over the model, and that's the read the player needs to judge
            // whether one is nearly down. Segments empty right to left.
            if (def.HpSegments > 1) DrawSegmentedHpBar(in e, def, sizePx);
        }
    }

    /// <summary>The multi-bar health readout above a mini-boss. `HpSegments` bars, each
    /// worth 1/N of the hull, draining right to left; the one currently taking damage
    /// shows a partial fill.</summary>
    private void DrawSegmentedHpBar(in Sentinel.Sim.Enemy e, Config.EnemyDef def, float sizePx)
    {
        int segs = Mathf.Clamp(def.HpSegments, 2, 10);
        float frac = e.MaxHp > 0f ? Mathf.Clamp(e.Hp / e.MaxHp, 0f, 1f) : 0f;

        const float segH = 7f, gap = 2f;
        float totalW = Mathf.Max(64f, sizePx * 0.95f);
        float segW = (totalW - gap * (segs - 1)) / segs;
        float x0 = e.Pos.X - totalW * 0.5f;
        float y = e.Pos.Y - sizePx * 0.5f - 16f;

        // how much of the whole bar each segment holds, in "segments" units
        float filled = frac * segs;
        var full = new Color(1f, 0.42f, 0.30f);
        var empty = new Color(1f, 1f, 1f, 0.14f);

        for (int k = 0; k < segs; k++)
        {
            var box = new Rect2(x0 + k * (segW + gap), y, segW, segH);
            DrawRect(box, empty);
            float f = Mathf.Clamp(filled - k, 0f, 1f);
            if (f > 0f) DrawRect(new Rect2(box.Position, new Vector2(segW * f, segH)), full);
            DrawRect(box, new Color(0, 0, 0, 0.55f), filled: false, width: 1f);
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

        // procedural fire-recoil kick (see OnSimEvent/HeroWeaponFired) — a quick
        // punch backward along the ship's own facing, plus a matching brief flare
        // on the engine trail, so firing reads as a physical event on the hull
        // itself and not just the weapon's own VFX. Purely a draw-position offset —
        // never touches h.Pos, so aim reticles/auras/FX anchored to it stay exact.
        Vector2 recoilOffset = Vector2.FromAngle(heading + Mathf.Pi) * (_heroRecoil * 5f);
        float thrustDraw = Mathf.Clamp(speedFrac + _heroRecoil * 0.5f, 0f, 1f);
        DrawShip(h.Pos + recoilOffset, heading, 1f, thrustDraw, gt);

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

        // --- ship laser: one locked, high-power beam held on a target (PDTD's ship laser) ---
        if (World.HeroBeamLeft > 0f && World.HeroBeamTargetPos is Vector2 beamTo)
        {
            var lc = new Color(0.95f, 0.22f, 0.20f);
            float pulse = 0.85f + 0.15f * Mathf.Sin(World.GameTime * 22f);
            if (!BeamSprite("beam01", h.Pos, beamTo, 38f * pulse, new Color(lc, 0.95f)))
            {
                DrawLine(h.Pos, beamTo, new Color(lc, 0.35f), 10f);
                DrawLine(h.Pos, beamTo, new Color(1f, 0.85f, 0.8f, 0.9f), 3.5f);
            }
            DrawLine(h.Pos, beamTo, new Color(1f, 0.95f, 0.92f, 0.9f), 2f);
            var lf = Art.Vfx("flare");
            if (lf != null)
            {
                Blit(lf, beamTo, 0f, 62f * pulse, new Color(lc.Lightened(0.3f), 0.9f));
                Blit(lf, h.Pos, 0f, 34f * pulse, new Color(lc.Lightened(0.4f), 0.8f));
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

    /// <summary>The player's ship — an angular strike-corvette: a narrow spear-nosed
    /// spine hull, swept delta wings with glowing tip pods, twin rear engine
    /// nacelles held clear of the hull on struts, and a raised glass-canopy
    /// cockpit. Beyond's teal/magenta palette, drawn from polygons. Nose points
    /// along <paramref name="ang"/> (local +X).</summary>
    private void DrawShip(Vector2 c, float ang, float scale, float thrust, float gt)
    {
        Vector2 P(float x, float y) => c + new Vector2(x, y).Rotated(ang) * scale;

        var hull     = new Color(0.12f, 0.14f, 0.19f);
        var hullDark = new Color(0.08f, 0.09f, 0.13f);
        var hullLit  = new Color(0.19f, 0.24f, 0.33f);
        var edge     = new Color(0.32f, 0.74f, 0.98f);
        var mag      = new Color(0.86f, 0.30f, 0.62f);
        var glow     = new Color(0.40f, 0.86f, 1f);

        // nacelle anchor points — held out on struts behind the wings, clear of
        // the spine, so the silhouette reads as twin engines rather than one slab
        Vector2 NacL(float x, float y) => new(x, y);
        var nacPos = new[] { NacL(-30, -17f), NacL(-30, 17f) };

        // ---- engine trail + nozzle wash (twin nacelles) ----
        if (thrust > 0.04f)
        {
            float tl = 28f + thrust * 50f;
            foreach (var n in nacPos)
            {
                DrawLine(P(n.X, n.Y), P(n.X - tl, n.Y), new Color(glow, 0.28f * thrust), 4.2f);
                DrawLine(P(n.X, n.Y), P(n.X - tl * 1.3f, n.Y), new Color(1f, 1f, 1f, 0.2f * thrust), 1.8f);
            }
        }

        // ---- struts anchoring the nacelles to the hull ----
        foreach (var n in nacPos)
            DrawLine(P(-10, n.Y * 0.55f), P(n.X + 6f, n.Y), new Color(edge, 0.4f), 2.2f);

        // ---- swept delta wings ----
        foreach (int s in new[] { -1, 1 })
        {
            Vector2[] wing = { P(8, s * 6f), P(-6, s * 32f), P(-20, s * 30f), P(-14, s * 9f) };
            DrawColoredPolygon(wing, hullDark);
            DrawPolyline(new[] { wing[0], wing[1], wing[2], wing[3], wing[0] }, new Color(edge, 0.6f), 1.4f);
            // wingtip weapon/marker pod
            DrawCircle(P(-8, s * 30f), 2.6f, new Color(mag, 0.85f));
            DrawLine(P(2, s * 8f), P(-4, s * 30f), new Color(mag, 0.35f), 1.2f);   // leading-edge accent
        }

        // ---- twin engine nacelles ----
        foreach (var n in nacPos)
        {
            Vector2[] nac = { P(n.X + 8f, n.Y - 4.5f), P(n.X - 10f, n.Y - 5f), P(n.X - 14f, n.Y), P(n.X - 10f, n.Y + 5f), P(n.X + 8f, n.Y + 4.5f) };
            DrawColoredPolygon(nac, hullDark);
            DrawPolyline(new[] { nac[0], nac[1], nac[2], nac[3], nac[4], nac[0] }, new Color(edge, 0.55f), 1.2f);
        }

        // ---- main spine hull (narrow, tapered) ----
        Vector2[] body =
        {
            P(46, 0f), P(30, -6.5f), P(4, -8f), P(-12, -7f),
            P(-16, 0f),
            P(-12, 7f), P(4, 8f), P(30, 6.5f),
        };
        DrawColoredPolygon(body, hull);
        // lit dorsal panel
        DrawColoredPolygon(new[] { P(40, -2f), P(26, -5.5f), P(0, -6.5f), P(-10, -5.5f), P(-8, -1f), P(14, -1.5f) }, hullLit);
        DrawPolyline(new[]
        {
            body[0], body[1], body[2], body[3], body[4], body[5], body[6], body[7], body[0],
        }, new Color(edge, 0.9f), 1.8f);
        // spine plating seams / greebles
        DrawLine(P(24, -6f), P(24, 6f), new Color(edge, 0.22f), 1f);
        DrawLine(P(8, -7.5f), P(8, 7.5f), new Color(edge, 0.22f), 1f);
        DrawRect(new Rect2(P(-2, -3f), new Vector2(8, 6)), new Color(hullDark, 0.8f));
        // magenta spine stripe
        DrawLine(P(40, -1.5f), P(-10, -1.8f), new Color(mag, 0.6f), 1.6f);

        // ---- spear nose + twin flanking cannons ----
        DrawColoredPolygon(new[] { P(30, -6.5f), P(48, -1f), P(52, 0f), P(48, 1f), P(30, 6.5f) }, hullDark);
        DrawLine(P(44, -3.2f), P(58, -3.2f), new Color(0.85f, 0.78f, 0.35f), 2.2f);   // upper cannon
        DrawLine(P(44, 3.2f), P(58, 3.2f), new Color(0.85f, 0.78f, 0.35f), 2.2f);     // lower cannon
        DrawLine(P(38, 0f), P(52, 0f), new Color(1f, 0.9f, 0.55f), 3f);               // central spike / main gun
        DrawCircle(P(52, 0f), 2.2f, new Color(1f, 0.9f, 0.5f, 0.85f));

        // ---- raised glass-canopy cockpit (forward-mid, offset above the spine) ----
        Vector2[] canopy = { P(20, -4f), P(28, -2f), P(27, 3f), P(19, 4f) };
        DrawColoredPolygon(canopy, hullLit);
        DrawPolyline(new[] { canopy[0], canopy[1], canopy[2], canopy[3], canopy[0] }, edge, 1.4f);
        DrawColoredPolygon(new[] { P(23, -1.5f), P(27, 0.5f), P(23, 2.5f) }, new Color(0.7f, 0.95f, 1f, 0.9f));

        // ---- nacelle nozzle glow ----
        float pulse = 0.72f + 0.28f * Mathf.Sin(gt * 16f);
        foreach (var n in nacPos)
        {
            DrawCircle(P(n.X - 13f, n.Y), 4.2f * (0.7f + 0.3f * pulse), new Color(glow, 0.85f));
            DrawCircle(P(n.X - 13f, n.Y), 1.9f, Colors.White);
        }

        // ---- running lights (wingtips) ----
        float blink = Mathf.Sin(gt * 3f) > 0f ? 1f : 0.25f;
        DrawCircle(P(-8, -30f), 1.7f, new Color(1f, 0.35f, 0.35f, blink));
        DrawCircle(P(-8, 30f), 1.7f, new Color(0.35f, 1f, 0.45f, blink));
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
                    // real PDTD lightning-arch texture stretched along the arc, with the old
                    // procedural bolt as the fallback
                    if (!BeamSprite("lightning_arc", f.A, f.B, 26f * (1f - k * 0.4f), new Color(f.Col, 1f - k)))
                        DrawLightning(f.A, f.B, f.Col, 1f - k);
                    break;
                case FxKind.AquaBolt:
                    // Waterdrop ricochet leg — PDTD's own aqua bullet streak
                    if (!BeamSprite("aqua_bullet", f.A, f.B, 20f, new Color(f.Col, 1f - k)))
                        DrawLine(f.A, f.B, new Color(f.Col, 1f - k), 3f);
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
