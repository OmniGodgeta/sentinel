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
        float step = Cfg.Hero.MoveSpeed * dt;
        if (to.Length() <= step) h.Pos = _heroTarget;
        else h.Pos += to.Normalized() * step;
        h.Pos = ClampToOrbitBand(h.Pos);
        h.OrbitRadius = h.Pos.Length();

        // point-defense auto-fire: continuous DPS to the closest enemy in range
        int closest = ClosestEnemyTo(h.Pos, Cfg.Hero.PointDefenseRange);
        if (closest >= 0)
        {
            DamageEnemy(closest, Cfg.Hero.PointDefenseDps * dt, DamageSource.Hero);
        }
    }

    private void TryFireVolley(Vector2 aim)
    {
        ref var h = ref Hero;
        if (!h.Alive || h.VolleyCooldownLeft > 0f) return;

        int missiles = Cfg.Hero.VolleyMissiles;
        float dmg = Cfg.Hero.MissileDamage;
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
            // small fan so a volley reads as a salvo, not one dot
            dir = dir.Rotated(Rng.NextFloat(-0.12f, 0.12f));
            SpawnProjectile(kind: 1, h.Pos, dir * speed, dmg, splash, tgt, src: 255, life: 5f);
        }

        h.VolleyCooldownLeft = Cfg.Hero.VolleyCooldown * h.OverdriveVolleyMult;
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
            h.RespawnLeft = 15f;
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
