using Godot;

namespace Sentinel.Sim;

/// <summary>
/// The planet's orbital weapons — PDTD-style "sentinels" that orbit the planet and
/// auto-fire. Each has a per-run level that the in-fight card draft raises (later
/// levels also come from out-of-battle meta). Deterministic: platform positions
/// are a pure function of GameTime, every roll comes off <see cref="Rng"/>.
/// </summary>
public sealed partial class SimWorld
{
    private int[] _owLevel = System.Array.Empty<int>();
    private float[] _owCd = System.Array.Empty<float>();

    private const float OwOrbit = 2.45f;   // × SentinelOrbitRadius — a clear orbit ring in open space

    // active timed field effects (radiation line / shock orb / radiation zone / beam laser /
    // force field / laser burn zone)
    public struct OwEffect
    {
        public int Kind;        // 1 rad_line, 2 shock_orb, 3 rad_zone, 4 beam_laser, 5 force_field, 6 laser zone
        public float DieAt;     // GameTime
        public float Tick;      // dps accumulator
        public float Dps;
        public float Radius;
        public float Stun;
        public float Slow;      // force_field: 1 = normal speed, <1 = slowed while inside
        public float P0;        // rad_line: fixed bearing; shock_orb/rad_zone: seed angle
        public Vector2 Pos;     // shock_orb / rad_zone centre; beam_laser: current beam endpoint
        public Vector2 From;    // beam_laser: the platform's current position (it keeps orbiting)
        public EnemyHandle Target;   // beam_laser: locked target
        public int WeaponIndex;      // beam_laser: which platform this beam is anchored to
        public int NodeCount;        // rad_line: how many linked relay stations (2-10; 2 = a single link)
        public float Spin;           // rad_line: angular speed the whole link chain orbits at
        public float StartTime;      // rad_line: GameTime this effect was cast
    }

    /// <summary>× DespawnRadius — the radius the relay stations orbit at.
    /// Was 0.62 (r≈558) which is why the weapon read as doing no damage at all: that far
    /// out enemies are still fanned across the full 360°, so a base two-node link — a
    /// single 64° chord — sat in front of maybe a sixth of them, and each one crossed its
    /// 24px width in a fraction of a second. Pulled in to the convergence zone just
    /// outside the planet, where every attacker has to funnel through regardless of the
    /// bearing it spawned on, which is where PDTD puts its Radiation Link too.</summary>
    private const float RadLineRing = 0.26f;
    /// <summary>Half-width of the damaging beam between two relay stations.</summary>
    private const float RadLineBeamHalfWidth = 30f;

    /// <summary>The arc (degrees) the whole relay chain spans for a given node count — PDTD's
    /// Radiation Link levels up by extending/connecting more links until they nearly ring the
    /// planet, not just adding flat damage. Grows from a modest 64° at the base 2-node link up
    /// to 340° (deliberately short of a full 360° loop, so it still reads as a chain with two
    /// ends rather than a seamless ring) as node count climbs toward its max.</summary>
    private static float RadLineSpreadDeg(int nodes) => Mathf.Lerp(64f, 340f, Mathf.Clamp((nodes - 2) / 8f, 0f, 1f));

    /// <summary>Position of Radiation Line relay station <paramref name="k"/> (of
    /// <see cref="OwEffect.NodeCount"/>) right now — shared by the sim and the
    /// renderer so the drawn chain always matches what's actually dealing damage.</summary>
    public Vector2 RadLineNode(in OwEffect fx, int k)
    {
        int n = Mathf.Max(2, fx.NodeCount);
        float spread = Mathf.DegToRad(RadLineSpreadDeg(n));
        float baseAngle = fx.P0 + fx.Spin * (GameTime - fx.StartTime);
        float a = baseAngle + (n == 1 ? 0f : -spread * 0.5f + spread * k / (n - 1));
        return Vector2.FromAngle(a) * (B.DespawnRadius * RadLineRing);
    }
    private readonly System.Collections.Generic.List<OwEffect> _owEffects = new();

    private float _fxOwBeamLeft; private int _fxOwBeamKind; private Vector2 _fxOwBeamFrom, _fxOwBeamTo;

    // ---- views ----
    public int OrbitalWeaponCount => _owLevel.Length;
    public int OrbitalWeaponLevel(int i) => (uint)i < (uint)_owLevel.Length ? _owLevel[i] : 0;
    /// <summary>The weapon's <c>Kind</c> string (e.g. "beam_laser", "waterdrop") — lets the
    /// renderer pick per-weapon color/art by kind instead of by array position, so the roster
    /// can be reordered/added-to/removed-from without a parallel index-aligned array drifting.</summary>
    public string OrbitalWeaponKind(int i) => (uint)i < (uint)Cfg.OrbitalWeapons.Count ? Cfg.OrbitalWeapons[i].Kind : "";
    public float OrbitalWeaponCooldownLeft(int i) => (uint)i < (uint)_owCd.Length ? _owCd[i] : 0f;
    public Vector2 OrbitalPlatformPos(int i)
    {
        float a = GameTime * 0.32f + i * Mathf.Tau / Mathf.Max(1, _owLevel.Length);
        return Vector2.FromAngle(a) * (B.SentinelOrbitRadius * OwOrbit);
    }
    public System.ReadOnlySpan<OwEffect> OrbitalEffects => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_owEffects);
    public float FxOrbitalBeamLeft => _fxOwBeamLeft;
    public int FxOrbitalBeamKind => _fxOwBeamKind;
    public Vector2 FxOrbitalBeamFrom => _fxOwBeamFrom;
    public Vector2 FxOrbitalBeamTo => _fxOwBeamTo;

    private void ResetOrbitalWeapons()
    {
        int n = Cfg.OrbitalWeapons.Count;
        _owLevel = new int[n];
        _owCd = new float[n];
        _owEffects.Clear();
        _fxOwBeamLeft = 0f;

        // meta head-start: an orbital weapon bought to a persistent level starts there
        for (int i = 0; i < n; i++)
            _owLevel[i] = Mathf.Clamp(Mods.OrbitalMetaLevel(Cfg.OrbitalWeapons[i].Id), 0, Cfg.OrbitalWeapons[i].MaxLevel);
    }

    private void LevelUpOrbitalWeapon(int i)
    {
        if ((uint)i >= (uint)_owLevel.Length) return;
        int max = Mathf.Max(1, Cfg.OrbitalWeapons[i].MaxLevel);
        if (_owLevel[i] < max) _owLevel[i]++;
    }

    private void StepOrbitalWeapons(float dt)
    {
        if (_fxOwBeamLeft > 0f) _fxOwBeamLeft -= dt;

        for (int i = 0; i < _owCd.Length; i++) if (_owCd[i] > 0f) _owCd[i] -= dt;

        // --- active field effects ---
        for (int k = _owEffects.Count - 1; k >= 0; k--)
        {
            var fx = _owEffects[k];
            if (GameTime >= fx.DieAt) { _owEffects.RemoveAt(k); continue; }
            StepOwEffect(ref fx, dt);
            _owEffects[k] = fx;
        }

        if (!_heroAutoFire) return;   // the AUTO toggle gates orbital fire too

        for (int i = 0; i < _owLevel.Length; i++)
        {
            if (_owLevel[i] <= 0 || _owCd[i] > 0f) continue;
            var d = Cfg.OrbitalWeapons[i];
            if (ClosestEnemyTo(Vector2.Zero, B.DespawnRadius) < 0) continue;
            FireOrbitalWeapon(i);
        }
    }

    private void FireOrbitalWeapon(int i)
    {
        var d = Cfg.OrbitalWeapons[i];
        int L = _owLevel[i];
        // chip bonuses (Mods.OrbitalWeaponDamageMult/RadiusMult) apply uniformly to every
        // orbital weapon — see ModifierSet.cs's "orbital weapons: chip bonuses" section
        float dmg = (d.Damage + d.DamagePerLevel * (L - 1)) * Mathf.Max(0.2f, Mods.OrbitalWeaponDamageMult);
        int cnt = Mathf.Max(1, d.Count + Mathf.FloorToInt(d.CountPerLevel * (L - 1)));
        float radius = (d.Radius + d.RadiusPerLevel * (L - 1)) * Mathf.Max(0.2f, Mods.OrbitalWeaponRadiusMult);
        float dur = d.Duration + d.DurationPerLevel * (L - 1);
        Vector2 from = OrbitalPlatformPos(i);

        switch (d.Kind)
        {
            case "beam_laser":
            {
                // PDTD's Beam sentinel — locks on and burns continuously for the duration,
                // instead of an instant zap
                int t = ClosestEnemyTo(from, d.Range);
                if (t < 0) break;
                _owEffects.Add(new OwEffect
                {
                    Kind = 4, DieAt = GameTime + dur, Dps = dmg, WeaponIndex = i,
                    Target = HandleOf(t), From = from, Pos = Enemies[t].Pos,
                });
                break;
            }
            case "lightning":
            {
                int cur = ClosestEnemyTo(from, d.Range);
                if (cur < 0) break;
                Vector2 node = from;
                System.Span<bool> hit = stackalloc bool[96];
                for (int j = 0; j < cnt && cur >= 0; j++)
                {
                    ref var en = ref Enemies[cur];
                    DamageEnemy(cur, dmg, DamageSource.Orbital);
                    if (en.Alive && d.StunSeconds > 0f && !_missionEnemyDefs[en.DefIndex].CcImmune)
                        en.StunLeft = Mathf.Max(en.StunLeft, d.StunSeconds);
                    if (cur < 96) hit[cur] = true;
                    Events.PushLine(SimEventKind.ChainArc, node, en.Pos, 3f, 1);
                    node = en.Pos;
                    int next = -1; float best = radius * radius;
                    for (int e = 0; e < EnemyHighWater; e++)
                    {
                        if (!Enemies[e].Alive || (e < 96 && hit[e])) continue;
                        float dd = Enemies[e].Pos.DistanceSquaredTo(node);
                        if (dd < best) { best = dd; next = e; }
                    }
                    cur = next;
                }
                break;
            }
            case "rad_line":
            {
                // PDTD's Radiation Link — relay stations linked by a damage corridor, slowly
                // orbiting the planet; higher levels add relays (more connections)
                int t = ClosestEnemyTo(Vector2.Zero, B.DespawnRadius);
                float bearing = t >= 0 ? Enemies[t].Pos.Angle() : Rng.NextFloat(0f, Mathf.Tau);
                // PDTD's Radiation Link always starts as a SINGLE link (2 relay stations).
                // Extra links come only from "+1 Radiation Link" upgrade cards, which trade
                // damage for reach — levelling the weapon alone never adds links.
                int nodes = Mathf.Clamp(2 + Mods.RadLinkExtraNodes, 2, 10);
                float spinDir = Rng.NextInt(2) == 0 ? 1f : -1f;
                _owEffects.Add(new OwEffect
                {
                    Kind = 1, DieAt = GameTime + dur, Dps = dmg * Mathf.Max(0.1f, Mods.RadLineDamageMult), P0 = bearing,
                    NodeCount = nodes, Spin = spinDir * (0.10f + 0.01f * L), StartTime = GameTime,
                });
                break;
            }
            case "shock_orb":
                _owEffects.Add(new OwEffect { Kind = 2, DieAt = GameTime + dur, Dps = dmg, Radius = radius, Stun = d.StunSeconds, P0 = Rng.NextFloat(0f, Mathf.Tau) });
                break;
            case "rad_zone":
                _owEffects.Add(new OwEffect { Kind = 3, DieAt = GameTime + dur, Dps = dmg, Radius = radius, P0 = Rng.NextFloat(0f, Mathf.Tau) });
                break;
            case "waterdrop":
            {
                // PDTD's Waterdrop (lua-decrypted/game/attack/aqua_attack.lua): a bullet that
                // carries "durability points" spent per hit and re-targets the next nearest
                // enemy each time — a ricochet chain, not a straight pierce-line. Searches from
                // the planet (not the platform) for the first target, same fix as Space Bomb.
                int cur = ClosestEnemyTo(Vector2.Zero, B.DespawnRadius);
                if (cur < 0) break;
                Vector2 node = from;
                Vector2 lastPos = node;
                System.Span<bool> hit = stackalloc bool[96];
                for (int j = 0; j < cnt && cur >= 0; j++)
                {
                    ref var en = ref Enemies[cur];
                    DamageEnemy(cur, dmg, DamageSource.Orbital, armorPen: d.ArmorPierce ? 9999f : 0f);
                    if (cur < 96) hit[cur] = true;
                    Events.PushLine(SimEventKind.ChainArc, node, en.Pos, 3f, 2);
                    node = en.Pos;
                    lastPos = node;
                    int next = -1; float best = radius * radius;
                    for (int e = 0; e < EnemyHighWater; e++)
                    {
                        if (!Enemies[e].Alive || (e < 96 && hit[e])) continue;
                        float dd = Enemies[e].Pos.DistanceSquaredTo(node);
                        if (dd < best) { best = dd; next = e; }
                    }
                    cur = next;
                }
                _fxOwBeamLeft = 0.14f; _fxOwBeamKind = 2; _fxOwBeamFrom = from; _fxOwBeamTo = lastPos;
                break;
            }
            case "space_bomb":
            {
                // PDTD's Space Bomb — a lobbed gravity bomb, instant AoE at the impact point.
                // Searches from the planet, not the platform — the platform-relative,
                // range-capped search here used to mean this often found no target at all
                // (the platform orbits far out, on its own independent phase, so it's
                // frequently just out of range of wherever the enemies actually are) and
                // silently did nothing: no damage, no animation, looked completely broken.
                int t = ClosestEnemyTo(Vector2.Zero, B.DespawnRadius);
                if (t < 0) break;
                Vector2 impact = Enemies[t].Pos;
                float rSq = radius * radius;
                for (int e = 0; e < EnemyHighWater; e++)
                {
                    ref var en = ref Enemies[e];
                    if (!en.Alive || en.Pos.DistanceSquaredTo(impact) > rSq) continue;
                    DamageEnemy(e, dmg, DamageSource.Orbital);
                }
                _fxOwBeamLeft = 0.24f; _fxOwBeamKind = 3; _fxOwBeamFrom = from; _fxOwBeamTo = impact;
                break;
            }
            case "force_field":
                // PDTD's Force Field — a damage + slow pulse anchored on the planet itself
                _owEffects.Add(new OwEffect { Kind = 5, DieAt = GameTime + dur, Dps = dmg, Radius = radius, Slow = d.SlowFactor > 0f ? d.SlowFactor : 1f });
                break;
            case "laser":
            {
                // PDTD's plain Laser (lua-decrypted/game/attack/laser.lua) — strikes several of
                // the nearest targets at once and scorches each impact point into a short-lived
                // burning zone (its real "LaserZoneField"/Irradiated mechanic), plus a chance to
                // stun. Searches from the planet, not the platform, so it always finds targets
                // regardless of the platform's current orbital phase (see waterdrop/space_bomb —
                // a platform-relative, range-capped search is why those looked broken).
                System.Span<int> picks = stackalloc int[8];
                int n = NearestEnemiesTo(Vector2.Zero, System.Math.Min(cnt, 8), picks);
                for (int m = 0; m < n; m++)
                {
                    int e = picks[m];
                    DamageEnemy(e, dmg, DamageSource.Orbital);
                    if (Enemies[e].Alive && d.StunSeconds > 0f && Rng.NextFloat() < 0.3f
                        && !_missionEnemyDefs[Enemies[e].DefIndex].CcImmune)
                        Enemies[e].StunLeft = Mathf.Max(Enemies[e].StunLeft, d.StunSeconds);
                    _owEffects.Add(new OwEffect
                    {
                        Kind = 6, DieAt = GameTime + Mathf.Max(0.5f, d.DotSeconds), Dps = d.DotDps,
                        Radius = 44f, Pos = Enemies[e].Pos,
                    });
                }
                if (n > 0) { _fxOwBeamLeft = 0.12f; _fxOwBeamKind = 1; _fxOwBeamFrom = from; _fxOwBeamTo = Enemies[picks[0]].Pos; }
                break;
            }
        }

        _owCd[i] = Mathf.Max(d.MinCooldown, d.Cooldown + d.CooldownPerLevel * (L - 1)) / Mathf.Max(0.2f, Mods.OrbitalWeaponRateMult);
        Events.Push(SimEventKind.HeroWeaponFired, from, radius, 10 + i);
    }

    private void StepOwEffect(ref OwEffect fx, float dt)
    {
        fx.Tick += dt;
        float interval = 0.25f;
        float t = GameTime;

        if (fx.Kind == 1) // radiation line — PDTD's Radiation Link: relay stations joined by a beam
        {
            int n = Mathf.Max(2, fx.NodeCount);
            System.Span<Vector2> nodes = stackalloc Vector2[10];
            for (int k = 0; k < n; k++) nodes[k] = RadLineNode(in fx, k);

            while (fx.Tick >= interval)
            {
                fx.Tick -= interval;
                for (int seg = 0; seg < n - 1; seg++)
                {
                    Vector2 a = nodes[seg], b = nodes[seg + 1];
                    Vector2 dir = b - a;
                    float len = dir.Length();
                    if (len < 1f) continue;
                    dir /= len;
                    for (int e = 0; e < EnemyHighWater; e++)
                    {
                        ref readonly var en = ref Enemies[e];
                        if (!en.Alive) continue;
                        Vector2 rel = en.Pos - a;
                        float along = rel.Dot(dir);
                        if (along < 0f || along > len) continue;
                        if ((rel - dir * along).Length() > RadLineBeamHalfWidth + en.Radius) continue;
                        DamageEnemy(e, fx.Dps * interval * 4f, DamageSource.Orbital, shieldMult: 0f);
                        // "Irradiated": crossing the corridor leaves lingering radiation
                        // damage, as in PDTD. Without this the whole weapon's output was
                        // however much it could land during a fraction-of-a-second
                        // crossing, which rounded to nothing on anything but a trash mob.
                        ref var vic = ref Enemies[e];
                        vic.BurnDps = Mathf.Max(vic.BurnDps, fx.Dps * 0.5f);
                        vic.BurnLeft = Mathf.Max(vic.BurnLeft, 3f);
                    }
                }
            }
        }
        else if (fx.Kind == 4) // beam laser — locked on, continuous, pierces shields + anything in the beam
        {
            fx.From = OrbitalPlatformPos(fx.WeaponIndex);
            int ti = Resolve(in fx.Target, out int idx) ? idx : -1;
            if (ti < 0) { ti = ClosestEnemyTo(fx.From, B.DespawnRadius); if (ti >= 0) fx.Target = HandleOf(ti); }
            if (ti < 0) { fx.DieAt = GameTime; return; }   // no targets left — let it fizzle out
            fx.Pos = Enemies[ti].Pos;

            Vector2 dir = (fx.Pos - fx.From);
            float len = dir.Length();
            if (len > 1f)
            {
                dir /= len;
                while (fx.Tick >= interval)
                {
                    fx.Tick -= interval;
                    for (int e = 0; e < EnemyHighWater; e++)
                    {
                        ref var en = ref Enemies[e];
                        if (!en.Alive) continue;
                        Vector2 rel = en.Pos - fx.From;
                        float along = rel.Dot(dir);
                        if (along < 0f || along > len + 20f) continue;
                        if ((rel - dir * along).Length() > 22f + en.Radius) continue;
                        DamageEnemy(e, fx.Dps * interval * 4f, DamageSource.Orbital, shieldMult: 0f);
                    }
                }
            }
        }
        else if (fx.Kind == 5) // force field — planet-centred damage + slow aura
        {
            float rSq = fx.Radius * fx.Radius;
            if (fx.Slow > 0f && fx.Slow < 1f)
            {
                for (int e = 0; e < EnemyHighWater; e++)
                {
                    ref readonly var en = ref Enemies[e];
                    if (en.Alive && en.Pos.LengthSquared() <= rSq) ApplySlow(e, fx.Slow);
                }
            }
            while (fx.Tick >= interval)
            {
                fx.Tick -= interval;
                for (int e = 0; e < EnemyHighWater; e++)
                {
                    ref var en = ref Enemies[e];
                    if (!en.Alive || en.Pos.LengthSquared() > rSq) continue;
                    DamageEnemy(e, fx.Dps * interval * 4f, DamageSource.Orbital);
                }
            }
        }
        else if (fx.Kind == 6) // laser burn zone — a static scorched patch left at each impact point
        {
            float rSq = fx.Radius * fx.Radius;
            while (fx.Tick >= interval)
            {
                fx.Tick -= interval;
                for (int e = 0; e < EnemyHighWater; e++)
                {
                    ref var en = ref Enemies[e];
                    if (!en.Alive || en.Pos.DistanceSquaredTo(fx.Pos) > rSq) continue;
                    DamageEnemy(e, fx.Dps * interval * 4f, DamageSource.Orbital);
                }
            }
        }
        else // shock orb (2) or radiation zone (3) — a moving AoE
        {
            if (fx.Kind == 2)
            {
                float a = fx.P0 + t * 0.9f;
                fx.Pos = Vector2.FromAngle(a) * (B.SentinelOrbitRadius * 0.82f);

                // PDTD's real Ball Lightning (lua-decrypted/game/attack/ball_lightning_attack.lua)
                // chain-arcs to nearby enemies, not just a plain damage circle — "continuously
                // strikes enemies with lightning" per its own flavor text. fx.Spin is otherwise
                // unused by shock_orb, reused here as a between-zap countdown.
                fx.Spin -= dt;
                if (fx.Spin <= 0f)
                {
                    fx.Spin = 0.8f;
                    int zt = ClosestEnemyTo(fx.Pos, fx.Radius * 2.2f);
                    if (zt >= 0)
                    {
                        DamageEnemy(zt, fx.Dps * 1.4f, DamageSource.Orbital);
                        Events.PushLine(SimEventKind.ChainArc, fx.Pos, Enemies[zt].Pos, 3f, 3);
                    }
                }
            }
            else
            {
                float a = fx.P0 + t * 0.5f;
                float r = B.SpawnRadius * (0.45f + 0.25f * Mathf.Sin(t * 0.7f + fx.P0));
                fx.Pos = Vector2.FromAngle(a) * r;
            }
            float rSq = fx.Radius * fx.Radius;
            while (fx.Tick >= interval)
            {
                fx.Tick -= interval;
                for (int e = 0; e < EnemyHighWater; e++)
                {
                    ref var en = ref Enemies[e];
                    if (!en.Alive || en.Pos.DistanceSquaredTo(fx.Pos) > rSq) continue;
                    DamageEnemy(e, fx.Dps * interval * 4f, DamageSource.Orbital, shieldMult: fx.Kind == 3 ? 0f : 1f);
                    if (en.Alive && fx.Stun > 0f && !_missionEnemyDefs[en.DefIndex].CcImmune)
                        en.StunLeft = Mathf.Max(en.StunLeft, fx.Stun);
                }
            }
        }
    }
}
