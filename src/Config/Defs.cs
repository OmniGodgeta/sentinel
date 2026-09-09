using System.Collections.Generic;

namespace Sentinel.Config;

// All gameplay numbers live in data/*.json. These records are the typed view of
// that data — no balance value should ever be a literal in a system file.
// JSON is snake_case, mapped via JsonNamingPolicy.SnakeCaseLower.

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
    public float ResearchDataPerWave { get; init; } = 8f;
    public float ResearchDataMissionClear { get; init; } = 60f;
    public float XpPerWave { get; init; } = 12f;
    public float XpMissionClear { get; init; } = 100f;
    // in-run turret upgrades: level 2 = base*mult, level 3 = base*mult^2
    public float TurretUpgradeCostMult { get; init; } = 0.8f;   // cost of next level = base cost * this * level
    public float TurretUpgradeStatMult { get; init; } = 1.6f;   // dmg/rate scale per level
}

public sealed record HeroDef
{
    public float MaxHull { get; init; } = 400f;
    public float MoveSpeed { get; init; } = 300f;
    public float PointDefenseDps { get; init; } = 16f;
    public float PointDefenseRange { get; init; } = 170f;
    public float VolleyCooldown { get; init; } = 15f;
    public int VolleyMissiles { get; init; } = 4;
    public float MissileDamage { get; init; } = 55f;
    public float MissileSpeed { get; init; } = 340f;
    public float MissileSplashRadius { get; init; } = 46f;
    public int AbilitySlots { get; init; } = 3;
    public float RespawnSeconds { get; init; } = 15f;
}

public sealed record TurretFork
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public float DamageMult { get; init; } = 1f;
    public float FireRateMult { get; init; } = 1f;    // >1 = faster (interval divided)
    public float RangeMult { get; init; } = 1f;
    public float ArmorPenAdd { get; init; }
    public int ExtraPierce { get; init; }
    public int ExtraChain { get; init; }
    public float SplashMult { get; init; } = 1f;
}

public sealed record TurretDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Fire { get; init; } = "projectile";  // projectile | beam | chain | support
    public int Cost { get; init; } = 100;
    public float Damage { get; init; } = 10f;
    public float FireInterval { get; init; } = 0.5f;
    public float Range { get; init; } = 260f;
    public float ArcDegrees { get; init; } = 90f;
    public float ProjectileSpeed { get; init; } = 520f;
    public float SplashRadius { get; init; }
    public float ArmorPen { get; init; }
    public int Pierce { get; init; }
    public int ChainJumps { get; init; }               // chain turrets
    public float ChainRange { get; init; } = 90f;
    public float ShieldMult { get; init; } = 1f;        // damage multiplier vs shields (tesla/ion high)
    public float RampPerSecond { get; init; }           // laser lattice: dps ramp while on one target
    public float RampMax { get; init; } = 1f;
    public float SlowOnHit { get; init; }               // graviton: projectile applies this slow factor
    public float SupportBuff { get; init; }             // nanite forge: +dmg fraction to neighbours
    public float SupportRange { get; init; } = 60f;
    public string TargetMode { get; init; } = "first";  // first | closest | strongest
    public bool Homing { get; init; }
    public bool Hitscan { get; init; }                  // beam/chain never miss evasive units
    public float Color { get; init; } = 0.55f;
    public List<TurretFork> Forks { get; init; } = new();
    public string CodexId { get; init; } = "";
}

public sealed record AbilityDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Role { get; init; } = "";            // offense | control | defense | utility
    public float Cooldown { get; init; } = 30f;
    public float Duration { get; init; }
    public float Damage { get; init; }
    public float TickInterval { get; init; } = 0.2f;
    public float Radius { get; init; }
    public float ArcDegrees { get; init; }
    public float Value { get; init; }                  // kind-specific
    public float Value2 { get; init; }
    public int IntValue { get; init; }
    public string Cast { get; init; } = "instant";     // instant | reticle | arc
    public string CodexId { get; init; } = "";
}

public sealed record EnemyDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Class { get; init; } = "line";       // line | threat | boss
    public float MaxHp { get; init; } = 40f;
    public float Speed { get; init; } = 46f;
    public float Armor { get; init; }
    public float ShieldHp { get; init; }
    public float ShieldRegen { get; init; }            // per second, after a delay
    public float ShieldRegenDelay { get; init; } = 3f;
    public float Evasion { get; init; }                // 0..1 miss chance vs non-hitscan slow shots
    public float ContactDamage { get; init; } = 20f;
    public float Radius { get; init; } = 10f;
    public int Bounty { get; init; } = 6;
    public bool CcImmune { get; init; }                // siege crawler: immune to slow/pull
    public bool IgnoreObstacles { get; init; }
    // ranged attacker (bombard): stops at StandoffRange, shells planet/turrets
    public float StandoffRange { get; init; }
    public float RangedDamage { get; init; }
    public float RangedInterval { get; init; } = 2f;
    // spawner (carrier)
    public string SpawnEnemy { get; init; } = "";
    public float SpawnInterval { get; init; } = 3f;
    public int SpawnCount { get; init; } = 1;
    // blinker (phase runner)
    public float BlinkInterval { get; init; }
    public float BlinkDistance { get; init; }
    // leech
    public bool Leech { get; init; }
    public float LeechDisableSeconds { get; init; } = 10f;
    // aura support (warden)
    public float AuraRadius { get; init; }
    public float AuraHealPerSecond { get; init; }
    public float AuraShieldPerSecond { get; init; }
    // boss
    public string BossMechanic { get; init; } = "";    // threshing_gate | ...
    public float MechanicInterval { get; init; } = 8f;
    public float MechanicDuration { get; init; } = 3f;
    public int CoreDrop { get; init; }
    public int AlloyDrop { get; init; }
    public float Color { get; init; } = 0.02f;
    public string CodexId { get; init; } = "";
}

public sealed record WaveEntry
{
    public string Enemy { get; init; } = "";
    public int Count { get; init; } = 1;
    public float Interval { get; init; } = 0.6f;
    public float StartDelay { get; init; }
    public float ArcCenterDeg { get; init; } = -1f;
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
    public string Intro { get; init; } = "";
    public ulong Seed { get; init; } = 1;
    public int StartingCreditsOverride { get; init; } = -1;
    public bool Endless { get; init; }
    /// <summary>Enemies the procedural endless generator may use (needs their defs
    /// resolved up front). Ignored for normal missions.</summary>
    public List<string> EndlessRoster { get; init; } = new();
    public List<WaveDef> Waves { get; init; } = new();
}

public sealed record ArcMission
{
    public string Id { get; init; } = "";
    public string File { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed record ArcDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public List<ArcMission> Missions { get; init; } = new();
}

public sealed record AscensionTierDef
{
    public int Tier { get; init; }
    public string Name { get; init; } = "";
    public float EnemyHpMult { get; init; } = 1f;
    public float EnemySpeedMult { get; init; } = 1f;
    public float EnemyShieldAdd { get; init; }
    public float EnemyArmorAdd { get; init; }
    public float EnemyCountMult { get; init; } = 1f;
    public float RewardMult { get; init; } = 1f;
    public Dictionary<string, float> PlayerEffects { get; init; } = new();
}

public sealed record CodexEntry
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Category { get; init; } = "";
    public string Text { get; init; } = "";
}

public sealed record CardDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public string Rarity { get; init; } = "common";   // common | rare | epic
    public int Weight { get; init; } = 60;
    /// <summary>effect key -> value, applied to the run's ModifierSet on pick.</summary>
    public Dictionary<string, float> Effects { get; init; } = new();
    public float IntegrityBonus { get; init; }         // also added to current + max integrity
    public float HullBonus { get; init; }              // also added to current + max hero hull
}
