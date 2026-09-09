using Godot;

namespace Sentinel.Sim;

public sealed partial class SimWorld
{
    private void StepTurrets()
    {
        float dt = SimClock.TickDelta;

        for (int s = 0; s < Turrets.Length; s++)
        {
            ref var t = ref Turrets[s];
            if (!t.Built) continue;

            var def = TurretDefs[t.DefIndex];
            if (t.CooldownLeft > 0f) t.CooldownLeft -= dt;
            if (t.CooldownLeft > 0f) continue;

            int tgt = AcquireTarget(in t, def);
            if (tgt < 0) { t.Target = EnemyHandle.None; continue; }

            t.Target = HandleOf(tgt);
            FireTurret(s, in def, tgt);
            t.CooldownLeft = def.FireInterval;
        }
    }

    private int AcquireTarget(in Turret t, Config.TurretDef def)
    {
        float rangeSq = def.Range * def.Range;
        float halfArc = Mathf.DegToRad(def.ArcDegrees) * 0.5f;
        var mode = ParseTargetMode(def.TargetMode);

        int best = -1;
        float bestScore = mode == TargetMode.Strongest ? float.MinValue : float.MaxValue;

        for (int i = 0; i < EnemyHighWater; i++)
        {
            ref readonly var e = ref Enemies[i];
            if (!e.Alive) continue;

            Vector2 rel = e.Pos - t.Pos;
            if (rel.LengthSquared() > rangeSq) continue;
            float da = Mathf.Abs(Mathf.AngleDifference(t.Angle, rel.Angle()));
            if (da > halfArc) continue;

            float score = mode switch
            {
                TargetMode.First => e.DistToCenter,             // closest to the planet = most urgent
                TargetMode.Closest => rel.LengthSquared(),
                TargetMode.Strongest => e.Hp,
                _ => e.DistToCenter,
            };
            bool better = mode == TargetMode.Strongest ? score > bestScore : score < bestScore;
            if (better) { bestScore = score; best = i; }
        }
        return best;
    }

    private void FireTurret(int slot, in Config.TurretDef def, int enemyIdx)
    {
        ref readonly var e = ref Enemies[enemyIdx];
        var turretPos = Turrets[slot].Pos;

        // simple lead: aim where the enemy will be when the slug arrives
        float dist = turretPos.DistanceTo(e.Pos);
        float tHit = def.ProjectileSpeed > 1f ? dist / def.ProjectileSpeed : 0f;
        Vector2 aimPos = e.Pos + e.Vel * tHit;
        Vector2 dir = (aimPos - turretPos).Normalized();
        if (dir == Vector2.Zero) dir = Vector2.FromAngle(Turrets[slot].Angle);

        SpawnProjectile(kind: 0, turretPos, dir * def.ProjectileSpeed,
                        def.Damage, def.SplashRadius, EnemyHandle.None, (byte)slot, life: 3f);
        Events.Push(SimEventKind.TurretFired, turretPos, 0f, slot);
    }

    private static TargetMode ParseTargetMode(string s) => s switch
    {
        "closest" => TargetMode.Closest,
        "strongest" => TargetMode.Strongest,
        _ => TargetMode.First,
    };
}
