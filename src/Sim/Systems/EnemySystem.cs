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
            var def = _missionEnemyDefs[e.DefIndex];

            // shield regen (after a quiet delay)
            if (e.MaxShield > 0f && e.Shield < e.MaxShield)
            {
                e.ShieldRegenTimer += dt;
                if (e.ShieldRegenTimer >= def.ShieldRegenDelay)
                    e.Shield = Mathf.Min(e.MaxShield, e.Shield + def.ShieldRegen * dt);
            }

            // radiation / burn damage-over-time
            if (e.BurnLeft > 0f)
            {
                e.BurnLeft -= dt;
                DamageEnemy(i, e.BurnDps * dt, DamageSource.Orbital);
                if (!e.Alive) continue;
            }

            // stun — held in place, no behaviour, no motion
            if (e.StunLeft > 0f)
            {
                e.StunLeft -= dt;
                e.SlowFactor = 1f;
                e.DistToCenter = e.Pos.Length();
                continue;
            }

            StepEnemyBehaviour(ref e, def, i, dt);

            // --- motion ---
            float slow = Mathf.Clamp(e.SlowFactor, 0.05f, 1f);
            float speed = e.BaseSpeed * slow;
            e.SlowFactor = 1f; // consumed; control abilities re-apply each tick

            if (!e.Standoff)
            {
                Vector2 dir = (-e.Pos);
                float len = dir.Length();
                if (len > 0.001f) dir /= len;
                e.Pos += dir * speed * dt;

                // Mini-bosses sweep around the planet as they close rather than charging
                // straight down the radius. The tangential step is an angular rate, so the
                // sweep looks the same whether it's far out or nearly on top of the planet
                // (a fixed linear speed would crawl at range and whip around up close).
                if (def.OrbitSpeedDeg != 0f && len > 0.001f)
                {
                    float ang = Mathf.DegToRad(def.OrbitSpeedDeg) * dt * slow;
                    e.Pos = e.Pos.Rotated(ang);
                }
            }
            // pull impulses (gravity snare / graviton) accumulated this tick
            if (e.PullX != 0f || e.PullY != 0f)
            {
                e.Pos += new Vector2(e.PullX, e.PullY) * dt;
                e.PullX = 0f; e.PullY = 0f;
            }

            e.DistToCenter = e.Pos.Length();

            // hull collision — the ship rams the enemy and takes contact damage back
            if (Hero.Alive)
            {
                float hitR = e.Radius + 24f;
                if (e.Pos.DistanceSquaredTo(Hero.Pos) <= hitR * hitR)
                {
                    HeroTakeDamage(Cfg.Hero.CollisionDps * dt);
                    DamageEnemy(i, Cfg.Hero.RamDps * dt, DamageSource.Hero);
                    if (!e.Alive) continue;
                }
            }

            if (e.DistToCenter <= arrival)
            {
                DamagePlanet(e.ContactDamage, leaked: true);
                KillEnemy(i, leaked: true);
            }
        }
    }

    private void StepEnemyBehaviour(ref Enemy e, Config.EnemyDef def, int idx, float dt)
    {
        // ---- ranged attacker (Bombard): stop at standoff, shell inward ----
        if (def.StandoffRange > 0f)
        {
            if (!e.Standoff && e.DistToCenter <= def.StandoffRange) e.Standoff = true;
            if (e.Standoff)
            {
                e.AttackTimer -= dt;
                if (e.AttackTimer <= 0f)
                {
                    e.AttackTimer = def.RangedInterval;

                    // A mini-boss alternates its two attacks — a lobbed bomb, then a beam
                    // fired straight down onto the planet. Alternating (rather than rolling
                    // each time) means you always see both, which is what makes it read as
                    // an elite rather than a big Bombard.
                    bool beam = def.HpSegments > 1 && ((int)e.MechanicPhase & 1) == 1;
                    e.MechanicPhase += 1f;

                    if (beam)
                    {
                        // Hitscan: no travel time, nothing to shoot down — it just lands.
                        // Hits harder than the bomb to pay for being unavoidable.
                        DamagePlanet(def.RangedDamage * 1.6f);
                        Events.PushLine(SimEventKind.BeamTick, e.Pos, Vector2.Zero, 4f, 1);
                    }
                    else
                    {
                        Vector2 dir = (-e.Pos).Normalized();
                        // aim at a nearby turret if one lies roughly ahead, else the planet
                        int slot = TurretRoughlyAhead(e.Pos, dir);
                        Vector2 aim = slot >= 0 ? Turrets[slot].Pos : Vector2.Zero;
                        Vector2 v = (aim - e.Pos).Normalized() * 260f;
                        int p = SpawnProjectile(2, e.Pos, v, def.RangedDamage, 18f, EnemyHandle.None, 254, life: 4f);
                        if (p >= 0) Projectiles[p].SourceTurret = 254;
                        Events.Push(SimEventKind.TurretFired, e.Pos, 0f, -1);
                    }
                }
            }
        }

        // ---- spawner (Carrier) ----
        if (!string.IsNullOrEmpty(def.SpawnEnemy) && _enemyDefIndex.TryGetValue(def.SpawnEnemy, out int childIdx))
        {
            e.SpawnTimer -= dt;
            if (e.SpawnTimer <= 0f)
            {
                e.SpawnTimer = def.SpawnInterval;
                for (int k = 0; k < def.SpawnCount; k++)
                {
                    Vector2 off = new Vector2(Rng.NextFloat(-14, 14), Rng.NextFloat(-14, 14));
                    int ci = SpawnEnemy(childIdx, e.Pos + off);
                    if (ci >= 0)
                    {
                        Enemies[ci].Vel = (-Enemies[ci].Pos).Normalized() * _missionEnemyDefs[childIdx].Speed;
                        _aliveThisWave++;
                    }
                }
                Events.Push(SimEventKind.AbilityCast, e.Pos, 0f, -1);
            }
        }

        // ---- blinker (Phase Runner) ----
        if (def.BlinkInterval > 0f)
        {
            e.BlinkTimer -= dt;
            if (e.BlinkTimer <= 0f)
            {
                e.BlinkTimer = def.BlinkInterval;
                Vector2 dir = (-e.Pos).Normalized();
                e.Pos += dir * def.BlinkDistance;
                e.DistToCenter = e.Pos.Length();
                Events.Push(SimEventKind.BarrageTick, e.Pos, 6f);
            }
        }

        // ---- leech ----
        if (def.Leech && e.LeechSlot < 0)
        {
            int slot = NearestBuiltTurret(e.Pos, 46f);
            if (slot >= 0)
            {
                e.LeechSlot = slot;
                DisableTurret(slot, def.LeechDisableSeconds);
                e.Standoff = true;                 // clamps onto the turret
                e.Pos = Turrets[slot].Pos;
            }
        }
        else if (def.Leech && e.LeechSlot >= 0)
        {
            // keep the turret disabled while alive; refresh
            DisableTurret(e.LeechSlot, 0.5f);
            e.Pos = Turrets[e.LeechSlot].Pos;
        }

        // ---- aura (Warden): heal + shield nearby enemies ----
        if (def.AuraRadius > 0f)
        {
            float rSq = def.AuraRadius * def.AuraRadius;
            for (int j = 0; j < EnemyHighWater; j++)
            {
                if (j == idx || !Enemies[j].Alive) continue;
                if (Enemies[j].Pos.DistanceSquaredTo(e.Pos) > rSq) continue;
                if (def.AuraHealPerSecond > 0f) HealEnemy(j, def.AuraHealPerSecond * dt);
                if (def.AuraShieldPerSecond > 0f) ShieldEnemy(j, def.AuraShieldPerSecond * dt);
            }
        }

        // ---- boss mechanic ----
        if (def.Class == "boss" && !string.IsNullOrEmpty(def.BossMechanic))
        {
            e.MechanicTimer -= dt;
            if (!e.MechanicActive && e.MechanicTimer <= 0f)
            {
                e.MechanicActive = true;
                e.MechanicTimer = def.MechanicDuration;
                Events.Push(SimEventKind.NovaPulse, e.Pos, e.Radius);
            }
            else if (e.MechanicActive && e.MechanicTimer <= 0f)
            {
                e.MechanicActive = false;
                e.MechanicTimer = def.MechanicInterval;
            }
            // bosses also crawl slowly and don't leak trivially — keep them alive longer
        }
    }

    private int TurretRoughlyAhead(Vector2 from, Vector2 dir)
    {
        int best = -1; float bestDot = 0.7f;
        for (int s = 0; s < Turrets.Length; s++)
        {
            if (!Turrets[s].Built) continue;
            Vector2 to = (Turrets[s].Pos - from);
            float len = to.Length();
            if (len < 1f) continue;
            float d = to.Dot(dir) / len;
            if (d > bestDot) { bestDot = d; best = s; }
        }
        return best;
    }

    private int NearestBuiltTurret(Vector2 p, float maxDist)
    {
        int best = -1; float bestD = maxDist * maxDist;
        for (int s = 0; s < Turrets.Length; s++)
        {
            if (!Turrets[s].Built) continue;
            float d = Turrets[s].Pos.DistanceSquaredTo(p);
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }
}
