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

            if (t.DisabledLeft > 0f) { t.DisabledLeft -= dt; t.Target = EnemyHandle.None; continue; }

            var def = TurretDefs[t.DefIndex];
            var st = StatsFor(in t);

            if (def.Fire == "support") { StepSupportTurret(s, in def, in st); continue; }

            if (t.CooldownLeft > 0f) t.CooldownLeft -= dt;

            int tgt = AcquireTarget(t.Pos, t.Angle, def, st.Range);

            // laser lattice ramp bookkeeping
            if (def.Fire == "beam")
            {
                bool sameTarget = tgt >= 0 && !t.Target.IsNone && t.Target.Index == tgt && Enemies[tgt].Alive;
                if (tgt >= 0 && (sameTarget || t.Target.IsNone))
                {
                    t.RampStacks = Mathf.Min(def.RampMax, t.RampStacks + def.RampPerSecond * dt);
                    t.RampGraceLeft = 3f;
                }
                else
                {
                    t.RampGraceLeft -= dt;
                    if (t.RampGraceLeft <= 0f) t.RampStacks = Mathf.MoveToward(t.RampStacks, 0f, def.RampPerSecond * dt);
                }
            }

            if (tgt < 0) { t.Target = EnemyHandle.None; continue; }
            t.Target = HandleOf(tgt);

            switch (def.Fire)
            {
                case "beam":
                    // continuous DPS, hitscan (never misses evasive units)
                    float dps = st.Damage * (1f + t.RampStacks);
                    float dealt = DamageEnemy(tgt, dps * dt, DamageSource.Turret, st.ArmorPen, st.ShieldMult);
                    t.DamageDealt += dealt;
                    Events.PushLine(SimEventKind.BeamTick, t.Pos, Enemies[tgt].Pos, 1f + t.RampStacks, s);
                    break;

                case "chain":
                    if (t.CooldownLeft <= 0f)
                    {
                        FireChain(s, in st, tgt);
                        t.CooldownLeft = st.FireInterval;
                    }
                    break;

                default: // projectile
                    if (t.CooldownLeft <= 0f)
                    {
                        FireProjectileTurret(s, in def, in st, tgt);
                        t.CooldownLeft = st.FireInterval;
                    }
                    break;
            }
        }
    }

    private void StepSupportTurret(int slot, in Config.TurretDef def, in TurretStats st)
    {
        // Nanite Forge: passive buff to adjacent built turrets, no targeting.
        // The buff is read by neighbours in FireProjectileTurret via SupportBonus().
        // Nothing to do per-tick beyond existing; a light pulse event for VFX.
        if ((Tick % 30) == 0) Events.Push(SimEventKind.AbilityCast, Turrets[slot].Pos, 0f, -2);
    }

    private float SupportBonusAt(Vector2 pos)
    {
        float bonus = 0f;
        for (int s = 0; s < Turrets.Length; s++)
        {
            ref readonly var t = ref Turrets[s];
            if (!t.Built) continue;
            var def = TurretDefs[t.DefIndex];
            if (def.Fire != "support" || t.DisabledLeft > 0f) continue;
            if (t.Pos.DistanceTo(pos) <= def.SupportRange + 1f)
                bonus += def.SupportBuff * Mathf.Pow(B.TurretUpgradeStatMult, t.Level - 1);
        }
        return bonus;
    }

    private int AcquireTarget(Vector2 pos, float facing, Config.TurretDef def, float range)
    {
        float rangeSq = range * range;
        float halfArc = Mathf.DegToRad(def.ArcDegrees) * 0.5f;
        var mode = ParseTargetMode(def.TargetMode);

        int best = -1;
        float bestScore = mode == TargetMode.Strongest ? float.MinValue : float.MaxValue;

        for (int i = 0; i < EnemyHighWater; i++)
        {
            ref readonly var e = ref Enemies[i];
            if (!e.Alive) continue;

            Vector2 rel = e.Pos - pos;
            if (rel.LengthSquared() > rangeSq) continue;
            if (Mathf.Abs(Mathf.AngleDifference(facing, rel.Angle())) > halfArc) continue;

            float score = mode switch
            {
                TargetMode.First => e.DistToCenter,
                TargetMode.Closest => rel.LengthSquared(),
                TargetMode.Strongest => e.Hp + e.Shield,
                _ => e.DistToCenter,
            };
            bool better = mode == TargetMode.Strongest ? score > bestScore : score < bestScore;
            if (better) { bestScore = score; best = i; }
        }
        return best;
    }

    private void FireProjectileTurret(int slot, in Config.TurretDef def, in TurretStats st, int enemyIdx)
    {
        ref readonly var e = ref Enemies[enemyIdx];
        Vector2 turretPos = Turrets[slot].Pos;

        float dmg = CritRoll(st.Damage * (1f + SupportBonusAt(turretPos)));
        float dist = turretPos.DistanceTo(e.Pos);
        float tHit = def.ProjectileSpeed > 1f ? dist / def.ProjectileSpeed : 0f;
        Vector2 aimPos = e.Pos + e.Vel * tHit;
        Vector2 dir = (aimPos - turretPos).Normalized();
        if (dir == Vector2.Zero) dir = Vector2.FromAngle(Turrets[slot].Angle);

        EnemyHandle homing = def.Homing ? HandleOf(enemyIdx) : EnemyHandle.None;
        float ps = def.ProjectileSpeed;
        // homing turret rounds (missile silo) get the guided flight model; dumb rounds keep their flat trajectory
        SpawnProjectile(0, turretPos, dir * (def.Homing ? ps * 0.6f : ps), dmg, st.Splash, homing, (byte)slot,
                        life: def.Homing ? 3.6f : 3f, armorPen: st.ArmorPen, shieldMult: st.ShieldMult, pierce: st.Pierce,
                        slow: def.SlowOnHit,
                        speedMax: def.Homing ? ps * 1.15f : 0f,
                        accel: def.Homing ? ps * 2.2f : 0f,
                        agility: def.Homing ? 5.5f : 0f);
        Events.PushLine(SimEventKind.TurretFired, turretPos, aimPos, def.SplashRadius > 0f ? 1f : 0f, slot);
    }

    private void FireChain(int slot, in TurretStats st, int firstIdx)
    {
        Vector2 origin = Turrets[slot].Pos;
        int jumps = 1 + st.ChainJumps;
        int cur = firstIdx;
        Vector2 from = origin;
        System.Span<bool> hitFlag = stackalloc bool[64];

        for (int j = 0; j < jumps && cur >= 0; j++)
        {
            float dealt = DamageEnemy(cur, CritRoll(st.Damage), DamageSource.Turret, st.ArmorPen, st.ShieldMult);
            Turrets[slot].DamageDealt += dealt;
            Events.PushLine(SimEventKind.ChainArc, from, Enemies[cur].Pos);
            if (cur < 64) hitFlag[cur] = true;

            Vector2 anchor = Enemies[cur].Pos;
            int next = -1; float bestD = 90f * 90f; // chain range
            for (int i = 0; i < EnemyHighWater; i++)
            {
                if (!Enemies[i].Alive || (i < 64 && hitFlag[i])) continue;
                float d = Enemies[i].Pos.DistanceSquaredTo(anchor);
                if (d < bestD) { bestD = d; next = i; }
            }
            from = anchor;
            cur = next;
        }
    }

    private static TargetMode ParseTargetMode(string s) => s switch
    {
        "closest" => TargetMode.Closest,
        "strongest" => TargetMode.Strongest,
        _ => TargetMode.First,
    };
}
