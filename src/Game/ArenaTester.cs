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

        while (running && w.Tick < maxTicks)
        {
            if (w.Phase == SimPhase.Build) 
            {
                w.Enqueue(SimCommand.Wave());
            }
            
            w.StepTick();
            tickCount++;

            if (w.Phase is SimPhase.Won or SimPhase.Lost)
            {
                running = false;
            }

            if (tickCount % 1000 == 0)
            {
                Console.WriteLine($"Tick {w.Tick}: Phase={w.Phase}, Waves={w.WavesCleared}, Integrity={w.PlanetIntegrity:0:F1}");
            }
        }

        Console.WriteLine("--- ARENA TEST RESULTS ---");
        Console.WriteLine($"Final Phase: {w.Phase}");
        Console.WriteLine($"Final Waves: {w.WavesCleared}");
        Console.WriteLine($"Final Integrity: {w.PlanetIntegrity:0:F1}");
        Console.WriteLine($"Final Kills: {w.Stats.EnemiesKilled}");
        Console.WriteLine($"Total Ticks: {w.Tick}");
        
        if (w.Phase is SimPhase.Won or SimPhase.Lost)
        {
            GetTree().Quit(0);
        }
        else
        {
            Console.WriteLine("Simulation timed out.");
            GetTree().Quit(1);
        }
    }
}
