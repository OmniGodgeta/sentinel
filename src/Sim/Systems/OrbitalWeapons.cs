using Godot;

namespace Sentinel.Sim;

/// <summary>
/// The planet's orbital weapons — PDTD-style "sentinels" that orbit the planet and
/// auto-fire. Each has a per-run level that the in-fight card draft raises (later
/// levels also come from out-of-battle meta). Deterministic: platform positions
/// are a pure function of GameTime, every roll comes off <see cref="Rng"/>.
/// </summary>
public sealed partial class SimWorld
{
    private int[] _owLevel = System.Array.Empty<int>();
    private float[] _owCd = System.Array.Empty<float>();

    private const float OwOrbit = 2.45f;   // × SentinelOrbitRadius — a clear orbit ring in open space

    // active timed field effects (radiation line / shock orb / radiation zone)
    public struct OwEffect
    {
        public int Kind;        // 1 rad_line, 2 shock_orb, 3 rad_zone
        public float DieAt;     // GameTime
        public float Tick;      // dps accumulator
        public float Dps;
        public float Radius;
        public float Stun;
        public float P0;        // rad_line: bearing; shock_orb/rad_zone: seed angle
        public Vector2 Pos;     // shock_orb / rad_zone current centre
    }
    private readonly System.Collections.Generic.List<OwEffect> _owEffects = new();

    private float _fxOwBeamLeft; private int _fxOwBeamKind; private Vector2 _fxOwBeamFrom, _fxOwBeamTo;

    // ---- views ----
    public int OrbitalWeaponCount => _owLevel.Length;
    public int OrbitalWeaponLevel(int i) => (uint)i < (uint)_owLevel.Length ? _owLevel[i] : 0;
    public float OrbitalWeaponCooldownLeft(int i) => (uint)i < (uint)_owCd.Length ? _owCd[i] : 0f;
    public Vector2 OrbitalPlatformPos(int i)
    {
        float a = GameTime * 0.32f + i * Mathf.Tau / Mathf.Max(1, _owLevel.Length);
        return Vector2.FromAngle(a) * (B.SentinelOrbitRadius * OwOrbit);
    }
    public System.ReadOnlySpan<OwEffect> OrbitalEffects => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_owEffects);
    public float FxOrbitalBeamLeft => _fxOwBeamLeft;
    public int FxOrbitalBeamKind => _fxOwBeamKind;
    public Vector2 FxOrbitalBeamFrom => _fxOwBeamFrom;
    public Vector2 FxOrbitalBeamTo => _fxOwBeamTo;

    private void ResetOrbitalWeapons()
    {
        int n = Cfg.OrbitalWeapons.Count;
        _owLevel = new int[n];
        _owCd = new float[n];
        _owEffects.Clear();
        _fxOwBeamLeft = 0f;

        // meta head-start: an orbital weapon bought to a persistent level starts there
        for (int i = 0; i < n; i++)
            _owLevel[i] = Mathf.Clamp(Mods.OrbitalMetaLevel(Cfg.OrbitalWeapons[i].Id), 0, Cfg.OrbitalWeapons[i].MaxLevel);
    }

    private void LevelUpOrbitalWeapon(int i)
    {
        if ((uint)i >= (uint)_owLevel.Length) return;
        int max = Mathf.Max(1, Cfg.OrbitalWeapons[i].MaxLevel);
        if (_owLevel[i] < max) _owLevel[i]++;
    }

    private void StepOrbitalWeapons(float dt)
    {
        if (_fxOwBeamLeft > 0f) _fxOwBeamLeft -= dt;

        for (int i = 0; i < _owCd.Length; i++) if (_owCd[i] > 0f) _owCd[i] -= dt;

        // --- active field effects ---
        for (int k = _owEffects.Count - 1; k >= 0; k--)
        {
            var fx = _owEffects[k];
            if (GameTime >= fx.DieAt) { _owEffects.RemoveAt(k); continue; }
            StepOwEffect(ref fx, dt);
            _owEffects[k] = fx;
        }

        if (!_heroAutoFire) return;   // the AUTO toggle gates orbital fire too

        for (int i = 0; i < _owLevel.Length; i++)
        {
            if (_owLevel[i] <= 0 || _owCd[i] > 0f) continue;
            var d = Cfg.OrbitalWeapons[i];
            if (ClosestEnemyTo(Vector2.Zero, B.DespawnRadius) < 0) continue;
            FireOrbitalWeapon(i);
        }
    }

    private void FireOrbitalWeapon(int i)
    {
        var d = Cfg.OrbitalWeapons[i];
        int L = _owLevel[i];
        float dmg = d.Damage + d.DamagePerLevel * (L - 1);
        int cnt = Mathf.Max(1, d.Count + Mathf.FloorToInt(d.CountPerLevel * (L - 1)));
        float radius = d.Radius + d.RadiusPerLevel * (L - 1);
        float dur = d.Duration + d.DurationPerLevel * (L - 1);
        Vector2 from = OrbitalPlatformPos(i);

        switch (d.Kind)
        {
            case "cannon":
            {
                System.Span<int> picks = stackalloc int[8];
                int n = NearestEnemiesTo(from, System.Math.Min(cnt, 8), picks);
                for (int m = 0; m < n; m++)
                    DamageEnemy(picks[m], dmg, DamageSource.Orbital, armorPen: d.ArmorPierce ? 9999f : 0f);
                if (n > 0) { _fxOwBeamLeft = 0.12f; _fxOwBeamKind = 0; _fxOwBeamFrom = from; _fxOwBeamTo = Enemies[picks[0]].Pos; }
                break;
            }
            case "laser":
            {
                int t = ClosestEnemyTo(from, d.Range);
                if (t < 0) break;
                Vector2 to = Enemies[t].Pos;
                Vector2 dir = (to - from).Normalized();
                float len = from.DistanceTo(to) + 120f;
                for (int e = 0; e < EnemyHighWater; e++)
                {
                    ref var en = ref Enemies[e];
                    if (!en.Alive) continue;
                    Vector2 rel = en.Pos - from;
                    float along = rel.Dot(dir);
                    if (along < 0f || along > len) continue;
                    if ((rel - dir * along).Length() > 26f + en.Radius) continue;
                    DamageEnemy(e, dmg, DamageSource.Orbital, shieldMult: d.ShieldPierce ? 0f : 1f);
                    if (en.Alive && d.DotDps > 0f) { en.BurnLeft = Mathf.Max(en.BurnLeft, d.DotSeconds); en.BurnDps = Mathf.Max(en.BurnDps, d.DotDps + d.DamagePerLevel * 0.15f * (L - 1)); }
                }
                _fxOwBeamLeft = 0.16f; _fxOwBeamKind = 1; _fxOwBeamFrom = from; _fxOwBeamTo = from + dir * len;
                break;
            }
            case "lightning":
            {
                int cur = ClosestEnemyTo(from, d.Range);
                if (cur < 0) break;
                Vector2 node = from;
                System.Span<bool> hit = stackalloc bool[96];
                for (int j = 0; j < cnt && cur >= 0; j++)
                {
                    ref var en = ref Enemies[cur];
                    DamageEnemy(cur, dmg, DamageSource.Orbital);
                    if (en.Alive && d.StunSeconds > 0f && !_missionEnemyDefs[en.DefIndex].CcImmune)
                        en.StunLeft = Mathf.Max(en.StunLeft, d.StunSeconds);
                    if (cur < 96) hit[cur] = true;
                    Events.PushLine(SimEventKind.ChainArc, node, en.Pos, 3f, 1);
                    node = en.Pos;
                    int next = -1; float best = radius * radius;
                    for (int e = 0; e < EnemyHighWater; e++)
                    {
                        if (!Enemies[e].Alive || (e < 96 && hit[e])) continue;
                        float dd = Enemies[e].Pos.DistanceSquaredTo(node);
                        if (dd < best) { best = dd; next = e; }
                    }
                    cur = next;
                }
                break;
            }
            case "rad_line":
                _owEffects.Add(new OwEffect { Kind = 1, DieAt = GameTime + dur, Dps = dmg, P0 = Rng.NextFloat(0f, Mathf.Tau) });
                break;
            case "shock_orb":
                _owEffects.Add(new OwEffect { Kind = 2, DieAt = GameTime + dur, Dps = dmg, Radius = radius, Stun = d.StunSeconds, P0 = Rng.NextFloat(0f, Mathf.Tau) });
                break;
            case "rad_zone":
                _owEffects.Add(new OwEffect { Kind = 3, DieAt = GameTime + dur, Dps = dmg, Radius = radius, P0 = Rng.NextFloat(0f, Mathf.Tau) });
                break;
        }

        _owCd[i] = Mathf.Max(d.MinCooldown, d.Cooldown + d.CooldownPerLevel * (L - 1));
        Events.Push(SimEventKind.HeroWeaponFired, from, radius, 10 + i);
    }

    private void StepOwEffect(ref OwEffect fx, float dt)
    {
        fx.Tick += dt;
        float interval = 0.25f;
        float t = GameTime;

        if (fx.Kind == 1) // radiation line — a slowly rotating lethal beam across the field
        {
            float bearing = fx.P0 + t * 0.35f;
            Vector2 dir = Vector2.FromAngle(bearing);
            while (fx.Tick >= interval)
            {
                fx.Tick -= interval;
                for (int e = 0; e < EnemyHighWater; e++)
                {
                    ref readonly var en = ref Enemies[e];
                    if (!en.Alive) continue;
                    float along = en.Pos.Dot(dir);
                    if (along < B.PlanetRadius || along > B.DespawnRadius) continue;
                    if ((en.Pos - dir * along).Length() > 30f + en.Radius) continue;
                    DamageEnemy(e, fx.Dps * interval * 4f, DamageSource.Orbital, shieldMult: 0f);
                }
            }
        }
        else // shock orb (2) or radiation zone (3) — a moving AoE
        {
            if (fx.Kind == 2)
            {
                float a = fx.P0 + t * 0.9f;
                fx.Pos = Vector2.FromAngle(a) * (B.SentinelOrbitRadius * 0.82f);
            }
            else
            {
                float a = fx.P0 + t * 0.5f;
                float r = B.SpawnRadius * (0.45f + 0.25f * Mathf.Sin(t * 0.7f + fx.P0));
                fx.Pos = Vector2.FromAngle(a) * r;
            }
            float rSq = fx.Radius * fx.Radius;
            while (fx.Tick >= interval)
            {
                fx.Tick -= interval;
                for (int e = 0; e < EnemyHighWater; e++)
                {
                    ref var en = ref Enemies[e];
                    if (!en.Alive || en.Pos.DistanceSquaredTo(fx.Pos) > rSq) continue;
                    DamageEnemy(e, fx.Dps * interval * 4f, DamageSource.Orbital, shieldMult: fx.Kind == 3 ? 0f : 1f);
                    if (en.Alive && fx.Stun > 0f && !_missionEnemyDefs[en.DefIndex].CcImmune)
                        en.StunLeft = Mathf.Max(en.StunLeft, fx.Stun);
                }
            }
        }
    }
}
