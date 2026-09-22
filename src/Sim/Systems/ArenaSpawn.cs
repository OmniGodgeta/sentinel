using Godot;

namespace Sentinel.Sim;

public sealed partial class SimWorld
{
    /// <summary>
    /// Spawns one full Galaxy Arena wave as an immediate burst (not the staggered
    /// ticket trickle classic missions use) from the same deterministic procedural
    /// composition endless mode uses (<see cref="GenerateEndlessWave"/>), then applies
    /// the arena archetype's HP/speed multiplier to each just-spawned enemy. Isolated
    /// from <c>SpawnSystem.cs</c>'s ticket-based path on purpose — nothing here can
    /// affect a non-arena mission's spawn behaviour or determinism.
    /// </summary>
    internal void ArenaSpawnWave(int waveNumber, float hpMult, float speedMult)
    {
        var wave = GenerateEndlessWave(waveNumber);
        _aliveThisWave = 0;
        foreach (var g in wave.Groups)
        {
            if (!_enemyDefIndex.TryGetValue(g.Enemy, out int edi)) continue;
            for (int k = 0; k < g.Count; k++)
            {
                float ang = Rng.NextAngle();
                Vector2 pos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * B.SpawnRadius;
                int idx = SpawnEnemy(edi, pos);
                if (idx < 0) continue; // pool full — skip, same as the classic spawn path

                ref var e = ref Enemies[idx];
                e.MaxHp *= hpMult;
                e.Hp *= hpMult;
                e.BaseSpeed *= speedMult;
                _aliveThisWave++;
            }
        }
    }

    public int ArenaWave => _arenaDirector?.CurrentWave ?? 0;
    public bool ArenaIsIntermission => _arenaDirector?.IsIntermission ?? false;
    public float ArenaIntermissionTimeLeft => _arenaDirector?.IntermissionTimer ?? 0f;
}
