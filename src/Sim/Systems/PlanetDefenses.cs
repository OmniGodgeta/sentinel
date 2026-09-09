using Godot;

namespace Sentinel.Sim;

public sealed partial class SimWorld
{
    private float _batteryCd;
    private DetRandom _defRng;

    public struct Sentinel { public Vector2 Pos; public float Angle; public float Cd; }
    private Sentinel[] _sentinels = System.Array.Empty<Sentinel>();
    public System.ReadOnlySpan<Sentinel> SentinelView => _sentinels;

    internal void InitPlanetDefenses()
    {
        _batteryCd = 0f;
        _defRng = new DetRandom(Mission.Seed ^ 0x51E7CAFE00D1234UL);
        int n = Mathf.Clamp(Mods.SentinelCount, 0, 6);
        _sentinels = new Sentinel[n];
        for (int i = 0; i < n; i++)
            _sentinels[i].Angle = i * Mathf.Tau / n;
    }

    private void StepPlanetBattery()
    {
        float dt = SimClock.TickDelta;
        if (_batteryCd > 0f) { _batteryCd -= dt; return; }

        int salvo = Mathf.Max(1, B.BatterySalvo + Mods.BatterySalvoAdd);
        System.Span<int> picks = stackalloc int[8];
        int n = NearestEnemiesTo(Vector2.Zero, System.Math.Min(salvo, 8), picks);
        if (n == 0) return;

        float dmg = B.BatteryDamage * Mathf.Max(0.2f, Mods.BatteryDamageMult);
        float splash = B.BatterySplash + Mods.BatterySplashAdd;
        for (int m = 0; m < salvo; m++)
        {
            int ti = picks[System.Math.Min(m, n - 1)];
            if (!Enemies[ti].Alive) continue;
            Vector2 dir = (Enemies[ti].Pos).Normalized();
            if (dir == Vector2.Zero) dir = Vector2.Up;
            dir = dir.Rotated(_defRng.NextFloat(-0.4f, 0.4f));
            Vector2 launch = dir * (B.PlanetRadius + 6f);
            float ms = B.BatteryMissileSpeed;
            SpawnProjectile(3, launch, dir * (ms * 0.45f), dmg, splash,
                            HandleOf(ti), src: 255, life: 6f,
                            speedMax: ms * 1.25f, accel: ms * 1.9f, agility: 6.5f);
        }
        Events.Push(SimEventKind.VolleyLaunched, Vector2.Zero, salvo);
        _batteryCd = B.BatteryInterval / Mathf.Max(0.2f, Mods.BatteryRateMult);
    }

    private void StepOrbitalSentinels()
    {
        if (_sentinels.Length == 0) return;
        float dt = SimClock.TickDelta;
        float orbit = B.SentinelOrbitRadius;
        float rangeSq = B.SentinelRange * B.SentinelRange;
        float dmg = B.SentinelDamage * Mathf.Max(0.2f, Mods.SentinelDamageMult);

        for (int i = 0; i < _sentinels.Length; i++)
        {
            ref var s = ref _sentinels[i];
            s.Angle += dt * 0.5f;
            s.Pos = new Vector2(Mathf.Cos(s.Angle), Mathf.Sin(s.Angle)) * orbit;
            if (s.Cd > 0f) s.Cd -= dt;
            if (s.Cd > 0f) continue;

            int best = -1; float bd = rangeSq;
            for (int e = 0; e < EnemyHighWater; e++)
            {
                if (!Enemies[e].Alive) continue;
                float d = Enemies[e].Pos.DistanceSquaredTo(s.Pos);
                if (d < bd) { bd = d; best = e; }
            }
            if (best < 0) continue;
            Vector2 dir = (Enemies[best].Pos - s.Pos).Normalized();
            SpawnProjectile(0, s.Pos, dir * B.SentinelBoltSpeed, dmg, 0f, EnemyHandle.None, src: 255, life: 1.5f);
            Events.PushLine(SimEventKind.TurretFired, s.Pos, Enemies[best].Pos, 0f, -5);
            s.Cd = B.SentinelInterval / Mathf.Max(0.2f, Mods.SentinelRateMult);
        }
    }
}
