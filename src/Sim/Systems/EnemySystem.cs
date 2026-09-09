using Godot;

namespace Sentinel.Sim;

public sealed partial class SimWorld
{
    private void StepEnemies()
    {
        float dt = SimClock.TickDelta;
        float arrival = B.PlanetRadius;

        for (int i = 0; i < EnemyHighWater; i++)
        {
            ref var e = ref Enemies[i];
            if (!e.Alive) continue;

            e.Pos += e.Vel * dt;
            e.DistToCenter = e.Pos.Length();

            if (e.DistToCenter <= arrival)
            {
                DamagePlanet(e.ContactDamage);
                KillEnemy(i, leaked: true);
            }
        }
    }
}
