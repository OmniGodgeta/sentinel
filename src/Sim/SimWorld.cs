using System;
using System.Collections.Generic;
using Godot;
using Sentinel.Config;

namespace Sentinel.Sim;

public enum SimPhase { Build, Wave, Won, Lost }

public struct RunStats
{
    public float DamageByTurrets;
    public float DamageByHero;
    public float DamageByAbilities;
    public int EnemiesKilled;
    public int EnemiesLeaked;
    public long TicksElapsed;

    public readonly float TotalDamage => DamageByTurrets + DamageByHero + DamageByAbilities;
}

/// <summary>
/// The entire authoritative game state for one mission and the fixed-order
/// per-tick step. Rendering and UI only read from here; all mutation goes
/// through queued <see cref="SimCommand"/>s applied at the top of a tick.
/// </summary>
public sealed partial class SimWorld
{
    // ---- config (immutable for the run) ----
    public readonly ConfigDb Cfg;
    internal readonly BalanceDef B;
    internal readonly TurretDef[] TurretDefs;
    internal readonly EnemyDef[] EnemyDefs;
    internal readonly AbilityDef[] AbilityDefs;
    private readonly Dictionary<string, int> _turretDefIndex = new();
    private readonly Dictionary<string, int> _enemyDefIndex = new();
    private readonly Dictionary<string, int> _abilityDefIndex = new();

    // ---- rng ----
    internal DetRandom Rng;

    // ---- pools ----
    internal const int EnemyCap = 2000;
    internal const int ProjCap = 6000;
    internal readonly Enemy[] Enemies = new Enemy[EnemyCap];
    internal readonly Projectile[] Projectiles = new Projectile[ProjCap];
    internal int EnemyHighWater;   // highest slot index ever used (iteration bound)
    internal int ProjHighWater;
    private readonly Stack<int> _freeEnemies = new();
    private readonly Stack<int> _freeProjectiles = new();

    internal Turret[] Turrets;
    internal HeroState Hero;
    internal AbilitySlot[] Abilities;

    // ---- run state ----
    public MissionDef Mission { get; private set; } = new();
    public SimPhase Phase { get; private set; } = SimPhase.Build;
    public int WaveIndex { get; private set; }          // 0-based index of current/next wave
    public int WaveCount => Mission.Waves.Count;
    public float PhaseTimer { get; private set; }       // build: seconds left; wave: seconds elapsed
    public float PlanetIntegrity { get; private set; }
    public float PlanetIntegrityMax { get; private set; }
    public int Credits { get; private set; }
    public int WavesCleared { get; private set; }
    public float ResearchDataEarned { get; private set; }
    public float XpEarned { get; private set; }
    public RunStats Stats;

    // active-wave spawn scheduler
    private readonly List<SpawnTicket> _pending = new(128);
    private int _spawnCursor;
    private int _aliveThisWave;
    private int _toSpawnThisWave;

    // command queue
    private readonly Queue<SimCommand> _commands = new();

    public readonly SimEventBuffer Events = new();

    private struct SpawnTicket
    {
        public float Time;        // seconds after wave start
        public int EnemyDefIndex;
        public float Angle;       // radians, position on the spawn ring
    }

    public SimWorld(ConfigDb cfg)
    {
        Cfg = cfg;
        B = cfg.Balance;

        TurretDefs = new TurretDef[cfg.TurretOrder.Count];
        for (int i = 0; i < TurretDefs.Length; i++)
        {
            TurretDefs[i] = cfg.Turret(cfg.TurretOrder[i]);
            _turretDefIndex[TurretDefs[i].Id] = i;
        }

        var enemyIds = new List<string>();
        // enemy defs come from the mission; collect the union lazily in Load.
        EnemyDefs = Array.Empty<EnemyDef>();

        AbilityDefs = new AbilityDef[cfg.AbilityOrder.Count];
        for (int i = 0; i < AbilityDefs.Length; i++)
        {
            AbilityDefs[i] = cfg.Ability(cfg.AbilityOrder[i]);
            _abilityDefIndex[AbilityDefs[i].Id] = i;
        }

        Turrets = new Turret[B.TurretSlots];
        Abilities = new AbilitySlot[cfg.Hero.AbilitySlots];
    }

    // Enemy defs are resolved per mission (only the enemies a mission uses).
    private EnemyDef[] _missionEnemyDefs = Array.Empty<EnemyDef>();
    internal EnemyDef EnemyDefAt(int i) => _missionEnemyDefs[i];

    public void Load(MissionDef mission, string[] equippedAbilityIds)
    {
        Mission = mission;
        Rng = new DetRandom(mission.Seed);

        // resolve the enemy defs this mission references
        var used = new List<EnemyDef>();
        _enemyDefIndex.Clear();
        foreach (var wave in mission.Waves)
            foreach (var g in wave.Groups)
            {
                if (_enemyDefIndex.ContainsKey(g.Enemy)) continue;
                if (!Cfg.HasEnemy(g.Enemy))
                {
                    GD.PushError($"Mission {mission.Id}: unknown enemy '{g.Enemy}'");
                    continue;
                }
                _enemyDefIndex[g.Enemy] = used.Count;
                used.Add(Cfg.Enemy(g.Enemy));
            }
        _missionEnemyDefs = used.ToArray();

        // reset pools
        Array.Clear(Enemies, 0, Enemies.Length);
        Array.Clear(Projectiles, 0, Projectiles.Length);
        EnemyHighWater = 0;
        ProjHighWater = 0;
        _freeEnemies.Clear();
        _freeProjectiles.Clear();

        // turrets: clear slots, precompute ring positions
        Turrets = new Turret[B.TurretSlots];
        for (int s = 0; s < B.TurretSlots; s++)
        {
            float ang = s * Mathf.Tau / B.TurretSlots - Mathf.Pi / 2f;
            Turrets[s].Slot = s;
            Turrets[s].Angle = ang;                    // faces outward by default
            Turrets[s].Pos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * B.TurretRingRadius;
            Turrets[s].Built = false;
            Turrets[s].Target = EnemyHandle.None;
        }

        // hero
        Hero = new HeroState
        {
            OrbitRadius = (B.HeroOrbitMin + B.HeroOrbitMax) * 0.5f,
            MaxHull = Cfg.Hero.MaxHull,
            Hull = Cfg.Hero.MaxHull,
            Alive = true,
            VolleyCooldownLeft = 0f,
            OverdriveVolleyMult = 1f,
        };
        Hero.Pos = new Vector2(0, -Hero.OrbitRadius);
        _heroTarget = Hero.Pos;

        // abilities
        Abilities = new AbilitySlot[Cfg.Hero.AbilitySlots];
        for (int i = 0; i < Abilities.Length; i++)
        {
            int defIdx = -1;
            if (i < equippedAbilityIds.Length && _abilityDefIndex.TryGetValue(equippedAbilityIds[i], out int di))
                defIdx = di;
            Abilities[i] = new AbilitySlot { DefIndex = defIdx, CooldownLeft = 0f };
        }

        PlanetIntegrityMax = B.PlanetIntegrity;
        PlanetIntegrity = B.PlanetIntegrity;
        Credits = B.StartingCredits;
        WaveIndex = 0;
        WavesCleared = 0;
        ResearchDataEarned = 0f;
        XpEarned = 0f;
        Stats = default;

        Phase = SimPhase.Build;
        PhaseTimer = B.BuildPhaseSeconds;
        _pending.Clear();
        _spawnCursor = 0;
        _aliveThisWave = 0;
        _toSpawnThisWave = 0;
    }

    public void Enqueue(in SimCommand cmd) => _commands.Enqueue(cmd);

    // ------------------------------------------------------------------
    //  Per-tick step — fixed order. Called by SimClock.Advance.
    // ------------------------------------------------------------------
    public void StepTick()
    {
        DrainCommands();

        switch (Phase)
        {
            case SimPhase.Build:
                // Build phase does not auto-advance in Phase 1; player taps "Launch".
                // (PhaseTimer kept for a future optional countdown.)
                break;

            case SimPhase.Wave:
                PhaseTimer += SimClock.TickDelta;
                StepSpawns();
                StepEnemies();
                StepHero();
                StepTurrets();
                StepProjectiles();
                StepAbilities();
                CheckWaveEnd();
                break;
        }

        Stats.TicksElapsed++;
    }

    private void DrainCommands()
    {
        while (_commands.Count > 0)
            Apply(_commands.Dequeue());
    }

    private void Apply(in SimCommand c)
    {
        switch (c.Type)
        {
            case CommandType.BuildTurret:
                TryBuildTurret(c.IntA, c.StrA);
                break;
            case CommandType.RotateTurret:
                if (InSlot(c.IntA) && Turrets[c.IntA].Built)
                    Turrets[c.IntA].Angle = c.FloatA;
                break;
            case CommandType.SellTurret:
                if (InSlot(c.IntA) && Turrets[c.IntA].Built && Phase == SimPhase.Build)
                {
                    Credits += TurretDefs[Turrets[c.IntA].DefIndex].Cost / 2;
                    Turrets[c.IntA].Built = false;
                    Turrets[c.IntA].Target = EnemyHandle.None;
                }
                break;
            case CommandType.StartWave:
                if (Phase == SimPhase.Build && WaveIndex < WaveCount)
                    BeginWave();
                break;
            case CommandType.SetHeroTarget:
                _heroTarget = ClampToOrbitBand(c.Pos);
                break;
            case CommandType.FireVolley:
                TryFireVolley(c.Pos);
                break;
            case CommandType.CastAbility:
                TryCastAbility(c.IntA, c.Pos);
                break;
        }
    }

    private bool InSlot(int s) => s >= 0 && s < Turrets.Length;

    private void TryBuildTurret(int slot, string? id)
    {
        if (Phase != SimPhase.Build || !InSlot(slot) || id == null) return;
        if (Turrets[slot].Built) return;
        if (!_turretDefIndex.TryGetValue(id, out int di)) return;
        var def = TurretDefs[di];
        if (Credits < def.Cost) return;
        Credits -= def.Cost;
        Turrets[slot].Built = true;
        Turrets[slot].DefIndex = di;
        Turrets[slot].CooldownLeft = 0f;
        Turrets[slot].Target = EnemyHandle.None;
        Turrets[slot].DamageDealt = 0f;
        // face outward from center through the slot
        Turrets[slot].Angle = Turrets[slot].Pos.Angle();
    }

    private void BeginWave()
    {
        var wave = Mission.Waves[WaveIndex];
        _pending.Clear();
        foreach (var g in wave.Groups)
        {
            if (!_enemyDefIndex.TryGetValue(g.Enemy, out int edi)) continue;
            for (int k = 0; k < g.Count; k++)
            {
                float ang;
                if (g.ArcCenterDeg < 0f)
                    ang = Rng.NextAngle();
                else
                {
                    float half = Mathf.DegToRad(g.ArcSpreadDeg) * 0.5f;
                    ang = Mathf.DegToRad(g.ArcCenterDeg) + Rng.NextFloat(-half, half);
                }
                _pending.Add(new SpawnTicket
                {
                    Time = g.StartDelay + k * g.Interval,
                    EnemyDefIndex = edi,
                    Angle = ang,
                });
            }
        }
        _pending.Sort(static (a, b) => a.Time.CompareTo(b.Time));
        _spawnCursor = 0;
        _toSpawnThisWave = _pending.Count;
        _aliveThisWave = 0;
        Phase = SimPhase.Wave;
        PhaseTimer = 0f;
    }

    private void CheckWaveEnd()
    {
        if (PlanetIntegrity <= 0f)
        {
            PlanetIntegrity = 0f;
            Phase = SimPhase.Lost;
            AccrueRewards(missionClear: false);
            Events.Push(SimEventKind.MissionLost, Vector2.Zero);
            return;
        }

        bool doneSpawning = _spawnCursor >= _pending.Count;
        if (doneSpawning && _aliveThisWave <= 0)
        {
            WavesCleared++;
            ResearchDataEarned += B.ResearchDataPerWave;
            XpEarned += B.XpPerWave;
            Credits += B.CreditsPerWave;
            Events.Push(SimEventKind.WaveCleared, Vector2.Zero, 0f, WaveIndex);

            WaveIndex++;
            if (WaveIndex >= WaveCount)
            {
                Phase = SimPhase.Won;
                AccrueRewards(missionClear: true);
                Events.Push(SimEventKind.MissionWon, Vector2.Zero);
            }
            else
            {
                Phase = SimPhase.Build;
                PhaseTimer = B.BuildPhaseSeconds;
                // top up hull a little between waves so a bad wave isn't a death sentence
                if (Hero.Alive) Hero.Hull = Mathf.Min(Hero.MaxHull, Hero.Hull + Hero.MaxHull * 0.15f);
            }
        }
    }

    private void AccrueRewards(bool missionClear)
    {
        if (missionClear)
        {
            ResearchDataEarned += B.ResearchDataMissionClear;
            XpEarned += B.XpMissionClear;
        }
        // rewards for partial progress are already added per wave cleared.
    }

    // ---- pool helpers ----
    internal int SpawnEnemy(int missionEnemyDefIndex, Vector2 pos)
    {
        int idx;
        if (_freeEnemies.Count > 0) idx = _freeEnemies.Pop();
        else if (EnemyHighWater < EnemyCap) idx = EnemyHighWater++;
        else return -1;

        var def = _missionEnemyDefs[missionEnemyDefIndex];
        ref var e = ref Enemies[idx];
        e.Alive = true;
        e.Gen++;
        e.DefIndex = missionEnemyDefIndex;
        e.Pos = pos;
        e.Hp = def.MaxHp;
        e.MaxHp = def.MaxHp;
        e.Armor = def.Armor;
        e.Radius = def.Radius;
        e.ContactDamage = def.ContactDamage;
        e.Bounty = def.Bounty;
        e.DistToCenter = pos.Length();
        return idx;
    }

    internal void KillEnemy(int idx, bool leaked)
    {
        ref var e = ref Enemies[idx];
        if (!e.Alive) return;
        e.Alive = false;
        e.Gen++;
        _freeEnemies.Push(idx);
        _aliveThisWave--;
        if (leaked) Stats.EnemiesLeaked++;
        else
        {
            Stats.EnemiesKilled++;
            Credits += e.Bounty;
            Events.Push(SimEventKind.EnemyKilled, e.Pos, e.Radius);
        }
    }

    internal bool Resolve(in EnemyHandle h, out int idx)
    {
        idx = h.Index;
        return !h.IsNone && idx < EnemyHighWater && Enemies[idx].Alive && Enemies[idx].Gen == h.Gen;
    }

    internal EnemyHandle HandleOf(int idx) => new() { Index = idx, Gen = Enemies[idx].Gen };

    internal int SpawnProjectile(byte kind, Vector2 pos, Vector2 vel, float dmg, float splash,
                                 EnemyHandle target, byte src, float life = 4f)
    {
        int idx;
        if (_freeProjectiles.Count > 0) idx = _freeProjectiles.Pop();
        else if (ProjHighWater < ProjCap) idx = ProjHighWater++;
        else return -1;

        ref var p = ref Projectiles[idx];
        p.Alive = true;
        p.Kind = kind;
        p.Pos = pos;
        p.Vel = vel;
        p.Damage = dmg;
        p.SplashRadius = splash;
        p.Target = target;
        p.SourceTurret = src;
        p.Life = life;
        return idx;
    }

    internal void DespawnProjectile(int idx)
    {
        if (!Projectiles[idx].Alive) return;
        Projectiles[idx].Alive = false;
        _freeProjectiles.Push(idx);
    }

    /// <summary>Apply damage to an enemy; handles armor, death, attribution.</summary>
    internal void DamageEnemy(int idx, float amount, DamageSource src)
    {
        ref var e = ref Enemies[idx];
        if (!e.Alive) return;
        float dealt = Mathf.Max(1f, amount - e.Armor);
        e.Hp -= dealt;
        switch (src)
        {
            case DamageSource.Turret: Stats.DamageByTurrets += dealt; break;
            case DamageSource.Hero: Stats.DamageByHero += dealt; break;
            case DamageSource.Ability: Stats.DamageByAbilities += dealt; break;
        }
        Events.Push(SimEventKind.EnemyHit, e.Pos, dealt);
        if (e.Hp <= 0f) KillEnemy(idx, leaked: false);
    }

    internal void DamagePlanet(float amount)
    {
        // Aegis Barrier absorbs first.
        for (int i = 0; i < Abilities.Length; i++)
        {
            ref var a = ref Abilities[i];
            if (a.DefIndex < 0) continue;
            if (AbilityDefs[a.DefIndex].Kind == "barrier" && a.ActiveLeft > 0f && a.P2 > 0f)
            {
                float absorbed = Mathf.Min(a.P2, amount);
                a.P2 -= absorbed;
                amount -= absorbed;
                if (a.P2 <= 0f) a.ActiveLeft = 0f;
                if (amount <= 0f) return;
            }
        }
        PlanetIntegrity -= amount;
        Events.Push(SimEventKind.PlanetHit, Vector2.Zero, amount);
    }

    internal enum DamageSource { Turret, Hero, Ability }

    // ---- geometry helpers ----
    internal Vector2 ClampToOrbitBand(Vector2 p)
    {
        float len = p.Length();
        if (len < 0.001f) return new Vector2(0, -Hero.OrbitRadius);
        float r = Mathf.Clamp(len, B.HeroOrbitMin, B.HeroOrbitMax);
        return p / len * r;
    }

    // ---- read-only views for renderer / UI ----
    public ReadOnlySpan<Enemy> EnemyView => new(Enemies, 0, EnemyHighWater);
    public ReadOnlySpan<Projectile> ProjectileView => new(Projectiles, 0, ProjHighWater);
    public ReadOnlySpan<Turret> TurretView => Turrets;
    public HeroState HeroView => Hero;
    public ReadOnlySpan<AbilitySlot> AbilityView => Abilities;
    public int EnemiesAlive => _aliveThisWave;
    public int SpawnsRemaining => Mathf.Max(0, _pending.Count - _spawnCursor);
}
