using Godot;
using Sentinel.Config;
using Sentinel.Sim;

namespace Sentinel.Game;

/// <summary>
/// Headless harness. For every arc-1 mission: run it twice with the same seed and
/// a scripted "reasonable" player, assert the two runs are byte-identical
/// (determinism), and print a balance snapshot + timing.
///   godot --headless scenes/SimTest.tscn --quit
/// </summary>
public sealed partial class SimTest : Node
{
    private static readonly string[] Loadout = { "kinetic_barrage", "aegis_barrier", "overdrive" };

    public override void _Ready()
    {
        var cfg = ConfigDb.Load();
        bool allOk = true;
        GD.Print("mission  | outcome        waves  integ  kills  leak  ticks   rd    turr/hero/abil %   ms   det");
        GD.Print("---------|---------------------------------------------------------------------------------------");

        foreach (var m in cfg.Arc.Missions)
        {
            var a = RunOnce(cfg, m.File);
            var bRun = RunOnce(cfg, m.File);
            bool det = a.phase == bRun.phase && a.waves == bRun.waves && a.ticks == bRun.ticks
                       && a.kills == bRun.kills && Mathf.IsEqualApprox(a.integ, bRun.integ);
            allOk &= det;
            float tot = Mathf.Max(1f, a.dTur + a.dHero + a.dAbil);
            GD.Print($"{m.Id,-8} | {(a.phase == SimPhase.Won ? "WON " : "lost"),-6}  " +
                     $"{a.waves,2}/{a.total,-2}  {a.integ,5:0}  {a.kills,5}  {a.leak,4}  {a.ticks,6}  {a.rd,4:0}   " +
                     $"{a.dTur / tot * 100,3:0}/{a.dHero / tot * 100,3:0}/{a.dAbil / tot * 100,3:0}    {a.ms,4}  {(det ? "ok" : "FAIL")}");
        }

        GD.Print("---------|---------------------------------------------------------------------------------------");

        // progression: a beefy modifier set must change the outcome AND stay deterministic
        var mods = new Sentinel.Meta.ModifierSet();
        foreach (var (k, v) in new (string, float)[] {
            ("turret_damage", 0.5f), ("turret_fire_rate", 0.3f), ("planet_integrity", 0.4f),
            ("hero_missile_damage", 0.5f), ("ability_effect", 0.4f), ("ability_cooldown", -0.3f),
            ("rd_gain", 0.5f), ("turret_crit_chance", 0.15f) })
            mods.ApplyEffect(k, v);
        var baseRun = RunOnce(cfg, "res://data/missions/m07.json");
        var modA = RunOnce(cfg, "res://data/missions/m07.json", mods);
        var modB = RunOnce(cfg, "res://data/missions/m07.json", mods);
        bool modDet = modA.ticks == modB.ticks && Mathf.IsEqualApprox(modA.integ, modB.integ) && modA.kills == modB.kills;
        bool modChanged = modA.ticks != baseRun.ticks || !Mathf.IsEqualApprox(modA.rd, baseRun.rd);
        GD.Print($"progression: base m07 integ={baseRun.integ:0} rd={baseRun.rd:0}  |  +mods integ={modA.integ:0} rd={modA.rd:0}  " +
                 $"deterministic={(modDet ? "ok" : "FAIL")}  changed-outcome={(modChanged ? "ok" : "FAIL")}");
        allOk &= modDet && modChanged;

        // endless: run deep, twice, check determinism + that it actually escalates
        var e1 = RunOnce(cfg, "res://data/missions/endless.json");
        var e2 = RunOnce(cfg, "res://data/missions/endless.json");
        bool eDet = e1.ticks == e2.ticks && e1.waves == e2.waves && Mathf.IsEqualApprox(e1.integ, e2.integ);
        GD.Print($"endless: reached wave {e1.waves + 1}   kills {e1.kills}   ticks {e1.ticks}   rd {e1.rd:0}   " +
                 $"deterministic={(eDet ? "ok" : "FAIL")}");
        allOk &= eDet && e1.waves >= 1;

        // weekly challenge: the endless mission under this week's seed + twist, run twice
        var wk = Sentinel.Meta.WeeklyChallenge.Current();
        var wkMission = cfg.LoadMission("res://data/missions/endless.json") with
            { Id = "weekly", Seed = wk.Seed };
        var wkMods = new Sentinel.Meta.ModifierSet();
        foreach (var (k, v) in wk.Twist.PlayerEffects) wkMods.ApplyEffect(k, v);
        var w1 = RunOnce(cfg, wkMission, wkMods, wk.Twist);
        var w2 = RunOnce(cfg, wkMission, wkMods, wk.Twist);
        bool wkDet = w1.ticks == w2.ticks && w1.waves == w2.waves && Mathf.IsEqualApprox(w1.integ, w2.integ);
        GD.Print($"weekly: {wk.Id} \"{wk.MutatorName}\"  reached wave {w1.waves + 1}  kills {w1.kills}  " +
                 $"deterministic={(wkDet ? "ok" : "FAIL")}");
        allOk &= wkDet && w1.waves >= 1;

        // speed-independence: the fixed-step clock must produce a byte-identical sim
        // whether the frames are stepped at 1x or 4x (design-spec §2 / balance-pass gate)
        var sp1 = ClockRun(cfg, 1);
        var sp4 = ClockRun(cfg, 4);
        bool spDet = sp1.ticks == sp4.ticks && sp1.kills == sp4.kills
                     && Mathf.IsEqualApprox(sp1.integ, sp4.integ) && sp1.phase == sp4.phase;
        GD.Print($"speed 1x vs 4x: 1x integ={sp1.integ:0} ticks={sp1.ticks} kills={sp1.kills}  |  " +
                 $"4x integ={sp4.integ:0} ticks={sp4.ticks} kills={sp4.kills}  identical={(spDet ? "ok" : "FAIL")}");
        allOk &= spDet;

        GD.Print(allOk ? "ALL CHECKS OK" : "SOME CHECKS FAILED");
        GetTree().Quit(allOk ? 0 : 1);
    }

    /// <summary>Run m05 for ~4 minutes of game time, feeding 1/60s "frames" through a
    /// real <see cref="SimClock"/> at the given speed. Inputs are tick-indexed, so the
    /// result must not depend on how many ticks a frame advances.</summary>
    private static R ClockRun(ConfigDb cfg, int speed)
    {
        var w = new SimWorld(cfg);
        w.Load(cfg.LoadMission("res://data/missions/m05.json"), Loadout, null, null, null, null);
        var clock = new SimClock();
        clock.SetSpeed(speed);

        void Tick()
        {
            long tk = w.Tick;                 // the tick about to run
            if (w.Phase == SimPhase.Build) { Build(w); w.Enqueue(SimCommand.Wave()); }
            else WaveInputs(w, tk);
            w.StepTick();
        }

        int frames = 60 * 240 / speed + 20;
        for (int f = 0; f < frames; f++)
        {
            clock.Advance(1f / 60f, Tick);
            if (w.Phase is SimPhase.Won or SimPhase.Lost) break;
        }
        var s = w.Stats;
        int total = w.IsSurvival ? Mathf.FloorToInt(w.Mission.Duration) : w.WaveCount;
        return new R(w.Phase, w.WavesCleared, total, w.PlanetIntegrity, s.EnemiesKilled, s.EnemiesLeaked,
            s.TicksElapsed, w.ResearchDataEarned, s.DamageByTurrets, s.DamageByHero, s.DamageByAbilities, 0);
    }

    private readonly record struct R(SimPhase phase, int waves, int total, float integ, int kills, int leak,
        long ticks, float rd, float dTur, float dHero, float dAbil, long ms);

    private static R RunOnce(ConfigDb cfg, string file, Sentinel.Meta.ModifierSet? mods = null)
        => RunOnce(cfg, cfg.LoadMission(file), mods, null);

    private static R RunOnce(ConfigDb cfg, Config.MissionDef mission,
                             Sentinel.Meta.ModifierSet? mods, Config.AscensionTierDef? asc)
    {
        var w = new SimWorld(cfg);
        w.Load(mission, Loadout, mods, null, null, asc);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long wt = 0;
        int guard = 0;
        while (w.Phase is SimPhase.Build or SimPhase.Wave && guard++ < 60 * 60 * 40)
        {
            if (w.Phase == SimPhase.Build) { Build(w); w.Enqueue(SimCommand.Wave()); w.StepTick(); }
            else { WaveInputs(w, wt++); w.StepTick(); }
        }
        sw.Stop();
        var s = w.Stats;
        int total = w.IsSurvival ? Mathf.FloorToInt(w.Mission.Duration) : w.WaveCount;
        return new R(w.Phase, w.WavesCleared, total, w.PlanetIntegrity, s.EnemiesKilled, s.EnemiesLeaked,
            s.TicksElapsed, w.ResearchDataEarned, s.DamageByTurrets, s.DamageByHero, s.DamageByAbilities, sw.ElapsedMilliseconds);
    }

    // a plausible player opening / fill order — a spread, not autocannon spam
    private static readonly string[] BuildPlan =
    {
        "autocannon", "flak", "autocannon", "railgun", "tesla", "missile_silo",
        "autocannon", "flak", "railgun", "laser_lattice", "autocannon", "graviton",
    };

    private static void Build(SimWorld w)
    {
        // buy what we can afford, cheapest-useful first, then upgrade spare credits
        for (int s = 0; s < w.TurretView.Length; s++)
            if (!w.TurretView[s].Built && w.Credits > 260)
                w.Enqueue(SimCommand.Build(s, BuildPlan[s % BuildPlan.Length]));
        for (int i = 0; i < 4; i++) w.StepTick();
        for (int s = 0; s < w.TurretView.Length; s++)
        {
            int c = w.TurretUpgradeCost(s);
            if (c > 0 && w.Credits > c + 180) w.Enqueue(SimCommand.Upgrade(s));
        }
        for (int i = 0; i < 4; i++) w.StepTick();
    }

    private static void WaveInputs(SimWorld w, long tick)
    {
        // survival: take the first offered upgrade whenever a draft is waiting
        if (w.HasPendingDraft && w.DraftOptionIndices.Count > 0)
            w.Enqueue(SimCommand.Card(w.DraftOptionIndices[0]));

        // survival: keep managing the base in real time — fill empty slots, then upgrade
        if (w.IsSurvival && tick % 45 == 0)
        {
            var turrets = w.TurretView;
            int emptied = -1;
            for (int s = 0; s < turrets.Length; s++)
                if (!turrets[s].Built) { emptied = s; break; }
            if (emptied >= 0)
                w.Enqueue(SimCommand.Build(emptied, BuildPlan[emptied % BuildPlan.Length]));
            else
                for (int s = 0; s < turrets.Length; s++)
                {
                    int c = w.TurretUpgradeCost(s);
                    if (c > 0 && w.Credits > c + 150) { w.Enqueue(SimCommand.Upgrade(s)); break; }
                }
        }

        if (w.HeroView.Alive && w.HeroView.VolleyCooldownLeft <= 0f)
            w.Enqueue(SimCommand.Volley(Threat(w) * 300f));
        var ab = w.AbilityView;
        for (int i = 0; i < ab.Length; i++)
            if (ab[i].DefIndex >= 0 && ab[i].CooldownLeft <= 0f)
                w.Enqueue(SimCommand.Cast(i, Threat(w) * 380f));
        if (tick % 10 == 0) w.Enqueue(SimCommand.HeroTarget(Threat(w) * 280f));
    }

    private static Vector2 Threat(SimWorld w)
    {
        var en = w.EnemyView;
        Vector2 best = Vector2.Up; float bd = float.MaxValue;
        for (int i = 0; i < en.Length; i++)
        {
            if (!en[i].Alive) continue;
            float d = en[i].Pos.LengthSquared();
            if (d < bd) { bd = d; best = en[i].Pos.Normalized(); }
        }
        return best;
    }
}
