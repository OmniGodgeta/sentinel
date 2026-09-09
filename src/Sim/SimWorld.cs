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
    public long Tick { get; private set; }               // sim ticks since Load
    public float GameTime => Tick * SimClock.TickDelta;
    public int WaveIndex { get; private set; }          // 0-based index of current/next wave
    private bool _endless;
    public bool IsEndless => _endless;
    public int WaveCount => _endless ? int.MaxValue : Mission.Waves.Count;
    public float EndlessScale => _endless ? 1f + WaveIndex * 0.05f : 1f;
    public float PhaseTimer { get; private set; }       // build: seconds left; wave: seconds elapsed
    public float PlanetIntegrity { get; private set; }
    public float PlanetIntegrityMax { get; private set; }
    public float PlanetShield { get; private set; }
    public int Credits { get; private set; }
    public int WavesCleared { get; private set; }
    public float ResearchDataEarned { get; private set; }
    public float XpEarned { get; private set; }
    public int CoresEarned { get; private set; }
    public int AlloyEarned { get; private set; }
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

    internal Meta.ModifierSet Mods = new();

    public void Load(MissionDef mission, string[] equippedAbilityIds,
                     Meta.ModifierSet? mods = null,
                     float[]? abilityEffect = null, float[]? abilityCd = null)
    {
        Mission = mission;
        Mods = (mods ?? new Meta.ModifierSet()).Clone();   // run-scoped copy — card draft mutates it
        Rng = new DetRandom(mission.Seed);
        _draftRng = new DetRandom(mission.Seed ^ 0x9E3779B97F4A7C15UL);
        _runCards.Clear();
        _draftOptions.Clear();
        _pendingDrafts = 0;

        _endless = mission.Endless;

        // resolve the enemy defs this mission references (+ the endless roster)
        var used = new List<EnemyDef>();
        _enemyDefIndex.Clear();
        void Resolve(string id)
        {
            if (_enemyDefIndex.ContainsKey(id)) return;
            if (!Cfg.HasEnemy(id)) { GD.PushError($"Mission {mission.Id}: unknown enemy '{id}'"); return; }
            _enemyDefIndex[id] = used.Count;
            used.Add(Cfg.Enemy(id));
        }
        foreach (var wave in mission.Waves)
            foreach (var g in wave.Groups) Resolve(g.Enemy);
        foreach (var id in mission.EndlessRoster) Resolve(id);
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
        float heroHull = Cfg.Hero.MaxHull * Mathf.Max(0.2f, Mods.HeroHullMult);
        Hero = new HeroState
        {
            OrbitRadius = (B.HeroOrbitMin + B.HeroOrbitMax) * 0.5f,
            MaxHull = heroHull,
            Hull = heroHull,
            Alive = true,
            VolleyCooldownLeft = 0f,
            OverdriveVolleyMult = 1f,
        };
        Hero.Pos = new Vector2(0, -Hero.OrbitRadius);
        _heroTarget = Hero.Pos;

        // abilities — slot count from hero level, ids + levels from the loadout
        int slots = Mathf.Clamp(Mods.AbilitySlots, 3, 5);
        Abilities = new AbilitySlot[slots];
        for (int i = 0; i < Abilities.Length; i++)
        {
            int defIdx = -1;
            if (i < equippedAbilityIds.Length && _abilityDefIndex.TryGetValue(equippedAbilityIds[i], out int di))
                defIdx = di;
            float eff = (abilityEffect != null && i < abilityEffect.Length) ? abilityEffect[i] : 1f;
            float cdm = (abilityCd != null && i < abilityCd.Length) ? abilityCd[i] : 1f;
            Abilities[i] = new AbilitySlot { DefIndex = defIdx, CooldownLeft = 0f, EffMult = eff, CdMult = cdm };
        }

        PlanetIntegrityMax = B.PlanetIntegrity * Mathf.Max(0.2f, Mods.PlanetIntegrityMult);
        PlanetIntegrity = PlanetIntegrityMax;
        PlanetShield = Mods.PlanetStartShield;
        Credits = B.StartingCredits + Mods.StartCreditsAdd;
        WaveIndex = 0;
        WavesCleared = 0;
        ResearchDataEarned = 0f;
        XpEarned = 0f;
        Stats = default;

        Phase = SimPhase.Build;
        PhaseTimer = B.BuildPhaseSeconds;
        Tick = 0;
        CoresEarned = 0;
        AlloyEarned = 0;
        _bossHandle = EnemyHandle.None;
        PdgActiveLeft = SalvageActiveLeft = DronesActiveLeft = 0f;
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

        Tick++;
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
                    Credits += TurretRefund(c.IntA);
                    Turrets[c.IntA].Built = false;
                    Turrets[c.IntA].Target = EnemyHandle.None;
                }
                break;
            case CommandType.UpgradeTurret:
                TryUpgradeTurret(c.IntA);
                break;
            case CommandType.ForkTurret:
                TryForkTurret(c.IntA, c.IntB);
                break;
            case CommandType.PickCard:
                if (Phase == SimPhase.Build) PickCard(c.IntA);
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
        ref var t = ref Turrets[slot];
        t.Built = true;
        t.DefIndex = di;
        t.Level = 1;
        t.Fork = -1;
        t.CooldownLeft = 0f;
        t.RampStacks = 0f;
        t.RampGraceLeft = 0f;
        t.DisabledLeft = 0f;
        t.Target = EnemyHandle.None;
        t.DamageDealt = 0f;
        t.Angle = t.Pos.Angle();          // faces outward
    }

    public int TurretUpgradeCost(int slot)
    {
        if (!InSlot(slot) || !Turrets[slot].Built || Turrets[slot].Level >= 3) return -1;
        var def = TurretDefs[Turrets[slot].DefIndex];
        return Mathf.RoundToInt(def.Cost * B.TurretUpgradeCostMult * Turrets[slot].Level
                                * Mathf.Max(0.3f, Mods.TurretUpgradeCostMult));
    }

    private int TurretRefund(int slot)
    {
        var t = Turrets[slot];
        var def = TurretDefs[t.DefIndex];
        int spent = def.Cost;
        for (int l = 1; l < t.Level; l++)
            spent += Mathf.RoundToInt(def.Cost * B.TurretUpgradeCostMult * l);
        return spent / 2;
    }

    private void TryUpgradeTurret(int slot)
    {
        if (Phase != SimPhase.Build || !InSlot(slot) || !Turrets[slot].Built) return;
        int cost = TurretUpgradeCost(slot);
        if (cost < 0 || Credits < cost) return;
        Credits -= cost;
        Turrets[slot].Level++;
    }

    private void TryForkTurret(int slot, int forkIndex)
    {
        if (Phase != SimPhase.Build || !InSlot(slot) || !Turrets[slot].Built) return;
        if (Turrets[slot].Level < 3 || Turrets[slot].Fork >= 0) return;
        var def = TurretDefs[Turrets[slot].DefIndex];
        if (forkIndex < 0 || forkIndex >= def.Forks.Count) return;
        Turrets[slot].Fork = forkIndex;
    }

    // effective per-turret stats after level + fork
    internal readonly struct TurretStats
    {
        public readonly float Damage, FireInterval, Range, Splash, ArmorPen, ShieldMult;
        public readonly int Pierce, ChainJumps;
        public TurretStats(float d, float fi, float r, float sp, float ap, float sm, int pc, int cj)
        { Damage = d; FireInterval = fi; Range = r; Splash = sp; ArmorPen = ap; ShieldMult = sm; Pierce = pc; ChainJumps = cj; }
    }

    internal TurretStats StatsFor(in Turret t)
    {
        var def = TurretDefs[t.DefIndex];
        float lvlMult = Mathf.Pow(B.TurretUpgradeStatMult, t.Level - 1);
        float dMult = lvlMult, rateMult = 1f, rangeMult = 1f, splashMult = 1f, apAdd = 0f;
        int pierce = def.Pierce, chain = def.ChainJumps;
        if (t.Fork >= 0 && t.Fork < def.Forks.Count)
        {
            var f = def.Forks[t.Fork];
            dMult *= f.DamageMult;
            rateMult *= f.FireRateMult;
            rangeMult *= f.RangeMult;
            splashMult *= f.SplashMult;
            apAdd += f.ArmorPenAdd;
            pierce += f.ExtraPierce;
            chain += f.ExtraChain;
        }
        return new TurretStats(
            def.Damage * dMult * Mathf.Max(0.1f, Mods.TurretDamageMult),
            def.FireInterval / Mathf.Max(0.05f, rateMult * Mathf.Max(0.1f, Mods.TurretFireRateMult)),
            def.Range * rangeMult * Mathf.Max(0.3f, Mods.TurretRangeMult),
            def.SplashRadius * splashMult * Mathf.Max(0.1f, Mods.TurretSplashMult),
            def.ArmorPen + apAdd + Mods.TurretArmorPenAdd,
            def.ShieldMult,
            pierce, chain);
    }

    /// <summary>Roll a turret crit for this shot (research-driven).</summary>
    internal float CritRoll(float dmg)
        => Mods.TurretCritChance > 0f && Rng.Chance(Mods.TurretCritChance) ? dmg * Mods.TurretCritMult : dmg;

    private WaveDef GetWave(int index)
    {
        if (index < Mission.Waves.Count) return Mission.Waves[index];
        return GenerateEndlessWave(index);
    }

    /// <summary>Procedural wave for endless mode. Deterministic from the mission seed + wave index.</summary>
    private WaveDef GenerateEndlessWave(int index)
    {
        var rng = new DetRandom(Mission.Seed ^ (0xA24BAED4963EE407UL + (ulong)index));
        float budget = 10f + index * 4.5f;
        var w = new WaveDef();

        // weight table by threat; heavies unlock as depth grows
        (string id, int weight, int minWave)[] table =
        {
            ("skiff", 60, 0), ("interceptor", 25, 2), ("leech", 15, 4), ("phase_runner", 14, 6),
            ("hauler", 22, 1), ("aegis_cruiser", 20, 3), ("bombard", 18, 4),
            ("warden", 12, 7), ("carrier", 10, 6), ("siege_crawler", 12, 8),
        };
        float wcost(string id) => id switch
        { "skiff" => 1, "interceptor" => 2, "leech" => 2, "phase_runner" => 3, "hauler" => 6,
          "aegis_cruiser" => 6, "bombard" => 5, "warden" => 6, "carrier" => 12, "siege_crawler" => 8, _ => 3 };

        int guard = 0;
        while (budget > 1f && guard++ < 14)
        {
            var avail = new List<(string, int)>();
            int total = 0;
            foreach (var t in table)
                if (index >= t.minWave && _enemyDefIndex.ContainsKey(t.id)) { avail.Add((t.id, t.weight)); total += t.weight; }
            if (avail.Count == 0) break;
            int roll = rng.NextInt(total);
            string pick = avail[0].Item1;
            foreach (var (id, wt) in avail) { if (roll < wt) { pick = id; break; } roll -= wt; }

            float c = wcost(pick);
            int count = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(budget, budget * (pick == "skiff" ? 0.5f : 0.35f)) / c));
            count = Mathf.Min(count, pick == "skiff" ? 40 : 8);
            budget -= count * c;
            w.Groups.Add(new WaveEntry
            {
                Enemy = pick,
                Count = count,
                Interval = pick == "skiff" ? 0.35f : 1.6f,
                StartDelay = rng.NextFloat(0f, 4f),
                ArcCenterDeg = rng.NextInt(4) == 0 ? rng.NextInt(12) * 30 : -1,
                ArcSpreadDeg = 70,
            });
        }
        return w;
    }

    private void BeginWave()
    {
        var wave = GetWave(WaveIndex);
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
            ResearchDataEarned += B.ResearchDataPerWave * Mods.ResearchDataGainMult;
            XpEarned += B.XpPerWave * Mods.XpGainMult;
            Credits += Mathf.RoundToInt(B.CreditsPerWave * Mods.WaveIncomeMult);
            if (WavesCleared % 4 == 0) CoresEarned += 1;
            if (Mods.PlanetRegenPerWaveFrac > 0f)
                PlanetIntegrity = Mathf.Min(PlanetIntegrityMax, PlanetIntegrity + PlanetIntegrityMax * Mods.PlanetRegenPerWaveFrac);
            Events.Push(SimEventKind.WaveCleared, Vector2.Zero, 0f, WaveIndex);

            WaveIndex++;
            if (!_endless && WaveIndex >= Mission.Waves.Count)
            {
                Phase = SimPhase.Won;
                AccrueRewards(missionClear: true);
                Events.Push(SimEventKind.MissionWon, Vector2.Zero);
            }
            else
            {
                Phase = SimPhase.Build;
                PhaseTimer = B.BuildPhaseSeconds;
                OfferDraftAfterWave();
                // top up hull a little between waves so a bad wave isn't a death sentence
                if (Hero.Alive) Hero.Hull = Mathf.Min(Hero.MaxHull, Hero.Hull + Hero.MaxHull * 0.15f);
            }
        }
    }

    private void AccrueRewards(bool missionClear)
    {
        if (missionClear)
        {
            ResearchDataEarned += B.ResearchDataMissionClear * Mods.ResearchDataGainMult;
            XpEarned += B.XpMissionClear * Mods.XpGainMult;
            CoresEarned += 2;
            AlloyEarned += 5;           // mission first-clear alloy; AppRoot only banks it once
        }
        // Sentinel Core gain multiplier + Exotic Alloy gain multiplier applied here at the end
        CoresEarned = Mathf.RoundToInt(CoresEarned * Mods.SentinelCoreGainMult);
        AlloyEarned = Mathf.RoundToInt(AlloyEarned * Mods.ExoticAlloyGainMult);
        // on a loss, keep only LossRewardFrac of the run's rewards
        if (!missionClear && Mods.LossRewardFrac < 1f)
        {
            ResearchDataEarned *= Mods.LossRewardFrac;
            XpEarned *= Mods.LossRewardFrac;
        }
    }

    // ---- pool helpers ----
    internal int SpawnEnemy(int missionEnemyDefIndex, Vector2 pos)
    {
        int idx;
        if (_freeEnemies.Count > 0) idx = _freeEnemies.Pop();
        else if (EnemyHighWater < EnemyCap) idx = EnemyHighWater++;
        else return -1;

        var def = _missionEnemyDefs[missionEnemyDefIndex];
        float sc = EndlessScale;                    // 1.0 for normal missions
        ref var e = ref Enemies[idx];
        uint nextGen = e.Gen + 1;
        e = default;
        e.Alive = true;
        e.Gen = nextGen;
        e.DefIndex = missionEnemyDefIndex;
        e.Pos = pos;
        e.Hp = def.MaxHp * sc;
        e.MaxHp = def.MaxHp * sc;
        e.Shield = def.ShieldHp * sc;
        e.MaxShield = def.ShieldHp * sc;
        e.Armor = def.Armor;
        e.Radius = def.Radius;
        e.ContactDamage = def.ContactDamage * Mathf.Sqrt(sc);
        e.Bounty = Mathf.RoundToInt(def.Bounty * Mathf.Sqrt(sc));
        e.DistToCenter = pos.Length();
        e.BaseSpeed = def.Speed;
        e.SlowFactor = 1f;
        e.LeechSlot = -1;
        e.AttackTimer = def.RangedInterval;
        e.SpawnTimer = def.SpawnInterval;
        e.BlinkTimer = def.BlinkInterval;
        e.MechanicTimer = def.MechanicInterval;
        if (def.Class == "boss") _bossHandle = new EnemyHandle { Index = idx, Gen = e.Gen };
        return idx;
    }

    internal void KillEnemy(int idx, bool leaked)
    {
        ref var e = ref Enemies[idx];
        if (!e.Alive) return;
        var def = _missionEnemyDefs[e.DefIndex];

        // a leech frees its turret when it dies
        if (e.LeechSlot >= 0 && e.LeechSlot < Turrets.Length)
            Turrets[e.LeechSlot].DisabledLeft = 0f;

        e.Alive = false;
        e.Gen++;
        _freeEnemies.Push(idx);
        _aliveThisWave--;
        if (leaked) Stats.EnemiesLeaked++;
        else
        {
            Stats.EnemiesKilled++;
            Credits += e.Bounty;

            // Salvage Beacon: kills inside the field pay bonus RD + XP
            if (SalvageActiveLeft > 0f && e.Pos.DistanceSquaredTo(SalvageAnchor) <= SalvageRadius * SalvageRadius)
            {
                ResearchDataEarned += e.Bounty * SalvageBonusFrac;
                XpEarned += e.Bounty * SalvageBonusFrac * 0.5f;
            }

            if (def.Class == "boss")
            {
                CoresEarned += Mathf.Max(1, def.CoreDrop);
                AlloyEarned += def.AlloyDrop;
                _bossHandle = EnemyHandle.None;
                Events.Push(SimEventKind.MissionWon, e.Pos); // banner cue; actual win is wave-end
            }
            Events.Push(SimEventKind.EnemyKilled, e.Pos, e.Radius);
        }
    }

    private EnemyHandle _bossHandle = EnemyHandle.None;
    public bool TryGetBoss(out Vector2 pos, out float hpFrac, out bool shielded)
    {
        pos = Vector2.Zero; hpFrac = 0f; shielded = false;
        if (!Resolve(in _bossHandle, out int bi)) return false;
        ref readonly var b = ref Enemies[bi];
        pos = b.Pos;
        hpFrac = b.MaxHp > 0 ? b.Hp / b.MaxHp : 0f;
        shielded = b.MechanicActive || b.IsShielded;
        return true;
    }

    internal bool Resolve(in EnemyHandle h, out int idx)
    {
        idx = h.Index;
        return !h.IsNone && idx < EnemyHighWater && Enemies[idx].Alive && Enemies[idx].Gen == h.Gen;
    }

    internal EnemyHandle HandleOf(int idx) => new() { Index = idx, Gen = Enemies[idx].Gen };

    internal int SpawnProjectile(byte kind, Vector2 pos, Vector2 vel, float dmg, float splash,
                                 EnemyHandle target, byte src, float life = 4f,
                                 float armorPen = 0f, float shieldMult = 1f, int pierce = 0, float slow = 0f)
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
        p.ArmorPen = armorPen;
        p.ShieldMult = shieldMult;
        p.PierceLeft = pierce;
        p.Slow = slow;
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

    /// <summary>Apply damage to an enemy. Shields soak first (with a multiplier so
    /// shield-strip weapons matter), then armour reduces the rest. Returns dealt.</summary>
    internal float DamageEnemy(int idx, float amount, DamageSource src,
                               float armorPen = 0f, float shieldMult = 1f)
    {
        ref var e = ref Enemies[idx];
        if (!e.Alive) return 0f;

        // boss invulnerable shell
        if (e.MechanicActive && _missionEnemyDefs[e.DefIndex].BossMechanic == "threshing_gate")
        {
            Events.Push(SimEventKind.EnemyHit, e.Pos, 0f);
            return 0f;
        }

        // desperation: below 30% integrity, everything hits harder (Fortification T5)
        if (Mods.DesperationBonus && PlanetIntegrity < PlanetIntegrityMax * 0.3f)
            amount *= 1.2f;

        float remaining = amount;
        float dealt = 0f;

        if (e.Shield > 0.01f)
        {
            float toShield = remaining * shieldMult;
            float soak = Mathf.Min(e.Shield, toShield);
            e.Shield -= soak;
            e.ShieldRegenTimer = 0f;
            dealt += soak;
            // overkill on the shield converts back to hull damage at the normal rate
            remaining -= soak / Mathf.Max(0.01f, shieldMult);
            if (remaining < 0f) remaining = 0f;
        }

        if (remaining > 0f)
        {
            float effArmor = Mathf.Max(0f, e.Armor - armorPen);
            float hull = Mathf.Max(1f, remaining - effArmor);
            e.Hp -= hull;
            dealt += hull;
        }

        switch (src)
        {
            case DamageSource.Turret: Stats.DamageByTurrets += dealt; break;
            case DamageSource.Hero: Stats.DamageByHero += dealt; break;
            case DamageSource.Ability: Stats.DamageByAbilities += dealt; break;
        }
        Events.Push(SimEventKind.EnemyHit, e.Pos, dealt);
        if (e.Hp <= 0f) KillEnemy(idx, leaked: false);
        return dealt;
    }

    internal void ApplySlow(int idx, float factor)
    {
        ref var e = ref Enemies[idx];
        if (!e.Alive || _missionEnemyDefs[e.DefIndex].CcImmune) return;
        if (factor < e.SlowFactor) e.SlowFactor = factor;   // strongest slow wins this tick
    }

    internal void ApplyPull(int idx, Vector2 toward, float strength)
    {
        ref var e = ref Enemies[idx];
        if (!e.Alive || _missionEnemyDefs[e.DefIndex].CcImmune) return;
        Vector2 d = toward - e.Pos;
        float len = d.Length();
        if (len < 1f) return;
        Vector2 imp = d / len * strength;
        e.PullX += imp.X;
        e.PullY += imp.Y;
    }

    internal void HealEnemy(int idx, float amount)
    {
        ref var e = ref Enemies[idx];
        if (!e.Alive) return;
        e.Hp = Mathf.Min(e.MaxHp, e.Hp + amount);
    }

    internal void ShieldEnemy(int idx, float amount)
    {
        ref var e = ref Enemies[idx];
        if (!e.Alive || e.MaxShield <= 0f) return;
        e.Shield = Mathf.Min(e.MaxShield, e.Shield + amount);
    }

    internal void DamagePlanet(float amount, bool leaked = false)
    {
        amount *= Mathf.Max(0.1f, Mods.PlanetDamageTakenMult);
        if (leaked) amount *= Mathf.Max(0.1f, Mods.LeakedDamageMult);

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

        // planet shield (Fortification "Static Envelope") soaks before integrity
        if (PlanetShield > 0.01f)
        {
            float s = Mathf.Min(PlanetShield, amount);
            PlanetShield -= s;
            amount -= s;
            if (amount <= 0f) { Events.Push(SimEventKind.PlanetHit, Vector2.Zero, s); return; }
        }

        PlanetIntegrity -= amount;
        Events.Push(SimEventKind.PlanetHit, Vector2.Zero, amount);
    }

    internal void DamageTurret(int slot, float amount)
    {
        // Phase 1/2: turrets aren't destroyed, only the planet loses integrity when
        // shelled — the turret hit just spills a fraction to the planet and flags VFX.
        if (!InSlot(slot) || !Turrets[slot].Built) { DamagePlanet(amount); return; }
        DamagePlanet(amount * 0.6f);
        Events.Push(SimEventKind.PlanetHit, Turrets[slot].Pos, amount);
    }

    internal void DisableTurret(int slot, float seconds)
    {
        if (!InSlot(slot) || !Turrets[slot].Built) return;
        if (seconds > Turrets[slot].DisabledLeft) Turrets[slot].DisabledLeft = seconds;
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

    /// <summary>"14× Skiff  ·  3× Hauler" for the build-phase preview (spec §13).</summary>
    public string NextWavePreview()
    {
        if (!_endless && WaveIndex >= Mission.Waves.Count) return "";
        var counts = new Dictionary<string, int>();
        var order = new List<string>();
        foreach (var g in GetWave(WaveIndex).Groups)
        {
            if (!Cfg.HasEnemy(g.Enemy)) continue;
            string name = Cfg.Enemy(g.Enemy).Name;
            if (!counts.ContainsKey(name)) { counts[name] = 0; order.Add(name); }
            counts[name] += g.Count;
        }
        var parts = new List<string>();
        foreach (var n in order) parts.Add($"{counts[n]}× {n}");
        return string.Join("   ·   ", parts);
    }
}
