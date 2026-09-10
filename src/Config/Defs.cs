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

    // planet's built-in missile battery — the always-on primary defence (PDTD style)
    public float BatteryDamage { get; init; } = 26f;
    public float BatteryInterval { get; init; } = 1.4f;
    public int BatterySalvo { get; init; } = 1;
    public float BatterySplash { get; init; }
    public float BatteryMissileSpeed { get; init; } = 300f;
    public float BatteryRange { get; init; } = 620f;

    // orbital sentinels (autonomous auto-firing escorts unlocked by level cards)
    public float SentinelOrbitRadius { get; init; } = 190f;
    public float SentinelDamage { get; init; } = 10f;
    public float SentinelInterval { get; init; } = 0.7f;
    public float SentinelRange { get; init; } = 240f;
    public float SentinelBoltSpeed { get; init; } = 560f;
}

/// <summary>Survival spawn-director tuning. Every balance knob for the 5-minute
/// hold lives here so a balance pass never touches code (design-spec §14).</summary>
public sealed record SurvivalDef
{
    public float EpsBase { get; init; } = 0.35f;          // enemies/sec at t=0
    public float EpsRamp { get; init; } = 2.7f;           // added by the end of the hold
    public float EpsRampCurve { get; init; } = 1.35f;     // >1 = slower open, sharper finish
    public float LevelSpawnFactor { get; init; } = 0.085f;// per mission Level, extra spawn rate
    public float SurgeA { get; init; } = 0.30f;           // slow spawn-rate wobble
    public float SurgeB { get; init; } = 0.18f;           // faster wobble
    public int SoftCapBase { get; init; } = 38;           // max concurrent enemies at t=0
    public float SoftCapRamp { get; init; } = 95f;        // added by the end
    public int SoftCapPerLevel { get; init; } = 2;
    public float BossTimeFrac { get; init; } = 0.82f;     // when the survival boss enters
    public float PincerChance { get; init; } = 0.28f;     // odds a spawn joins a tight bearing
    public float ScaleLevelFactor { get; init; } = 0.075f;// enemy stat scale per mission Level
    public float ScaleRamp { get; init; } = 0.70f;        // enemy stat scale added by the end
    public float ScaleRampCurve { get; init; } = 1.3f;
    public float EndlessRampSeconds { get; init; } = 200f;// endless: seconds per +1.0 ramp unit
    public float SelfRepairFracPerSec { get; init; } = 0.0016f; // planet auto-repair / sec of max
    public float RewardRdMult { get; init; } = 3f;        // per survived minute, vs per-wave value
    public float RewardXpMult { get; init; } = 3f;
    public float RewardCreditsMult { get; init; } = 1.6f;
    public int CoreEveryNMinutes { get; init; } = 2;
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
    /// <summary>Damage per second the hull takes while an enemy is touching it.</summary>
    public float CollisionDps { get; init; } = 26f;
    /// <summary>Damage per second the hull deals to an enemy it is ramming.</summary>
    public float RamDps { get; init; } = 70f;
}

/// <summary>One of the ship's own weapon systems (data/hero_weapons.json). Each
/// levels up from the in-fight upgrade cards; the hull auto-fires whatever is
/// unlocked, or the player fires it by hand from the weapon buttons.</summary>
public sealed record HeroWeaponDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";        // laser | missiles | ion | yamato | plasma | shield
    public string Accent { get; init; } = "#4fd6de";
    public string Text { get; init; } = "";
    public int MaxLevel { get; init; } = 10;
    public bool AlwaysOn { get; init; }             // plasma field — no cooldown, always damaging
    public float Cooldown { get; init; } = 6f;
    public float CooldownPerLevel { get; init; }
    public float MinCooldown { get; init; } = 1f;
    public float Damage { get; init; }
    public float DamagePerLevel { get; init; }
    public int Count { get; init; } = 1;            // beams / missiles / chain jumps
    public float CountPerLevel { get; init; }       // added as floor(perLevel * (level-1))
    public float Range { get; init; } = 600f;
    public float ArcDeg { get; init; } = 360f;
    public float Radius { get; init; }
    public float RadiusPerLevel { get; init; }
    public float Splash { get; init; }
    public float Speed { get; init; } = 330f;
    public float Duration { get; init; }
    public float DurationPerLevel { get; init; }
    public float Absorb { get; init; }              // shield: fraction of incoming damage soaked
    public bool ShieldPierce { get; init; }
    public bool ArmorPierce { get; init; }
}

public sealed record HeroWeaponsDef
{
    public System.Collections.Generic.List<HeroWeaponDef> Weapons { get; init; } = new();
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

    /// <summary>Survival mission: one continuous escalating hold, no discrete waves.
    /// The spawn director ramps over <see cref="Duration"/> seconds.</summary>
    public bool Survival { get; init; }
    /// <summary>Seconds to hold. 0 = open-ended (endless / weekly).</summary>
    public float Duration { get; init; } = 300f;
    /// <summary>Difficulty scale index — higher = stronger/tougher/faster enemies and a steeper spawn ramp.</summary>
    public int Level { get; init; } = 1;
    /// <summary>Survival spawn roster: enemy id -> fraction of the run (0..1) before it may appear.</summary>
    public Dictionary<string, float> Roster { get; init; } = new();
    /// <summary>Optional boss id, spawned once near the end of a survival hold.</summary>
    public string Boss { get; init; } = "";

    /// <summary>Backdrop key — a space image in res://assets/game/bg/&lt;backdrop&gt;.jpg
    /// shown (dimmed) behind the play field. Empty = just the procedural starfield.</summary>
    public string Backdrop { get; init; } = "";

    /// <summary>Battle music key (a file stem in res://assets/music/game/, e.g. "eve_05").
    /// Empty = picked deterministically from the seed.</summary>
    public string Music { get; init; } = "";

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

public sealed record LevelCardDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Cat { get; init; } = "";
    public string Text { get; init; } = "";
    public bool Repeatable { get; init; }
    public int MaxPicks { get; init; } = 1;
    public string UnlockTurret { get; init; } = "";
    public string UnlockAbility { get; init; } = "";
    public Dictionary<string, float> Effects { get; init; } = new();
}

public sealed record ShopItemDef
{
    public string Id { get; init; } = "";
    public string Tab { get; init; } = "";       // fleet | worlds | ordnance
    public string Name { get; init; } = "";
    public string Desc { get; init; } = "";
    public int Cost { get; init; }               // Commendations; 0 = owned by default
    public string Apply { get; init; } = "";     // "category:value" set on Save.Options when equipped
}

public sealed record ShopCommendationsDef
{
    public int PerStar { get; init; } = 4;
    public int PerAscensionTier { get; init; } = 3;
    public int WeeklyComplete { get; init; } = 6;
    public int PerCodexEntry { get; init; } = 1;
    public int EndlessPerTwoMinutes { get; init; } = 3;
}

public sealed record ShopDef
{
    public ShopCommendationsDef Commendations { get; init; } = new();
    public List<ShopItemDef> Items { get; init; } = new();
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
