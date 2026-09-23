using Godot;
using Sentinel.Sim;
using Sentinel.Config;
using System;
using System.Collections.Generic;

namespace Sentinel.Sim.Systems
{
    /// <summary>
    /// Manages discrete wave-based combat for the Galaxy Arena mode. 
    /// Unlike SurvivalDirector, this handles wave-start, intermission, and wave-end states.
    /// </summary>
    public partial class ArenaDirector : Node
    {
        public enum WaveArchetype { Swarm, Balanced, Elite, Siege }

        private int _currentWave;
        private bool _isIntermission;
        private float _intermissionTimer;

        // Starts at 0 so the first StartNextWave() call (from SimWorld.Load) lands on wave 1
        public const int MaxWaves = 100;
        public int CurrentWave => _currentWave;
        public bool IsIntermission => _isIntermission;
        public float IntermissionTimer => _intermissionTimer;

        private const float INTERMISSION_DURATION = 3.0f;

        private WaveArchetype GetArchetype(int wave)
        {
            if (wave % 4 == 1) return WaveArchetype.Swarm;
            if (wave % 4 == 2) return WaveArchetype.Balanced;           
            if (wave % 4 == 3) return WaveArchetype.Elite;
            return WaveArchetype.Siege;
        }

        public void StartNextWave(SimWorld world)
        {
            if (_currentWave < MaxWaves) {
                _currentWave++;
                _isIntermission = false;
                _intermissionTimer = 0f;

                // Calculate wave difficulty scaling
                float hpMultiplier = 1.0f + (CurrentWave - 1) * 0.15f;
                float speedMultiplier = 1.0f + (CurrentWave - 1) * 0.05f;

                // Apply archetype-specific shifts
                WaveArchetype archetype = GetArchetype(CurrentWave);
                switch (archetype) {
                    case WaveArchetype.Swarm:
                        hpMultiplier *= 0.8f; speedMultiplier *= 1.5f; break;
                    case WaveArchetype.Balanced:
                        hpMultiplier *= 1.1f; speedMultiplier *= 1.1f; break;
                    case WaveArchetype.Elite:
                        hpMultiplier *= 2.0f; speedMultiplier *= 0.8f; break;
                    case WaveArchetype.Siege:
                        hpMultiplier *= 1.5f; speedMultiplier *= 0.5f; break;
                }

                GD.Print($"Arena: Wave {CurrentWave} [{archetype}] (HP x{hpMultiplier:F2}, Speed x{speedMultiplier:F2})");
                world.ArenaSpawnWave(CurrentWave, hpMultiplier, speedMultiplier);
                world.Events.Push(SimEventKind.ArenaWaveStart, Vector2.Zero, 0f, CurrentWave);
            } else {
                GD.PrintErr("Arena: Maximum waves reached!");
            }
        }

        public void TriggerIntermission(SimWorld world)
        {
            _isIntermission = true;
            _intermissionTimer = INTERMISSION_DURATION;
            GD.Print($"Arena: Wave Cleared. Intermission active for {INTERMISSION_DURATION}s.");
            world.Events.Push(SimEventKind.ArenaIntermission, Vector2.Zero);
        }

        public void Update(SimWorld world, float delta)
        {
            if (_isIntermission) {
                _intermissionTimer -= delta;
                if (_intermissionTimer <= 0) {
                    StartNextWave(world);
                }
            }
        }

        public bool IsWaveActive => !_isIntermission;
    }
}