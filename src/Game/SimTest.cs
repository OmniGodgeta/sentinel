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
        GD.Print(allOk ? "DETERMINISM OK across all missions" : "DETERMINISM FAILED");
        GetTree().Quit(allOk ? 0 : 1);
    }

    private readonly record struct R(SimPhase phase, int waves, int total, float integ, int kills, int leak,
        long ticks, float rd, float dTur, float dHero, float dAbil, long ms);

    private static R RunOnce(ConfigDb cfg, string file)
    {
        var w = new SimWorld(cfg);
        w.Load(cfg.LoadMission(file), Loadout);
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
        return new R(w.Phase, w.WavesCleared, w.WaveCount, w.PlanetIntegrity, s.EnemiesKilled, s.EnemiesLeaked,
            s.TicksElapsed, w.ResearchDataEarned, s.DamageByTurrets, s.DamageByHero, s.DamageByAbilities, sw.ElapsedMilliseconds);
    }

    private static void Build(SimWorld w)
    {
        // a plausible opening: ring of autocannons, a few flak, upgrade what we can afford
        string[] plan = { "autocannon", "autocannon", "flak", "autocannon", "railgun", "autocannon",
                          "autocannon", "flak", "autocannon", "tesla", "autocannon", "flak" };
        for (int s = 0; s < w.TurretView.Length; s++)
            if (!w.TurretView[s].Built)
                w.Enqueue(SimCommand.Build(s, plan[s % plan.Length]));
        for (int i = 0; i < 4; i++) w.StepTick();
        // spend spare credits upgrading built turrets
        for (int s = 0; s < w.TurretView.Length; s++)
        {
            int c = w.TurretUpgradeCost(s);
            if (c > 0 && w.Credits > c + 200) w.Enqueue(SimCommand.Upgrade(s));
        }
        for (int i = 0; i < 4; i++) w.StepTick();
    }

    private static void WaveInputs(SimWorld w, long tick)
    {
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
