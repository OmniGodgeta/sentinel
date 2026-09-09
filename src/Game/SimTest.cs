using Godot;
using Sentinel.Config;
using Sentinel.Sim;

namespace Sentinel.Game;

/// <summary>
/// Headless harness: runs mission 1 end-to-end with a scripted opening build,
/// twice, and asserts the two runs are identical (determinism check). Also
/// prints a balance snapshot. Run with:
///   godot --headless scenes/SimTest.tscn --quit
/// </summary>
public sealed partial class SimTest : Node
{
    public override void _Ready()
    {
        var cfg = ConfigDb.Load();
        string[] loadout = { "kinetic_barrage", "aegis_barrier", "overdrive" };

        var r1 = RunOnce(cfg, loadout, seedLabel: "run A");
        var r2 = RunOnce(cfg, loadout, seedLabel: "run B");

        bool identical =
            r1.phase == r2.phase &&
            r1.waves == r2.waves &&
            Mathf.IsEqualApprox(r1.integrity, r2.integrity) &&
            r1.kills == r2.kills &&
            r1.ticks == r2.ticks &&
            Mathf.IsEqualApprox(r1.rd, r2.rd);

        GD.Print("------------------------------------------------------------");
        GD.Print(identical
            ? "DETERMINISM OK — identical outcome across two runs"
            : "DETERMINISM FAIL — runs diverged");
        GD.Print($"  A: phase={r1.phase} waves={r1.waves}/{r1.totalWaves} integ={r1.integrity:0} kills={r1.kills} leaked={r1.leaked} ticks={r1.ticks} rd={r1.rd:0} xp={r1.xp:0}");
        GD.Print($"  B: phase={r2.phase} waves={r2.waves}/{r2.totalWaves} integ={r2.integrity:0} kills={r2.kills} leaked={r2.leaked} ticks={r2.ticks} rd={r2.rd:0}");
        GD.Print($"  damage split — turrets {r1.dTur:0} hero {r1.dHero:0} abilities {r1.dAbil:0}");
        GD.Print($"  wall time for one full run: {r1.wallMs} ms  ({r1.ticks} ticks => {r1.ticks / Mathf.Max(1, r1.wallMs) } ticks/ms)");
        GD.Print("------------------------------------------------------------");

        GetTree().Quit(identical ? 0 : 1);
    }

    private readonly record struct Result(
        SimPhase phase, int waves, int totalWaves, float integrity, int kills, int leaked,
        long ticks, float rd, float xp, float dTur, float dHero, float dAbil, long wallMs);

    private static Result RunOnce(ConfigDb cfg, string[] loadout, string seedLabel)
    {
        var world = new SimWorld(cfg);
        world.Load(cfg.LoadMission("res://data/missions/mission_01.json"), loadout);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        int guard = 0;
        long waveTick = 0;
        const int guardMax = 60 * 60 * 30; // 30 sim-minutes hard cap

        while (world.Phase is SimPhase.Build or SimPhase.Wave && guard++ < guardMax)
        {
            if (world.Phase == SimPhase.Build)
            {
                ScriptedBuild(world);
                world.Enqueue(SimCommand.Wave());
                world.StepTick(); // apply the launch
            }
            else
            {
                // during a wave: cast abilities off cooldown, sweep the hero, volley
                ScriptedWaveInputs(world, waveTick++);
                world.StepTick();
            }
        }
        sw.Stop();

        var s = world.Stats;
        return new Result(world.Phase, world.WavesCleared, world.WaveCount, world.PlanetIntegrity,
            s.EnemiesKilled, s.EnemiesLeaked, s.TicksElapsed, world.ResearchDataEarned, world.XpEarned,
            s.DamageByTurrets, s.DamageByHero, s.DamageByAbilities, sw.ElapsedMilliseconds);
    }

    private static void ScriptedBuild(SimWorld w)
    {
        // fill every 2nd slot with autocannon, the rest with flak, as budget allows
        for (int s = 0; s < w.TurretView.Length; s++)
        {
            if (w.TurretView[s].Built) continue;
            string id = (s % 2 == 0) ? "autocannon" : "flak";
            w.Enqueue(SimCommand.Build(s, id));
        }
        // drain the build commands
        for (int i = 0; i < 4; i++) w.StepTick();
    }

    private static void ScriptedWaveInputs(SimWorld w, long tick)
    {
        // volley whenever ready, aimed at the planet-ward side
        if (w.HeroView.Alive && w.HeroView.VolleyCooldownLeft <= 0f)
            w.Enqueue(SimCommand.Volley(new Vector2(0, -300)));

        // cast each ability off cooldown
        var ab = w.AbilityView;
        for (int i = 0; i < ab.Length; i++)
            if (ab[i].DefIndex >= 0 && ab[i].CooldownLeft <= 0f)
                w.Enqueue(SimCommand.Cast(i, new Vector2(200, 0)));

        // orbit the hero slowly so point-defense covers different faces
        if (tick % 12 == 0)
        {
            float a = tick * 0.03f;
            w.Enqueue(SimCommand.HeroTarget(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 280f));
        }
    }
}
