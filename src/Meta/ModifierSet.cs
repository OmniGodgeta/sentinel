namespace Sentinel.Meta;

/// <summary>
/// The aggregated numeric effect of everything permanent the player has bought —
/// research nodes, capstones, hero level, ability levels. Built fresh from
/// <see cref="SaveGame"/> before each mission and handed to the sim, which reads
/// it instead of hard-coding. Multipliers default to 1, additives to 0.
/// </summary>
public sealed class ModifierSet
{
    // ---- turrets ----
    public float TurretDamageMult = 1f;
    public float TurretFireRateMult = 1f;       // >1 = faster
    public float TurretRangeMult = 1f;
    public float TurretSplashMult = 1f;
    public float TurretArmorPenAdd = 0f;
    public float TurretCritChance = 0f;
    public float TurretCritMult = 1.5f;
    public int   TurretExtraTargets = 0;
    public float TurretUpgradeCostMult = 1f;
    public bool  TurretsFireInBuildPhase = false;

    // ---- planet / fortification ----
    public float PlanetIntegrityMult = 1f;
    public float PlanetDamageTakenMult = 1f;
    public float PlanetStartShield = 0f;
    public float PlanetRegenPerWaveFrac = 0f;   // fraction of max integrity restored each wave cleared
    public float LeakedDamageMult = 1f;
    public bool  DesperationBonus = false;       // <30% integrity => +20% all damage

    // ---- hero ----
    public float HeroHullMult = 1f;
    public float HeroMissileDamageMult = 1f;
    public float HeroMissileCdAdd = 0f;          // seconds (negative = faster), floored in sim
    public int   HeroExtraMissiles = 0;
    public float HeroMoveSpeedMult = 1f;
    public float HeroPointDefenseMult = 1f;
    public float HeroRespawnSeconds = -1f;       // <0 = use config default
    public bool  HeroDoubleSalvo = false;        // hero level 14

    // ---- abilities ----
    public float AbilityEffectMult = 1f;
    public float AbilityCooldownMult = 1f;
    public float AbilityRadiusMult = 1f;
    public bool  AbilitiesStartReady = false;
    public float SentinelCoreGainMult = 1f;

    // ---- economy / logistics ----
    public float ResearchDataGainMult = 1f;
    public float XpGainMult = 1f;
    public float ExoticAlloyGainMult = 1f;
    public int   StartCreditsAdd = 0;
    public float WaveIncomeMult = 1f;
    public int   CardDraftOptions = 2;           // reserved (card draft is a later phase)
    public int   CardDraftRerolls = 0;
    public float LossRewardFrac = 1f;            // fraction of rewards kept on a loss (already 1 by default here)

    // ---- capstone flags (one per branch, mutually exclusive) ----
    public string CapArmaments = "";
    public string CapFortification = "";
    public string CapFleet = "";
    public string CapSentinel = "";
    public string CapLogistics = "";

    // hero level (for the sim to know slot count / salvo)
    public int HeroLevel = 1;
    public int AbilitySlots = 3;

    public ModifierSet Clone() => (ModifierSet)MemberwiseClone();

    public void ApplyEffect(string key, float v)
    {
        switch (key)
        {
            case "turret_damage": TurretDamageMult += v; break;
            case "turret_fire_rate": TurretFireRateMult += v; break;
            case "turret_range": TurretRangeMult += v; break;
            case "turret_splash": TurretSplashMult += v; break;
            case "turret_armor_pen": TurretArmorPenAdd += v; break;
            case "turret_crit_chance": TurretCritChance += v; break;
            case "turret_crit_mult": TurretCritMult += v; break;
            case "turret_extra_target": TurretExtraTargets += (int)v; break;
            case "turret_upgrade_cost": TurretUpgradeCostMult += v; break;
            case "turret_build_phase_fire": TurretsFireInBuildPhase = true; break;

            case "planet_integrity": PlanetIntegrityMult += v; break;
            case "planet_damage_taken": PlanetDamageTakenMult += v; break;
            case "planet_start_shield": PlanetStartShield += v; break;
            case "planet_regen_per_wave": PlanetRegenPerWaveFrac += v; break;
            case "leaked_damage": LeakedDamageMult += v; break;
            case "desperation": DesperationBonus = true; break;

            case "hero_hull": HeroHullMult += v; break;
            case "hero_missile_damage": HeroMissileDamageMult += v; break;
            case "hero_missile_cd": HeroMissileCdAdd += v; break;
            case "hero_extra_missile": HeroExtraMissiles += (int)v; break;
            case "hero_move_speed": HeroMoveSpeedMult += v; break;
            case "hero_point_defense": HeroPointDefenseMult += v; break;
            case "hero_respawn": HeroRespawnSeconds = v; break;

            case "ability_effect": AbilityEffectMult += v; break;
            case "ability_cooldown": AbilityCooldownMult += v; break;
            case "ability_radius": AbilityRadiusMult += v; break;
            case "ability_start_ready": AbilitiesStartReady = true; break;
            case "core_gain": SentinelCoreGainMult += v; break;

            case "rd_gain": ResearchDataGainMult += v; break;
            case "xp_gain": XpGainMult += v; break;
            case "alloy_gain": ExoticAlloyGainMult += v; break;
            case "start_credits": StartCreditsAdd += (int)v; break;
            case "wave_income": WaveIncomeMult += v; break;
            case "card_options": CardDraftOptions += (int)v; break;
            case "card_reroll": CardDraftRerolls += (int)v; break;
            case "loss_reward": LossRewardFrac = System.Math.Max(LossRewardFrac, v); break;
        }
    }
}
