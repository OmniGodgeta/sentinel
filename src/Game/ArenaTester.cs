using Godot;
using Sentinel.Config;
using Sentinel.Sim;
using System;

namespace Sentinel.Game;

public partial class ArenaTester : Node
{
    public override void _Ready()
    {
        var cfg = ConfigDb.Load();
        var mission = cfg.LoadMission("res://data/missions/arena_test.json");
        
        string[] loadout = { "ion_cascade", "aegis_barrier", "overdrive" };
        
        var w = new SimWorld(cfg);
        Console.WriteLine("Starting Arena Test with mission: " + mission.Id);
        w.Load(mission, loadout, null, null, null, null);

        int maxTicks = 60 * 300; // 5 minutes
        int tickCount = 0;
        bool running = true;

        // Exercise the intermission shop the way ArenaShopManager does: build one
        // turret as soon as the arena allows edits, then try the shop's upgrade call
        // at every intermission until it maxes out.
        string turretId = cfg.TurretOrder[0];
        bool built = false, wasIntermission = false;
        int upgradesBought = 0, upgradeAttempts = 0, repairsBought = 0;
        bool repairBroken = false;

        while (running && w.Tick < maxTicks)
        {
            if (w.Phase == SimPhase.Build) 
            {
                w.Enqueue(SimCommand.Wave());
            }
            
            w.StepTick();
            tickCount++;

            if (!built && w.Phase == SimPhase.Arena)
            {
                w.Enqueue(SimCommand.Build(0, turretId));
                built = true;
            }
            bool inter = w.ArenaIsIntermission;
            if (inter && !wasIntermission)
            {
                upgradeAttempts++;
                bool ok = w.TryUpgradeArenaTurret(0, out int cost);
                if (ok) upgradesBought++;
                Console.WriteLine($"Intermission {upgradeAttempts}: built={w.Turrets[0].Built} gold={w.ArenaGold} upgrade(cost {cost}) -> {(ok ? "BOUGHT" : "no")}  Lv={w.Turrets[0].Level}");
                // Turret maxed: spend the gold on planet repairs (the gold sink).
                float before = w.PlanetIntegrity;
                int goldBefore = w.ArenaGold;
                if (w.Turrets[0].Level >= 3 && w.TryArenaRepair(out int repairCost))
                {
                    repairsBought++;
                    if (w.PlanetIntegrity <= before || w.ArenaGold != goldBefore - repairCost) repairBroken = true;
                    Console.WriteLine($"  repair {repairsBought} (cost {repairCost}): integrity {before:F0} -> {w.PlanetIntegrity:F0}, gold {goldBefore} -> {w.ArenaGold}, next {w.ArenaRepairCost}");
                }
            }
            wasIntermission = inter;

            if (w.Phase is SimPhase.Won or SimPhase.Lost)
            {
                running = false;
            }

            if (tickCount % 1000 == 0)
            {
                Console.WriteLine($"Tick {w.Tick}: Phase={w.Phase}, Waves={w.WavesCleared}, Integrity={w.PlanetIntegrity:F1}");
            }
        }

        Console.WriteLine("--- ARENA TEST RESULTS ---");
        Console.WriteLine($"Final Phase: {w.Phase}");
        Console.WriteLine($"Final Waves: {w.WavesCleared}");
        Console.WriteLine($"Final Integrity: {w.PlanetIntegrity:F1}");
        Console.WriteLine($"Final Kills: {w.Stats.EnemiesKilled}");
        Console.WriteLine($"Total Ticks: {w.Tick}");
        Console.WriteLine($"Turret: built={w.Turrets[0].Built} Lv={w.Turrets[0].Level}  upgrades bought {upgradesBought}/{upgradeAttempts} intermissions");

        Console.WriteLine($"Repairs bought: {repairsBought}");
        if (repairBroken)
        {
            Console.WriteLine("FAIL: a planet repair didn't restore integrity / charge the right gold.");
            GetTree().Quit(3);
            return;
        }

        if (!w.Turrets[0].Built || (upgradeAttempts > 0 && upgradesBought == 0))
        {
            Console.WriteLine("FAIL: arena turret build/upgrade never succeeded.");
            GetTree().Quit(2);
            return;
        }
        
        // Arena is endless: still fighting at the time limit means the planet held,
        // which is a pass. Only a stall (no waves cleared) counts as a failure.
        if (w.Phase is SimPhase.Won or SimPhase.Lost or SimPhase.Arena && w.WavesCleared > 0)
        {
            GetTree().Quit(0);
        }
        else
        {
            Console.WriteLine("Simulation stalled: no waves cleared.");
            GetTree().Quit(1);
        }
    }
}
