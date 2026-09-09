using Godot;

namespace Sentinel.Sim;

public sealed partial class SimWorld
{
    private const float MissileTurnRate = 5.5f; // radians/sec

    private void StepProjectiles()
    {
        float dt = SimClock.TickDelta;
        float despawnSq = B.DespawnRadius * B.DespawnRadius;

        for (int i = 0; i < ProjHighWater; i++)
        {
            ref var p = ref Projectiles[i];
            if (!p.Alive) continue;

            p.Life -= dt;
            if (p.Life <= 0f) { DespawnProjectile(i); continue; }

            // homing (hero missiles)
            if (p.Kind == 1)
            {
                if (Resolve(in p.Target, out int ti))
                {
                    Vector2 desired = (Enemies[ti].Pos - p.Pos).Normalized();
                    float speed = p.Vel.Length();
                    float cur = p.Vel.Angle();
                    float want = desired.Angle();
                    float turn = Mathf.Clamp(Mathf.AngleDifference(cur, want), -MissileTurnRate * dt, MissileTurnRate * dt);
                    p.Vel = Vector2.FromAngle(cur + turn) * speed;
                }
                else if (!p.Target.IsNone)
                {
                    // target died — retarget to whatever is nearest, else fly on
                    int nn = ClosestEnemyTo(p.Pos, 400f);
                    p.Target = nn >= 0 ? HandleOf(nn) : EnemyHandle.None;
                }
            }

            Vector2 prev = p.Pos;
            p.Pos += p.Vel * dt;

            if (p.Pos.LengthSquared() > despawnSq) { DespawnProjectile(i); continue; }

            int hit = SweepEnemy(prev, p.Pos);
            if (hit >= 0)
            {
                ImpactProjectile(i, hit);
            }
        }
    }

    /// <summary>First enemy whose circle intersects the segment prev→next.</summary>
    private int SweepEnemy(Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float abLenSq = Mathf.Max(ab.LengthSquared(), 1e-5f);
        int best = -1;
        float bestT = float.MaxValue;

        for (int i = 0; i < EnemyHighWater; i++)
        {
            ref readonly var e = ref Enemies[i];
            if (!e.Alive) continue;
            Vector2 ac = e.Pos - a;
            float t = Mathf.Clamp(ac.Dot(ab) / abLenSq, 0f, 1f);
            Vector2 closest = a + ab * t;
            float rr = e.Radius + 3f; // projectile visual radius
            if (closest.DistanceSquaredTo(e.Pos) <= rr * rr && t < bestT)
            {
                bestT = t; best = i;
            }
        }
        return best;
    }

    private void ImpactProjectile(int projIdx, int enemyIdx)
    {
        ref var p = ref Projectiles[projIdx];
        var src = p.Kind == 1 ? DamageSource.Hero : DamageSource.Turret;
        Vector2 at = Enemies[enemyIdx].Pos;

        if (p.SplashRadius > 0f)
        {
            float rSq = p.SplashRadius * p.SplashRadius;
            for (int i = 0; i < EnemyHighWater; i++)
            {
                if (!Enemies[i].Alive) continue;
                if (Enemies[i].Pos.DistanceSquaredTo(at) <= rSq)
                    DamageEnemy(i, p.Damage, src);
            }
            Events.Push(p.Kind == 1 ? SimEventKind.MissileImpact : SimEventKind.EnemyHit, at, p.SplashRadius);
        }
        else
        {
            DamageEnemy(enemyIdx, p.Damage, src);
            if (p.Kind == 1) Events.Push(SimEventKind.MissileImpact, at, 0f);
        }

        // attribution back to the turret slot
        if (p.Kind == 0 && p.SourceTurret < Turrets.Length)
            Turrets[p.SourceTurret].DamageDealt += p.Damage;

        DespawnProjectile(projIdx);
    }
}
