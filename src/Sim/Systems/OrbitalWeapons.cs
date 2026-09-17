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

    /// <summary>Upgrade cards taken for each weapon this run. PDTD shows three star pips
    /// under a sentinel's tile and promotes it on the fourth: picks 1-3 light one, two and
    /// three stars, and the fourth clears them and raises the level (where the stars turn
    /// purple). So level = 1 + picks/StarsPerLevel and stars = picks % StarsPerLevel.</summary>
    private int[] _owPicks = System.Array.Empty<int>();

    /// <summary>Picks per promotion — three lit stars, then the fourth pick levels up.</summary>
    public const int StarsPerLevel = 4;

    /// <summary>Lit star pips under weapon <paramref name="i"/>'s tile right now (0-3).</summary>
    public int OrbitalWeaponStars(int i) =>
        (uint)i < (uint)_owPicks.Length ? _owPicks[i] % StarsPerLevel : 0;

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
        public float NodeShotTimer;  // rad_line: "Photon Nodes" endpoint-laser cadence
        /// <summary>rad_line behaviour flags resolved at cast time (level gate or card
        /// trait), so the tick and the reaper don't have to re-look-up the def:
        /// 1 = Link Burst, 2 = Photon Nodes.</summary>
        public int Flags;
    }

    private const int RadFlagBurst = 1;
    private const int RadFlagNodeShot = 2;

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
    /// <summary>"Photon Nodes" — seconds between endpoint laser shots. PDTD's card says
    /// "every 2s"; its sibling "Arc Nodes" says 2.5s.</summary>
    private const float RadLineNodeShotInterval = 2f;
    private const float RadLineNodeShotRange = 340f;
    /// <summary>Endpoint shot damage as a multiple of the link's per-tick DPS.</summary>
    private const float RadLineNodeShotMult = 3.5f;
    /// <summary>"Link Burst" — radius of the explosion each relay leaves when the
    /// structure expires.</summary>
    private const float RadLineBurstRadius = 150f;
    private const float RadLineBurstMult = 6f;

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
        _owPicks = new int[n];
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

    /// <summary>Put a weapon into play at level 1 (its "Release a X Sentinel" card).</summary>
    private void UnlockOrbitalWeapon(int i)
    {
        if ((uint)i >= (uint)_owLevel.Length) return;
        if (_owLevel[i] <= 0) _owLevel[i] = 1;
    }

    /// <summary>Record an upgrade card taken for weapon <paramref name="i"/>: light the
    /// next star and, on the fourth, promote it a level and clear them.</summary>
    private void AddOrbitalStar(int i)
    {
        if ((uint)i >= (uint)_owPicks.Length) return;
        _owPicks[i]++;
        if (_owPicks[i] % StarsPerLevel == 0) LevelUpOrbitalWeapon(i);
    }

    /// <summary>Index of the orbital weapon a skill card belongs to, or −1 for cards whose
    /// Kind isn't an orbital weapon (the planet's missile battery).</summary>
    public int OrbitalIndexOfKind(string kind)
    {
        for (int i = 0; i < Cfg.OrbitalWeapons.Count; i++)
            if (Cfg.OrbitalWeapons[i].Kind == kind) return i;
        return -1;
    }

    private void StepOrbitalWeapons(float dt)
    {
        if (_fxOwBeamLeft > 0f) _fxOwBeamLeft -= dt;

        for (int i = 0; i < _owCd.Length; i++) if (_owCd[i] > 0f) _owCd[i] -= dt;

        // --- active field effects ---
        for (int k = _owEffects.Count - 1; k >= 0; k--)
        {
            var fx = _owEffects[k];
            if (GameTime >= fx.DieAt)
            {
                // "Link Burst" (PDTD card 1000632): the relays detonate when the structure
                // ends rather than just switching off.
                if (fx.Kind == 1 && (fx.Flags & RadFlagBurst) != 0)
                    RadLineBurst(in fx);
                _owEffects.RemoveAt(k);
                continue;
            }
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
        // Two layers stack here: the global chip/research bonuses
        // (Mods.OrbitalWeapon*Mult), which apply to every sentinel, and the per-weapon
        // ones from PDTD's upgrade cards and per-weapon chips ("Radiation Link DMG +60%"),
        // which ModifierSet keys by weapon Kind — see its "per-weapon modifiers" section.
        string kind = d.Kind;
        float dmg = (d.Damage + d.DamagePerLevel * (L - 1))
                    * Mathf.Max(0.2f, Mods.OrbitalWeaponDamageMult)
                    * Mathf.Max(0.05f, Mods.WeaponMult(kind, "damage"));
        int cnt = Mathf.Max(1, d.Count + Mathf.FloorToInt(d.CountPerLevel * (L - 1))
                               + Mathf.FloorToInt(Mods.WeaponAdd(kind, "count")));
        float radius = (d.Radius + d.RadiusPerLevel * (L - 1))
                       * Mathf.Max(0.2f, Mods.OrbitalWeaponRadiusMult)
                       * Mathf.Max(0.2f, Mods.WeaponMult(kind, "radius"));
        float dur = (d.Duration + d.DurationPerLevel * (L - 1))
                    * Mathf.Max(0.2f, Mods.WeaponMult(kind, "duration"))
                    + Mods.WeaponAdd(kind, "duration_flat");
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
                int nodes = Mathf.Clamp(2 + Mods.RadLinkExtraNodes
                                          + Mathf.FloorToInt(Mods.WeaponAdd("rad_line", "count")), 2, 10);

                // Rotation is an UPGRADE in PDTD ("Lingering Orbit" at weapon level 2, then
                // "Link Spin" doubles it), not base behaviour — a fresh Radiation Link hangs
                // dead still. Without the gate every cast span slowly from the first second,
                // which is what made it read as wrong.
                float spin = 0f;
                if ((d.RotateLevel > 0 && L >= d.RotateLevel) || Mods.WeaponTrait("rad_line", "enableRotate"))
                {
                    float spinDir = Rng.NextInt(2) == 0 ? 1f : -1f;
                    spin = spinDir * (0.10f + 0.01f * L) * Mathf.Max(0.1f, Mods.WeaponMult("rad_line", "rotate_speed"));
                }

                // ONE structure, not a pile of them. Radiation Link's duration (7s+) is
                // longer than its cooldown (~4.7s), so every cast used to add a second,
                // third... independent link at its own random bearing — on screen that's
                // two disconnected lines that don't share endpoints, which is exactly the
                // reported bug. Re-firing now refreshes the live structure in place.
                int radFlags = 0;
                if ((d.BurstLevel > 0 && L >= d.BurstLevel) || Mods.WeaponTrait("rad_line", "RadiationLineExplosion"))
                    radFlags |= RadFlagBurst;
                if ((d.NodeShotLevel > 0 && L >= d.NodeShotLevel) || Mods.WeaponTrait("rad_line", "PhotonNodes"))
                    radFlags |= RadFlagNodeShot;

                bool refreshed = false;
                for (int fi = 0; fi < _owEffects.Count; fi++)
                {
                    var ex = _owEffects[fi];
                    if (ex.Kind != 1) continue;
                    ex.DieAt = GameTime + dur;
                    ex.Dps = dmg * Mathf.Max(0.1f, Mods.RadLineDamageMult);
                    ex.NodeCount = nodes;
                    ex.Spin = spin;
                    // Re-aim at the current pressure, every cast, spinning or not. A single
                    // structure only spans one chord, so a link left on the bearing of
                    // whatever was closest when it first went up ends up guarding empty
                    // sky — the old stacking hid that by covering several bearings at once.
                    // Resetting StartTime alongside P0 restarts the sweep from the new
                    // bearing, which is exactly a re-launch; skipping this for spinning
                    // links made "Lingering Orbit" a straight downgrade, since the link
                    // then rotated away from the enemies and never came back.
                    ex.P0 = bearing;
                    ex.StartTime = GameTime;
                    ex.Flags = radFlags;
                    _owEffects[fi] = ex;
                    refreshed = true;
                    break;
                }
                if (!refreshed)
                {
                    _owEffects.Add(new OwEffect
                    {
                        Kind = 1, DieAt = GameTime + dur, Dps = dmg * Mathf.Max(0.1f, Mods.RadLineDamageMult), P0 = bearing,
                        NodeCount = nodes, Spin = spin, StartTime = GameTime, Flags = radFlags,
                    });
                }
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
                System.Span<bool> hit = stackalloc bool[96];
                for (int j = 0; j < cnt && cur >= 0; j++)
                {
                    ref var en = ref Enemies[cur];
                    DamageEnemy(cur, dmg, DamageSource.Orbital, armorPen: d.ArmorPierce ? 9999f : 0f);
                    if (cur < 96) hit[cur] = true;
                    Events.PushLine(SimEventKind.ChainArc, node, en.Pos, 3f, 2);
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
                // No _fxOwBeam here. That FX draws a straight beam from the platform to the
                // LAST enemy in the chain, over the top of the ricochet arcs — so a weapon
                // that was already bouncing correctly still looked like it fired a laser.
                // The ChainArc events above (AquaBolt) are the whole visual: launch arc
                // from the platform, then a bolt between each pair of victims.
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

        _owCd[i] = Mathf.Max(d.MinCooldown, d.Cooldown + d.CooldownPerLevel * (L - 1))
                   / Mathf.Max(0.2f, Mods.OrbitalWeaponRateMult)
                   / Mathf.Max(0.2f, Mods.WeaponMult(kind, "rate"));
        Events.Push(SimEventKind.HeroWeaponFired, from, radius, 10 + i);
    }

    /// <summary>"Link Burst": every relay station explodes when the Radiation Link expires.
    /// Damage keys off the structure's own DPS so it scales with the weapon's level and
    /// every per-weapon damage card rather than needing its own tuning knob.</summary>
    private void RadLineBurst(in OwEffect fx)
    {
        int n = Mathf.Max(2, fx.NodeCount);
        float r2 = RadLineBurstRadius * RadLineBurstRadius;
        for (int k = 0; k < n; k++)
        {
            Vector2 c = RadLineNode(in fx, k);
            for (int e = 0; e < EnemyHighWater; e++)
            {
                if (!Enemies[e].Alive) continue;
                if (Enemies[e].Pos.DistanceSquaredTo(c) > r2) continue;
                DamageEnemy(e, fx.Dps * RadLineBurstMult, DamageSource.Orbital, shieldMult: 0f);
            }
            Events.Push(SimEventKind.MissileImpact, c, RadLineBurstRadius, 0);
        }
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

                // "Photon Nodes" (PDTD card 1000651): the relay endpoints fire a small
                // laser on their own timer. Endpoints only — the interior relays of a long
                // chain stay quiet, same as PDTD.
                if ((fx.Flags & RadFlagNodeShot) != 0)
                {
                    fx.NodeShotTimer += interval;
                    if (fx.NodeShotTimer >= RadLineNodeShotInterval)
                    {
                        fx.NodeShotTimer = 0f;
                        for (int k = 0; k < n; k++)
                        {
                            if (k != 0 && k != n - 1) continue;
                            int tgt = ClosestEnemyTo(nodes[k], RadLineNodeShotRange);
                            if (tgt < 0) continue;
                            DamageEnemy(tgt, fx.Dps * RadLineNodeShotMult, DamageSource.Orbital, shieldMult: 0f);
                            Events.PushLine(SimEventKind.ChainArc, nodes[k], Enemies[tgt].Pos, 2f, 1);
                        }
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
