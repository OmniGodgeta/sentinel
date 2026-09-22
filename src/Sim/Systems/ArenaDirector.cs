using Godot;
using Sentinel.Sim;
using Sentinel.Config;
using System;
using System.Collections.Generic;

namespace Sentinel.Sim.Systems;

/// <summary>
/// Manages discrete wave-based combat for the Galaxy Arena mode.
/// Unlike SurvivalDirector, this handles wave-start, intermission, and wave-end states.
/// </summary>
public partial class ArenaDirector : Node
{
    public int CurrentWave { get; private set; } = 1;
    public bool IsIntermission { get; private set; } = false;
    public float IntermissionTimer { get; private set; } = 0f;
    
    private const float INTERMISSION_DURATION = 15.0f;

    public void StartNextWave(SimWorld world)
    {
        CurrentWave++;
        IsIntermission = false;
        IntermissionTimer = 0f;
        
        // Calculate wave difficulty scaling
        // Wave 1 is baseline, each wave increases enemy HP and speed
        float hpMultiplier = 1.0f + (CurrentWave - 1) * 0.15f;
        float speedMultiplier = 1.0f + (CurrentWave - 1) * 0.05f;
        
        GD.Print($"Arena: Starting Wave {CurrentWave} (HP x{hpMultiplier:F2}, Speed x{speedMultiplier:F2})");
        
        // TODO: Apply these multipliers to the active enemy pool in SimWorld
        world.ApplyArenaScaling(hpMultiplier, speedMultiplier);
    }

    public void TriggerIntermission(SimWorld world)
    {
        IsIntermission = true;
        IntermissionTimer = INTERMISSION_DURATION;
        GD.Print($"Arena: Wave Cleared. Intermission active for {INTERMISSION_DURATION}s.");
    }

    public void Update(SimWorld world, float delta)
    {
        if (IsIntermission)
        {
            IntermissionTimer -= delta;
            if (IntermissionTimer <= 0)
            {
                StartNextWave(world);
            }
        }
    }
    
    public bool IsWaveActive() => !IsIntermission;
}
