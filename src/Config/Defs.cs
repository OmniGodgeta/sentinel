using System.Collections.Generic;

namespace Sentinel.Config;

// All gameplay numbers live in data/*.json. These records are the typed view of
// that data — no balance value should ever be a literal in a system file.
// Naming: JSON is snake_case, mapped via JsonNamingPolicy.SnakeCaseLower.

public sealed record BalanceDef
{
    public float PlanetRadius { get; init; } = 120f;
    public float PlanetIntegrity { get; init; } = 1000f;
    public float TurretRingRadius { get; init; } = 150f;
    public float HeroOrbitMin { get; init; } = 200f;
    public float HeroOrbitMax { get; init; } = 340f;
    public float SpawnRadius { get; init; } = 760f;
    public float DespawnRadius { get; init; } = 900f;
    public int TurretSlots { get; init; } = 12;
    public float BuildPhaseSeconds { get; init; } = 20f;
    public int StartingCredits { get; init; } = 300;
    public int CreditsPerWave { get; init; } = 120;
    // Reward curve — paid win OR loss (design rule: no wasted sessions).
    public float ResearchDataPerWave { get; init; } = 8f;
    public float ResearchDataMissionClear { get; init; } = 60f;
    public float XpPerWave { get; init; } = 12f;
    public float XpMissionClear { get; init; } = 100f;
}

public sealed record HeroDef
{
    public float MaxHull { get; init; } = 400f;
    public float MoveSpeed { get; init; } = 260f;      // units/sec along the drag
    public float PointDefenseDps { get; init; } = 14f;
    public float PointDefenseRange { get; init; } = 170f;
    public float VolleyCooldown { get; init; } = 15f;
    public int VolleyMissiles { get; init; } = 4;
    public float MissileDamage { get; init; } = 55f;
    public float MissileSpeed { get; init; } = 340f;
    public float MissileSplashRadius { get; init; } = 46f;
    public int AbilitySlots { get; init; } = 3;
}

public sealed record TurretDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Cost { get; init; } = 100;
    public float Damage { get; init; } = 10f;
    public float FireInterval { get; init; } = 0.5f;   // seconds between shots
    public float Range { get; init; } = 260f;
    public float ArcDegrees { get; init; } = 90f;      // firing cone half-angle*2
    public float ProjectileSpeed { get; init; } = 520f;
    public float SplashRadius { get; init; } = 0f;
    public string TargetMode { get; init; } = "first"; // first | closest | strongest
    public float Color { get; init; } = 0.55f;         // greybox hue 0..1
}

public sealed record AbilityDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";           // barrage | barrier | overdrive (Phase 1)
    public float Cooldown { get; init; } = 30f;
    public float Duration { get; init; } = 0f;
    public float Damage { get; init; } = 0f;
    public float Radius { get; init; } = 0f;
    public float ArcDegrees { get; init; } = 0f;
    public float Value { get; init; } = 0f;            // kind-specific (absorb pool, buff %, ...)
    public string Cast { get; init; } = "instant";     // instant | reticle | arc
}

public sealed record EnemyDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public float MaxHp { get; init; } = 40f;
    public float Speed { get; init; } = 46f;           // units/sec toward the planet
    public float Armor { get; init; } = 0f;            // flat reduction per hit
    public float ContactDamage { get; init; } = 20f;   // to planet integrity on arrival
    public float Radius { get; init; } = 10f;
    public int Bounty { get; init; } = 6;              // in-run credits
    public float Color { get; init; } = 0.02f;
}

public sealed record WaveEntry
{
    public string Enemy { get; init; } = "";
    public int Count { get; init; } = 1;
    public float Interval { get; init; } = 0.6f;       // seconds between spawns in this group
    public float StartDelay { get; init; } = 0f;       // seconds after wave start
    public float ArcCenterDeg { get; init; } = -1f;    // -1 => random angle per enemy
    public float ArcSpreadDeg { get; init; } = 360f;
}

public sealed record WaveDef
{
    public List<WaveEntry> Groups { get; init; } = new();
}

public sealed record MissionDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public ulong Seed { get; init; } = 1;
    public List<WaveDef> Waves { get; init; } = new();
}
