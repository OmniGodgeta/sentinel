#!/usr/bin/env python3
"""Generate data/research.json — 5 branches x 6 tiers, ~90 nodes.
Effect keys map 1:1 to ModifierSet.ApplyEffect. Offline-collector nodes from the
spec are repurposed (offline collectors are cut)."""
import json, os

OUT = os.path.join(os.path.dirname(__file__), "..", "data", "research.json")

# per branch: commander gate for the branch itself
BRANCH_GATE = {"armaments": 1, "logistics": 1, "fortification": 5, "fleet": 10, "sentinel": 15}
TIER_GATE = {1: 1, 2: 8, 3: 16, 4: 25, 5: 35, 6: 50}

nodes = []

def N(branch, tier, nid, name, effects, ranks=1, cost=None, currency="rd", alloy=0, text=""):
    if cost is None:
        cost = {1: 50, 2: 90, 3: 160, 4: 300, 5: 550, 6: 1200}[tier]
    nodes.append({
        "id": f"{branch[:3]}_{nid}",
        "branch": branch, "tier": tier, "name": name,
        "rank_count": ranks, "cost_base": cost, "currency": currency, "alloy_cost": alloy,
        "commander_gate": max(BRANCH_GATE[branch], TIER_GATE[tier]),
        "effects": effects, "text": text,
    })

def CAP(branch, group, nid, name, effects, cost, alloy, text):
    nodes.append({
        "id": f"{branch[:3]}_{nid}", "branch": branch, "tier": 6, "name": name,
        "rank_count": 1, "cost_base": cost, "currency": "rd", "alloy_cost": alloy,
        "commander_gate": TIER_GATE[6], "is_capstone": True, "capstone_group": group,
        "effects": effects, "text": text,
    })

# ---------------- A · Armaments ----------------
N("armaments", 1, "dmg", "Munitions Standard", {"turret_damage": 0.05}, ranks=5, text="Turret damage +5% per rank.")
N("armaments", 1, "range", "Targeting Optics", {"turret_range": 0.04}, ranks=5, text="Turret range +4% per rank.")
N("armaments", 2, "rof", "Autoloaders", {"turret_fire_rate": 0.04}, ranks=5, text="Turret fire rate +4% per rank.")
N("armaments", 2, "crit", "Weak-Point Scan", {"turret_crit_chance": 0.03}, ranks=3, text="Turret crit chance +3% per rank.")
N("armaments", 3, "pen", "Armour-Piercing Cores", {"turret_armor_pen": 3}, ranks=3, text="Turret armour penetration +3 per rank.")
N("armaments", 3, "splash", "Fragmentation", {"turret_splash": 0.12}, ranks=4, text="Turret splash radius +12% per rank.")
N("armaments", 4, "upcost", "Modular Mounts", {"turret_upgrade_cost": -0.08}, ranks=3, text="In-run turret upgrades cost 8% less per rank.")
N("armaments", 4, "critdmg", "Overpressure Rounds", {"turret_crit_mult": 0.25}, ranks=3, text="Turret crit damage +25% per rank.")
N("armaments", 5, "multi", "Fire Distribution", {"turret_extra_target": 1}, ranks=1, cost=650, text="Turrets engage +1 target.")
N("armaments", 5, "buildfire", "Standing Orders", {"turret_build_phase_fire": 1}, ranks=1, cost=500, text="Turrets keep firing during the build phase.")
CAP("armaments", "arm_doctrine", "cap_sat", "Saturation Doctrine",
    {"turret_fire_rate": 0.25, "turret_damage": -0.10}, 2400, 6, "+25% fire rate, −10% damage per shot.")
CAP("armaments", "arm_doctrine", "cap_prec", "Precision Doctrine",
    {"turret_damage": 0.35, "turret_fire_rate": -0.10}, 2400, 6, "+35% damage, −10% fire rate.")

# ---------------- B · Fortification ----------------
N("fortification", 1, "integ", "Reinforced Crust", {"planet_integrity": 0.08}, ranks=5, text="Planet integrity +8% per rank.")
N("fortification", 1, "shield", "Static Envelope", {"planet_start_shield": 70}, ranks=3, text="Start each run with +70 planet shield per rank.")
N("fortification", 2, "regen", "Between-Wave Repair", {"planet_regen_per_wave": 0.015}, ranks=4, text="Restore 1.5% integrity per wave cleared, per rank.")
N("fortification", 3, "dr", "Deflection Plating", {"planet_damage_taken": -0.04}, ranks=3, text="Damage to the planet −4% per rank.")
N("fortification", 3, "regen2", "Nanite Reserves", {"planet_regen_per_wave": 0.02}, ranks=3, text="+2% integrity per wave cleared, per rank.")
N("fortification", 4, "leak", "Interior Bulkheads", {"leaked_damage": -0.10}, ranks=3, text="Leaked enemies deal 10% less damage per rank.")
N("fortification", 4, "shield2", "Hardened Envelope", {"planet_start_shield": 120}, ranks=2, text="+120 starting shield per rank.")
N("fortification", 5, "desperation", "Last Stand Protocol", {"desperation": 1}, ranks=1, cost=600,
  text="Below 30% integrity: +20% all damage.")
CAP("fortification", "fort_doctrine", "cap_bastion", "Bastion",
    {"planet_integrity": 0.40}, 2400, 6, "+40% max integrity, no regeneration.")
CAP("fortification", "fort_doctrine", "cap_resil", "Resilience",
    {"planet_integrity": 0.15, "planet_regen_per_wave": 0.02}, 2400, 6, "+15% integrity, +2% regen per wave cleared.")

# ---------------- C · Fleet Command ----------------
N("fleet", 1, "hull", "Hull Bracing", {"hero_hull": 0.08}, ranks=5, text="Hero hull +8% per rank.")
N("fleet", 1, "mdmg", "Warhead Yield", {"hero_missile_damage": 0.06}, ranks=5, text="Missile volley damage +6% per rank.")
N("fleet", 1, "move", "Manoeuvring Thrusters", {"hero_move_speed": 0.08}, ranks=3, text="Hero drag speed +8% per rank.")
N("fleet", 2, "mcd", "Rapid Racks", {"hero_missile_cd": -0.5}, ranks=4, text="Missile cooldown −0.5s per rank (floor 11s).")
N("fleet", 2, "mcount", "Extra Tubes", {"hero_extra_missile": 1}, ranks=2, text="+1 missile per volley per rank.")
N("fleet", 3, "pd", "Point-Defence Boost", {"hero_point_defense": 0.25}, ranks=3, text="Hero point-defence damage +25% per rank.")
N("fleet", 3, "mdmg2", "Incendiary Payload", {"hero_missile_damage": 0.10}, ranks=3, text="Missile damage +10% per rank.")
N("fleet", 4, "dr", "Ablative Hull", {"hero_hull": 0.12}, ranks=2, text="Hero hull +12% per rank.")
N("fleet", 4, "mcd2", "Overpressure Feed", {"hero_missile_cd": -1.0}, ranks=2, text="Missile cooldown −1s per rank (floor 11s).")
N("fleet", 5, "respawn", "Emergency Reconstruction", {"hero_respawn": 8}, ranks=1, cost=650,
  text="Hero respawns in 8s instead of 15s.")
CAP("fleet", "fleet_doctrine", "cap_dread", "Dreadnought",
    {"hero_hull": 1.0, "hero_move_speed": -0.25}, 2400, 6, "Double hull, hero moves 25% slower.")
CAP("fleet", "fleet_doctrine", "cap_inter", "Interceptor",
    {"hero_move_speed": 0.40, "hero_missile_cd": -1.5, "hero_hull": -0.20}, 2400, 6,
    "+40% drag speed and reload, −20% hull.")

# ---------------- D · Sentinel Protocols ----------------
N("sentinel", 1, "eff", "Protocol Tuning", {"ability_effect": 0.05}, ranks=5, text="Ability effect +5% per rank.")
N("sentinel", 2, "cd", "Capacitor Banks", {"ability_cooldown": -0.04}, ranks=5, text="Ability cooldowns −4% per rank.")
N("sentinel", 3, "rad", "Field Projection", {"ability_radius": 0.08}, ranks=4, text="Ability radius / duration +8% per rank.")
N("sentinel", 4, "ready", "Warm Start", {"ability_start_ready": 1}, ranks=1, cost=320,
  text="Abilities start each run off cooldown.")
N("sentinel", 4, "eff2", "Resonance Cascade", {"ability_effect": 0.06}, ranks=3, text="Ability effect +6% per rank.")
N("sentinel", 5, "cd2", "Superconductors", {"ability_cooldown": -0.10}, ranks=1, cost=600,
  text="Ability cooldowns −10%.")
N("sentinel", 5, "core", "Core Refinement", {"core_gain": 0.25}, ranks=1, cost=600,
  text="Sentinel Core gain +25%.")
CAP("sentinel", "sent_doctrine", "cap_rapid", "Rapid Protocols",
    {"ability_cooldown": -0.20, "ability_effect": -0.15}, 2400, 6, "−20% cooldowns, −15% ability effect.")
CAP("sentinel", "sent_doctrine", "cap_heavy", "Heavy Protocols",
    {"ability_effect": 0.30, "ability_cooldown": 0.15}, 2400, 6, "+30% ability effect, +15% cooldowns.")

# ---------------- E · Logistics ----------------
N("logistics", 1, "rd", "Data Sifting", {"rd_gain": 0.06}, ranks=5, text="Research Data gain +6% per rank.")
N("logistics", 1, "xp", "After-Action Analysis", {"xp_gain": 0.06}, ranks=5, text="XP gain +6% per rank.")
N("logistics", 1, "credits", "Forward Depot", {"start_credits": 40}, ranks=3, text="Start each run with +40 credits per rank.")
N("logistics", 2, "core", "Core Salvage", {"core_gain": 0.10}, ranks=3, text="Sentinel Core gain +10% per rank.")
N("logistics", 2, "cards", "Requisition Options", {"card_options": 1}, ranks=1, cost=140,
  text="Card draft offers 3 options instead of 2. (draft: later)")
N("logistics", 3, "income", "Battlefield Reclamation", {"wave_income": 0.10}, ranks=3, text="In-run income per wave +10% per rank.")
N("logistics", 3, "alloy", "Exotic Assay", {"alloy_gain": 0.15}, ranks=2, text="Exotic Alloy gain +15% per rank.")
N("logistics", 4, "reroll", "Contingency Planning", {"card_reroll": 1}, ranks=2, cost=320,
  text="+1 card draft reroll per run per rank. (draft: later)")
N("logistics", 4, "rd2", "Deep Archives", {"rd_gain": 0.08}, ranks=3, text="Research Data gain +8% per rank.")
N("logistics", 5, "loss", "No Wasted Sorties", {"loss_reward": 1.0}, ranks=1, cost=550,
  text="A lost run pays 100% of its rewards.")
CAP("logistics", "log_doctrine", "cap_prospect", "Prospector",
    {"rd_gain": 0.35}, 2200, 5, "+35% Research Data, no Alloy bonus.")
CAP("logistics", "log_doctrine", "cap_vanguard", "Vanguard",
    {"alloy_gain": 0.35}, 2200, 5, "+35% Exotic Alloy, no Data bonus.")

# fill some effects defaults
for n in nodes:
    n.setdefault("is_capstone", False)

with open(OUT, "w") as f:
    json.dump(nodes, f, indent=2)
print(f"wrote {OUT} — {len(nodes)} nodes "
      f"({sum(1 for n in nodes if not n['is_capstone'])} ranked, {sum(1 for n in nodes if n['is_capstone'])} capstones)")
for b in BRANCH_GATE:
    print(f"  {b:14} {sum(1 for n in nodes if n['branch']==b)} nodes")
