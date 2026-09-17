using Godot;

namespace Sentinel.Sim;

/// <summary>
/// The ship's own weapon systems — Laser Volley, Missile Barrage, Ion Cannon,
/// Yamato Cannon, Plasma Field, Shields Boost. Each has a run level (0 = locked)
/// that the in-fight upgrade cards raise. The hull auto-fires everything unlocked
/// unless the player turns auto-fire off, and can fire any weapon by hand.
/// All timers are game-time; every roll comes off <see cref="Rng"/> — deterministic.
/// </summary>
public sealed partial class SimWorld
{
    private int[] _hwLevel = System.Array.Empty<int>();     // per weapon, 0 = not yet drafted
    private float[] _hwCd = System.Array.Empty<float>();     // cooldown remaining
    private bool _heroAutoFire = true;

    private float _heroShield;         // absorb pool from Shields Boost
    private float _heroShieldLeft;     // seconds remaining
    private float _heroShieldAbsorb;   // fraction of a hit the pool eats
    private float _plasmaRadius;       // >0 while Plasma Field is unlocked (renderer)

    // Ship laser: one high-power beam that locks a target and burns it for a few seconds
    // (PDTD's ship laser), not the old instant multi-beam cone.
    private float _heroBeamLeft, _heroBeamDps, _heroBeamTick, _heroBeamRange;
    private EnemyHandle _heroBeamTarget = EnemyHandle.None;

    // short cosmetic timers the renderer reads (set from sim, never read back)
    private float _fxLaserLeft;   private Vector2 _fxLaserAim;   private int _fxLaserBeams;
    private float _fxYamatoLeft;  private Vector2 _fxYamatoPos;  private float _fxYamatoR;
    private float _fxIonLeft;     private Vector2 _fxIonStart;

    // ---- renderer / HUD views ----
    public int HeroWeaponCount => _hwLevel.Length;
    public int HeroWeaponLevel(int i) => (uint)i < (uint)_hwLevel.Length ? _hwLevel[i] : 0;
    public float HeroWeaponCooldownLeft(int i) => (uint)i < (uint)_hwCd.Length ? _hwCd[i] : 0f;
    public bool HeroAutoFire => _heroAutoFire;
    public float HeroShield => _heroShield;
    public float HeroShieldLeft => _heroShieldLeft;
    public float PlasmaFieldRadius => _plasmaRadius;
    public float FxLaserLeft => _fxLaserLeft;
    public Vector2 FxLaserAim => _fxLaserAim;
    public int FxLaserBeams => _fxLaserBeams;
    /// <summary>Seconds left on the ship laser's locked beam (0 = not firing).</summary>
    public float HeroBeamLeft => _heroBeamLeft;
    /// <summary>Where the ship laser is currently burning, or null if it has no live target.</summary>
    public Vector2? HeroBeamTargetPos => Resolve(in _heroBeamTarget, out int bi) ? Enemies[bi].Pos : null;
    public float FxYamatoLeft => _fxYamatoLeft;
    public Vector2 FxYamatoPos => _fxYamatoPos;
    public float FxYamatoRadius => _fxYamatoR;
    public float FxIonLeft => _fxIonLeft;
    public Vector2 FxIonStart => _fxIonStart;

    private void ResetHeroWeapons()
    {
        int n = Cfg.HeroWeapons.Count;
        _hwLevel = new int[n];
        _hwCd = new float[n];
        _heroAutoFire = true;
        _heroShield = _heroShieldLeft = _heroShieldAbsorb = 0f;
        _plasmaRadius = 0f;
        _fxLaserLeft = _fxYamatoLeft = _fxIonLeft = 0f;
        _heroBeamLeft = _heroBeamDps = _heroBeamTick = _heroBeamRange = 0f;
        _heroBeamTarget = EnemyHandle.None;
    }

    /// <summary>An upgrade-card pick — raise this weapon one level (unlock at 1).</summary>
    private void LevelUpHeroWeapon(int i)
    {
        if ((uint)i >= (uint)_hwLevel.Length) return;
        int max = Mathf.Max(1, Cfg.HeroWeapons[i].MaxLevel);
        if (_hwLevel[i] < max) _hwLevel[i]++;
    }

    private int _hwPlasmaIndex = -1;
    private int HwPlasmaIndex()
    {
        if (_hwPlasmaIndex == -2) return -1;
        if (_hwPlasmaIndex >= 0) return _hwPlasmaIndex;
        for (int i = 0; i < Cfg.HeroWeapons.Count; i++)
            if (Cfg.HeroWeapons[i].AlwaysOn) { _hwPlasmaIndex = i; return i; }
        _hwPlasmaIndex = -2;
        return -1;
    }

    private void StepHeroWeapons(float dt)
    {
        for (int i = 0; i < _hwCd.Length; i++)
            if (_hwCd[i] > 0f) _hwCd[i] -= dt;

        if (_fxLaserLeft > 0f) _fxLaserLeft -= dt;
        if (_fxYamatoLeft > 0f) _fxYamatoLeft -= dt;
        if (_fxIonLeft > 0f) _fxIonLeft -= dt;

        StepHeroBeam(dt);

        // --- Plasma Field: always-on aura once unlocked ---
        int pi = HwPlasmaIndex();
        _plasmaRadius = 0f;
        if (pi >= 0 && _hwLevel[pi] > 0 && Hero.Alive)
        {
            var d = Cfg.HeroWeapons[pi];
            int L = _hwLevel[pi];
            float r = d.Radius + d.RadiusPerLevel * (L - 1);
            float dps = d.Damage + d.DamagePerLevel * (L - 1);
            float rSq = r * r;
            for (int e = 0; e < EnemyHighWater; e++)
                if (Enemies[e].Alive && Enemies[e].Pos.DistanceSquaredTo(Hero.Pos) <= rSq)
                    DamageEnemy(e, dps * dt, DamageSource.Hero);
            _plasmaRadius = r;
        }

        // --- Shields Boost buff decay ---
        if (_heroShieldLeft > 0f)
        {
            _heroShieldLeft -= dt;
            if (_heroShieldLeft <= 0f) { _heroShieldLeft = 0f; _heroShield = 0f; }
        }

        if (!Hero.Alive || !_heroAutoFire) return;

        for (int i = 0; i < _hwLevel.Length; i++)
        {
            if (_hwLevel[i] <= 0 || _hwCd[i] > 0f) continue;
            var d = Cfg.HeroWeapons[i];
            if (d.AlwaysOn) continue;

            if (d.Kind == "shield")
            {
                // only pop it defensively — hull hurt or the last barrier is gone
                if (_heroShield > 0f || Hero.Hull > Hero.MaxHull * 0.65f) continue;
            }
            else if (ClosestEnemyTo(Hero.Pos, d.Range >= 9000f ? B.DespawnRadius : d.Range + 60f) < 0)
                continue;

            FireHeroWeapon(i);
        }
    }

    /// <summary>Fire one weapon now (auto or manual). Returns true if it went off.</summary>
    private bool FireHeroWeapon(int i)
    {
        if ((uint)i >= (uint)_hwLevel.Length || _hwLevel[i] <= 0 || _hwCd[i] > 0f || !Hero.Alive) return false;
        var d = Cfg.HeroWeapons[i];
        if (d.AlwaysOn) return false;

        int L = _hwLevel[i];
        float dmg = d.Damage + d.DamagePerLevel * (L - 1);
        int cnt = Mathf.Max(1, d.Count + Mathf.FloorToInt(d.CountPerLevel * (L - 1)));

        switch (d.Kind)
        {
            case "laser": FireLaser(d, dmg, d.Duration + d.DurationPerLevel * (L - 1)); break;
            case "missiles": FireMissiles(d, dmg, cnt); break;
            case "ion": FireIonBeam(d, dmg, cnt); break;
            case "yamato": FireYamato(d, dmg, L); break;
            case "shield":
                _heroShield = Mathf.Max(_heroShield, dmg);
                _heroShieldLeft = d.Duration + d.DurationPerLevel * (L - 1);
                _heroShieldAbsorb = Mathf.Clamp(d.Absorb, 0.1f, 0.9f);
                Events.Push(SimEventKind.HeroShieldPop, Hero.Pos, _heroShield);
                break;
            default: return false;
        }

        float cd = Mathf.Max(d.MinCooldown, d.Cooldown + d.CooldownPerLevel * (L - 1));
        // "Yamato charges 30% faster" cards shorten its charge specifically
        if (d.Kind == "yamato") cd /= Mathf.Max(0.2f, Mods.HeroYamatoChargeMult);
        _hwCd[i] = cd;
        return true;
    }

    private Vector2 HeroAimDir()
    {
        int t = Resolve(in _heroFocus, out int fi) ? fi : ClosestEnemyTo(Hero.Pos, B.DespawnRadius);
        if (t < 0) return Hero.Pos.LengthSquared() > 1f ? (-Hero.Pos).Normalized() : Vector2.Down;
        Vector2 dir = Enemies[t].Pos - Hero.Pos;
        return dir.LengthSquared() > 0.01f ? dir.Normalized() : Vector2.Down;
    }

    /// <summary>PDTD's ship laser: locks one target and burns a single high-power beam into
    /// it for the weapon's duration, rather than spraying an instant cone. Damage is a
    /// per-second rate applied while the beam holds (see StepHeroBeam).</summary>
    private void FireLaser(Config.HeroWeaponDef d, float dps, float duration)
    {
        int t = Resolve(in _heroFocus, out int fi) ? fi : ClosestEnemyTo(Hero.Pos, d.Range);
        if (t < 0) return;
        _heroBeamTarget = HandleOf(t);
        _heroBeamLeft = duration;
        _heroBeamDps = dps;
        _heroBeamRange = d.Range;
        _heroBeamTick = 0f;
        _fxLaserLeft = 0.14f; _fxLaserAim = (Enemies[t].Pos - Hero.Pos).Normalized(); _fxLaserBeams = 1;
        Events.Push(SimEventKind.HeroWeaponFired, Hero.Pos, d.Range, 0);
    }

    /// <summary>Burn the locked beam. Re-acquires if the target dies mid-burn so the beam
    /// isn't wasted, and drops out if nothing is left in range.</summary>
    private void StepHeroBeam(float dt)
    {
        if (_heroBeamLeft <= 0f) return;
        _heroBeamLeft -= dt;
        if (_heroBeamLeft <= 0f || !Hero.Alive) { _heroBeamLeft = 0f; _heroBeamTarget = EnemyHandle.None; return; }

        if (!Resolve(in _heroBeamTarget, out int ti))
        {
            int next = ClosestEnemyTo(Hero.Pos, _heroBeamRange);
            if (next < 0) return;
            _heroBeamTarget = HandleOf(next);
            ti = next;
        }

        DamageEnemy(ti, _heroBeamDps * dt, DamageSource.Hero, shieldMult: 0f);

        // PDTD's refraction: the beam bounces on to nearby enemies, each bounce weaker than
        // the last. Base is 0 extra targets — "+5 refract" cards raise Mods.HeroLaserRefract.
        int refract = Mods.HeroLaserRefract;
        if (refract > 0)
        {
            Vector2 from = Enemies[ti].Pos;
            System.Span<bool> hit = stackalloc bool[96];
            if (ti < 96) hit[ti] = true;
            float falloff = 1f;
            for (int r = 0; r < refract; r++)
            {
                int next = -1; float best = 260f * 260f;
                for (int e = 0; e < EnemyHighWater; e++)
                {
                    if (!Enemies[e].Alive || (e < 96 && hit[e])) continue;
                    float dd = Enemies[e].Pos.DistanceSquaredTo(from);
                    if (dd < best) { best = dd; next = e; }
                }
                if (next < 0) break;
                falloff *= 0.8f;
                DamageEnemy(next, _heroBeamDps * dt * falloff, DamageSource.Hero, shieldMult: 0f);
                if (next < 96) hit[next] = true;
                if (_heroBeamTick + dt >= 0.1f) Events.PushLine(SimEventKind.BeamTick, from, Enemies[next].Pos, 1f, 1);
                from = Enemies[next].Pos;
            }
        }

        // one beam-line event every few ticks — the renderer draws a continuous beam from
        // these and the audio layer throttles itself off the same stream
        _heroBeamTick += dt;
        if (_heroBeamTick >= 0.1f)
        {
            _heroBeamTick = 0f;
            Events.PushLine(SimEventKind.BeamTick, Hero.Pos, Enemies[ti].Pos, 2f, 1);
        }
    }

    private void FireMissiles(Config.HeroWeaponDef d, float dmg, int missiles)
    {
        Vector2 aim = Resolve(in _heroFocus, out int fi)
            ? Enemies[fi].Pos
            : (ClosestEnemyTo(Hero.Pos, B.DespawnRadius) is var n && n >= 0 ? Enemies[n].Pos : Hero.Pos + HeroAimDir() * 200f);

        System.Span<int> picks = stackalloc int[24];
        int got = NearestEnemiesTo(aim, System.Math.Min(missiles, 24), picks);
        for (int m = 0; m < missiles; m++)
        {
            Vector2 dir;
            EnemyHandle tgt = EnemyHandle.None;
            if (m < got) { tgt = HandleOf(picks[m]); dir = (Enemies[picks[m]].Pos - Hero.Pos).Normalized(); }
            else { dir = (aim - Hero.Pos).Normalized(); if (dir == Vector2.Zero) dir = Vector2.Down; }
            dir = dir.Rotated(Rng.NextFloat(-0.4f, 0.4f));
            SpawnProjectile(kind: 1, Hero.Pos, dir * (d.Speed * 0.5f), dmg, d.Splash, tgt, src: 255, life: 5f,
                            speedMax: d.Speed * 1.35f, accel: d.Speed * 2.4f, agility: 8f);
        }
        Events.Push(SimEventKind.VolleyLaunched, Hero.Pos, missiles);
        Events.Push(SimEventKind.HeroWeaponFired, Hero.Pos, 0f, 1);
    }

    private void FireIonBeam(Config.HeroWeaponDef d, float dmg, int jumps)
    {
        int cur = Resolve(in _heroFocus, out int fi) ? fi : ClosestEnemyTo(Hero.Pos, B.DespawnRadius);
        if (cur < 0) return;
        _fxIonStart = Hero.Pos; _fxIonLeft = 0.22f;
        System.Span<bool> hit = stackalloc bool[96];
        Vector2 from = Hero.Pos;
        for (int j = 0; j < jumps && cur >= 0; j++)
        {
            ref var e = ref Enemies[cur];
            e.Shield = 0f;
            e.ShieldRegenTimer = -3f;
            DamageEnemy(cur, dmg, DamageSource.Hero, shieldMult: 0f);
            ApplySlow(cur, 0.5f);
            if (cur < 96) hit[cur] = true;
            Events.PushLine(SimEventKind.ChainArc, from, e.Pos, 3f, 1);
            from = e.Pos;

            int next = -1; float best = 190f * 190f;
            for (int i = 0; i < EnemyHighWater; i++)
            {
                if (!Enemies[i].Alive || (i < 96 && hit[i])) continue;
                float dd = Enemies[i].Pos.DistanceSquaredTo(from);
                if (dd < best) { best = dd; next = i; }
            }
            cur = next;
        }
        Events.Push(SimEventKind.HeroWeaponFired, Hero.Pos, 0f, 2);
    }

    private void FireYamato(Config.HeroWeaponDef d, float dmg, int L)
    {
        Vector2 aim = Resolve(in _heroFocus, out int fi)
            ? Enemies[fi].Pos
            : (ClosestEnemyTo(Hero.Pos, B.DespawnRadius) is var n && n >= 0 ? Enemies[n].Pos : Hero.Pos + HeroAimDir() * 260f);

        float r = d.Radius + d.RadiusPerLevel * (L - 1);
        float rSq = r * r;
        // Spread: past the core blast the shot keeps hurting, tapering to
        // SpreadDamageFrac at the outer edge. Yamato used to be a hard-edged circle —
        // everything inside took the full hit and one pixel outside took nothing — which
        // made a huge, slow, expensive weapon feel narrow.
        float spreadR = r * Mathf.Max(1f, d.SpreadRadiusMult);
        float spreadSq = spreadR * spreadR;
        for (int i = 0; i < EnemyHighWater; i++)
        {
            ref var e = ref Enemies[i];
            if (!e.Alive) continue;
            float dSq = e.Pos.DistanceSquaredTo(aim);
            if (dSq > spreadSq) continue;

            float hit = dmg;
            if (dSq > rSq && spreadR > r + 0.01f)
            {
                // linear taper from full damage at the core edge to SpreadDamageFrac out
                float t = (Mathf.Sqrt(dSq) - r) / (spreadR - r);
                hit = dmg * Mathf.Lerp(1f, Mathf.Clamp(d.SpreadDamageFrac, 0f, 1f), Mathf.Clamp(t, 0f, 1f));
            }
            DamageEnemy(i, hit, DamageSource.Hero, armorPen: d.ArmorPierce ? 9999f : 0f, shieldMult: d.ShieldPierce ? 0f : 1f);
            if (!_missionEnemyDefs[e.DefIndex].CcImmune)
            {
                Vector2 outward = (e.Pos - aim);
                outward = outward.LengthSquared() > 1f ? outward.Normalized() : Vector2.Up;
                // shove scales down across the spread too, so the outer ring is nudged
                // rather than flung
                e.Pos += outward * (dSq <= rSq ? 60f : 26f);
                e.DistToCenter = e.Pos.Length();
                e.Standoff = false;
            }
        }
        _fxYamatoLeft = 0.5f; _fxYamatoPos = aim; _fxYamatoR = r;
        Events.Push(SimEventKind.HeroWeaponFired, aim, r, 3);
        Events.Push(SimEventKind.NovaPulse, aim, r);
    }

    /// <summary>Shields Boost soaks a slice of an incoming hit before the hull. Returns the leftover.</summary>
    private float HeroShieldSoak(float amount)
    {
        if (_heroShield <= 0f || _heroShieldLeft <= 0f) return amount;
        float toShield = amount * _heroShieldAbsorb;
        float soak = Mathf.Min(_heroShield, toShield);
        _heroShield -= soak;
        if (_heroShield <= 0.01f) { _heroShield = 0f; _heroShieldLeft = 0f; }
        return amount - soak;
    }
}
