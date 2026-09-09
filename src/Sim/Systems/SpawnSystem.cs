using Godot;

namespace Sentinel.Sim;

public sealed partial class SimWorld
{
    private void StepSpawns()
    {
        float t = PhaseTimer;
        while (_spawnCursor < _pending.Count && _pending[_spawnCursor].Time <= t)
        {
            var ticket = _pending[_spawnCursor];
            _spawnCursor++;

            Vector2 pos = new Vector2(Mathf.Cos(ticket.Angle), Mathf.Sin(ticket.Angle)) * B.SpawnRadius;
            int idx = SpawnEnemy(ticket.EnemyDefIndex, pos);
            if (idx < 0) continue; // pool full — skip (should never happen with EnemyCap)

            // aim straight at the planet centre; a tiny tangential jitter keeps
            // stacked spawns from overlapping into one pixel.
            Vector2 toCenter = (-pos).Normalized();
            float jitter = Rng.NextFloat(-0.05f, 0.05f);
            toCenter = toCenter.Rotated(jitter);
            Enemies[idx].Vel = toCenter * EnemyDefAt(ticket.EnemyDefIndex).Speed;
            _aliveThisWave++;
        }
    }
}
