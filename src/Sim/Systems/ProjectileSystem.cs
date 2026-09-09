using Godot;

namespace Sentinel.Sim;

public sealed partial class SimWorld
{
    private const float MissileTurnRate = 5.5f;

    private void StepProjectiles()
    {
        float dt = SimClock.TickDelta;
        float despawnSq = B.DespawnRadius * B.DespawnRadius;
        float planetSq = (B.PlanetRadius + 4f) * (B.PlanetRadius + 4f);

        for (int i = 0; i < ProjHighWater; i++)
        {
            ref var p = ref Projectiles[i];
            if (!p.Alive) continue;

            p.Life -= dt;
            p.Age += dt;
            if (p.Life <= 0f) { DespawnProjectile(i); continue; }

            // homing (hero missiles + planet battery + homing turrets)
            if (p.Target.Index >= 0)
            {
                if (Resolve(in p.Target, out int ti))
                {
                    // motor: ramp toward cruise speed (guided missiles only)
                    float speed = p.Vel.Length();
                    if (p.SpeedMax > 0f)
                        speed = Mathf.MoveToward(speed, p.SpeedMax, p.Accel * dt);
                    speed = Mathf.Max(speed, 1f);

                    // proportional lead: steer at where the target will be, not where it is
                    Vector2 desired = InterceptDir(Enemies[ti].Pos - p.Pos, Enemies[ti].Vel, speed);

                    float agility = p.Agility > 0f ? p.Agility : MissileTurnRate;
                    // ease the turn rate in over the first third of a second so a
                    // salvo arcs out and reads as a salvo instead of snapping to target
                    agility *= Mathf.Clamp(p.Age / 0.35f, 0.2f, 1f);

                    float cur = p.Vel.Angle();
                    float turn = Mathf.Clamp(Mathf.AngleDifference(cur, desired.Angle()),
                                             -agility * dt, agility * dt);
                    p.Vel = Vector2.FromAngle(cur + turn) * speed;
                }
                else if (!p.Target.IsNone)
                {
                    int nn = ClosestEnemyTo(p.Pos, 460f);
                    p.Target = nn >= 0 ? HandleOf(nn) : EnemyHandle.None;
                }
            }
            else if (p.SpeedMax > 0f && p.Vel.LengthSquared() > 1e-4f)
            {
                // guided round with no lock (volley overflow) — still light the motor, hold heading
                float speed = Mathf.MoveToward(p.Vel.Length(), p.SpeedMax, p.Accel * dt);
                p.Vel = p.Vel.Normalized() * speed;
            }

            Vector2 prev = p.Pos;
            p.Pos += p.Vel * dt;

            // enemy shells hit the planet — Point Defense Grid intercepts them
            if (p.Kind == 2)
            {
                if (PdgActiveLeft > 0f && p.Pos.Length() <= PdgRadius)
                {
                    Events.Push(SimEventKind.MissileImpact, p.Pos, 6f);
                    DespawnProjectile(i); continue;
                }
                if (p.Pos.LengthSquared() <= planetSq) { DamagePlanet(p.Damage); DespawnProjectile(i); continue; }
                if (p.Pos.LengthSquared() > despawnSq) { DespawnProjectile(i); continue; }
                continue;
            }

            if (p.Pos.LengthSquared() > despawnSq) { DespawnProjectile(i); continue; }

            int hit = SweepEnemy(prev, p.Pos, p.Kind);
            if (hit >= 0) ImpactProjectile(i, hit);
        }
    }

    /// <summary>
    /// Unit direction toward the intercept point, assuming both the missile (at
    /// <paramref name="missileSpeed"/>) and the target (moving at <paramref name="targetVel"/>)
    /// hold course. Falls back to pure pursuit when there is no real solution.
    /// Pure float math — deterministic, no RNG.
    /// </summary>
    private static Vector2 InterceptDir(Vector2 relPos, Vector2 targetVel, float missileSpeed)
    {
        // solve |relPos + targetVel * t| = missileSpeed * t  for the smallest t > 0
        float a = targetVel.LengthSquared() - missileSpeed * missileSpeed;
        float b = 2f * relPos.Dot(targetVel);
        float c = relPos.LengthSquared();
        float t;

        if (Mathf.Abs(a) < 1e-3f)
        {
            t = Mathf.Abs(b) > 1e-3f ? -c / b : 0f;
        }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return Safe(relPos);
            float sq = Mathf.Sqrt(disc);
            float t1 = (-b + sq) / (2f * a);
            float t2 = (-b - sq) / (2f * a);
            t = float.MaxValue;
            if (t1 > 0f && t1 < t) t = t1;
            if (t2 > 0f && t2 < t) t = t2;
            if (t == float.MaxValue) return Safe(relPos);
        }

        return Safe(relPos + targetVel * t);

        static Vector2 Safe(Vector2 v) => v.LengthSquared() > 1e-6f ? v.Normalized() : Vector2.Up;
    }

    private int SweepEnemy(Vector2 a, Vector2 b, byte kind)
    {
        Vector2 ab = b - a;
        float abLenSq = Mathf.Max(ab.LengthSquared(), 1e-5f);
        int best = -1;
        float bestT = float.MaxValue;
        float projSpeed = ab.Length() / SimClock.TickDelta;

        for (int i = 0; i < EnemyHighWater; i++)
        {
            ref readonly var e = ref Enemies[i];
            if (!e.Alive) continue;

            // phase runner is untargetable while blinked "into" the field — approximate:
            // if it just blinked this tick its BlinkTimer is near max; skip a hair
            Vector2 ac = e.Pos - a;
            float t = Mathf.Clamp(ac.Dot(ab) / abLenSq, 0f, 1f);
            Vector2 closest = a + ab * t;
            float rr = e.Radius + 3f;
            if (closest.DistanceSquaredTo(e.Pos) <= rr * rr && t < bestT)
            {
                // evasion: slow, non-hitscan turret/hero shots can whiff evasive units
                var def = _missionEnemyDefs[e.DefIndex];
                if (def.Evasion > 0f && kind != 3 && projSpeed < 520f && Rng.Chance(def.Evasion))
                    continue;
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
                {
                    float d = DamageEnemy(i, p.Damage, src, p.ArmorPen, p.ShieldMult);
                    if (p.Kind == 0 && p.SourceTurret < Turrets.Length) Turrets[p.SourceTurret].DamageDealt += d;
                    if (p.Slow > 0f) ApplySlow(i, p.Slow);
                }
            }
            Events.Push(p.Kind == 1 ? SimEventKind.MissileImpact : SimEventKind.EnemyHit, at, p.SplashRadius);
            DespawnProjectile(projIdx);
            return;
        }

        float dealt = DamageEnemy(enemyIdx, p.Damage, src, p.ArmorPen, p.ShieldMult);
        if (p.Kind == 0 && p.SourceTurret < Turrets.Length) Turrets[p.SourceTurret].DamageDealt += dealt;
        if (p.Slow > 0f) ApplySlow(enemyIdx, p.Slow);
        if (p.Kind == 1) Events.Push(SimEventKind.MissileImpact, at, 0f);

        if (p.PierceLeft > 0)
        {
            p.PierceLeft--;
            // skip past the body we just hit so we don't re-hit it next tick
            p.Pos += p.Vel.Normalized() * (Enemies[enemyIdx].Radius * 2f + 4f);
        }
        else DespawnProjectile(projIdx);
    }
}
