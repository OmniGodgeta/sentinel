using Godot;

namespace Sentinel.Sim;

public sealed partial class SimWorld
{
    private Vector2 _heroTarget;

    private void StepHero()
    {
        float dt = SimClock.TickDelta;
        ref var h = ref Hero;

        // respawn
        if (!h.Alive)
        {
            h.RespawnLeft -= dt;
            if (h.RespawnLeft <= 0f)
            {
                h.Alive = true;
                h.Hull = h.MaxHull;
                h.Pos = new Vector2(0, -h.OrbitRadius);
                _heroTarget = h.Pos;
            }
            return;
        }

        // cooldowns / buffs run in game-time
        if (h.VolleyCooldownLeft > 0f) h.VolleyCooldownLeft -= dt;
        if (h.OverdriveLeft > 0f)
        {
            h.OverdriveLeft -= dt;
            if (h.OverdriveLeft <= 0f) h.OverdriveVolleyMult = 1f;
        }

        // movement: glide toward the drag target, staying on the orbit band
        Vector2 to = _heroTarget - h.Pos;
        float step = Cfg.Hero.MoveSpeed * Mathf.Max(0.2f, Mods.HeroMoveSpeedMult) * dt;
        if (to.Length() <= step) h.Pos = _heroTarget;
        else h.Pos += to.Normalized() * step;
        h.Pos = ClampToOrbitBand(h.Pos);
        h.OrbitRadius = h.Pos.Length();

        // point-defense auto-fire: continuous DPS to the closest enemy in range
        int closest = ClosestEnemyTo(h.Pos, Cfg.Hero.PointDefenseRange);
        if (closest >= 0)
        {
            DamageEnemy(closest, Cfg.Hero.PointDefenseDps * Mods.HeroPointDefenseMult * dt, DamageSource.Hero);
        }

        // Sentinel Deployment: escort drones chew on the nearest few enemies
        if (DronesActiveLeft > 0f)
        {
            System.Span<int> picks = stackalloc int[8];
            int n = NearestEnemiesTo(h.Pos, System.Math.Min(DroneCount, 8), picks);
            for (int k = 0; k < n; k++)
                if (Enemies[picks[k]].Pos.DistanceTo(h.Pos) <= DroneRange)
                    DamageEnemy(picks[k], DroneDps * dt, DamageSource.Hero);
        }
    }

    private void TryFireVolley(Vector2 aim)
    {
        ref var h = ref Hero;
        if (!h.Alive || h.VolleyCooldownLeft > 0f) return;

        int missiles = Cfg.Hero.VolleyMissiles + Mods.HeroExtraMissiles + (Mods.HeroDoubleSalvo ? Cfg.Hero.VolleyMissiles : 0);
        float dmg = Cfg.Hero.MissileDamage * Mathf.Max(0.2f, Mods.HeroMissileDamageMult);
        float speed = Cfg.Hero.MissileSpeed;
        float splash = Cfg.Hero.MissileSplashRadius;

        // target the enemies nearest the aim point; leftover missiles fly to the point
        System.Span<int> picks = stackalloc int[16];
        int n = NearestEnemiesTo(aim, System.Math.Min(missiles, 16), picks);

        for (int m = 0; m < missiles; m++)
        {
            Vector2 dir;
            EnemyHandle tgt = EnemyHandle.None;
            if (m < n)
            {
                tgt = HandleOf(picks[m]);
                dir = (Enemies[picks[m]].Pos - h.Pos).Normalized();
            }
            else
            {
                dir = (aim - h.Pos).Normalized();
                if (dir == Vector2.Zero) dir = Vector2.Down;
            }
            // wider fan + slow launch so the volley blooms outward before the
            // motors light and the missiles curve back onto their marks
            dir = dir.Rotated(Rng.NextFloat(-0.35f, 0.35f));
            SpawnProjectile(kind: 1, h.Pos, dir * (speed * 0.5f), dmg, splash, tgt, src: 255, life: 5f,
                            speedMax: speed * 1.35f, accel: speed * 2.4f, agility: 8f);
        }

        float cd = Mathf.Max(11f, Cfg.Hero.VolleyCooldown + Mods.HeroMissileCdAdd);
        h.VolleyCooldownLeft = cd * h.OverdriveVolleyMult;
        Events.Push(SimEventKind.VolleyLaunched, h.Pos, missiles);
    }

    internal void HeroTakeDamage(float amount)
    {
        ref var h = ref Hero;
        if (!h.Alive) return;
        h.Hull -= amount;
        Events.Push(SimEventKind.HeroHit, h.Pos, amount);
        if (h.Hull <= 0f)
        {
            h.Hull = 0f;
            h.Alive = false;
            h.RespawnLeft = Mods.HeroRespawnSeconds >= 0f ? Mods.HeroRespawnSeconds : Cfg.Hero.RespawnSeconds;
            Events.Push(SimEventKind.HeroDown, h.Pos);
        }
    }

    // ---- queries ----
    private int ClosestEnemyTo(Vector2 p, float maxRange)
    {
        float best = maxRange * maxRange;
        int bestI = -1;
        for (int i = 0; i < EnemyHighWater; i++)
        {
            ref readonly var e = ref Enemies[i];
            if (!e.Alive) continue;
            float d = p.DistanceSquaredTo(e.Pos);
            if (d < best) { best = d; bestI = i; }
        }
        return bestI;
    }

    /// <summary>Fills <paramref name="outIdx"/> with up to <paramref name="want"/>
    /// distinct enemy indices nearest to <paramref name="p"/>. Simple selection
    /// (want is tiny). Returns the count written.</summary>
    private int NearestEnemiesTo(Vector2 p, int want, System.Span<int> outIdx)
    {
        int written = 0;
        System.Span<float> bestD = stackalloc float[16];
        for (int k = 0; k < want && k < outIdx.Length; k++) { outIdx[k] = -1; bestD[k] = float.MaxValue; }

        for (int i = 0; i < EnemyHighWater; i++)
        {
            ref readonly var e = ref Enemies[i];
            if (!e.Alive) continue;
            float d = p.DistanceSquaredTo(e.Pos);
            // insertion into the small sorted best list
            for (int k = 0; k < want; k++)
            {
                if (d < bestD[k])
                {
                    for (int j = want - 1; j > k; j--) { bestD[j] = bestD[j - 1]; outIdx[j] = outIdx[j - 1]; }
                    bestD[k] = d; outIdx[k] = i;
                    if (written < want) written++;
                    break;
                }
            }
        }
        // compact out any -1 (fewer enemies than wanted)
        int real = 0;
        for (int k = 0; k < want; k++) if (outIdx[k] >= 0) outIdx[real++] = outIdx[k];
        return real;
    }
}
