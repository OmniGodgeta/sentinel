#!/usr/bin/env python3
"""Generate data/skillcards.json from Planet Defense TD's own skill_upgrade_data.

Beyond's in-run draft used to be "pick a weapon, it gains a level" with invented
card names. PDTD's is a per-weapon *menu* of 17-21 distinct named upgrades each
("Power Link", "Extended Reach", "Link Burst", …), each with its own description,
its own cap on how many times you may take it, and gates on the weapon's level
and star count. This script copies that structure across verbatim.

    python tools/gen_skillcards.py            # writes data/skillcards.json

Source: ~/Work/pdtd-reference/config-json/config/data/skill_upgrade_data.json
(decrypted from the shipping game; see that repo's README). Standing CLAUDE.md §4
user exception covers using it.

WHAT IS AND ISN'T FAITHFUL
--------------------------
Every card keeps PDTD's *identity*: title, description text (with the real
numbers substituted into its {statsPct.foo} placeholders), `selectableTimes`,
`needLevel` and `depSuperStar`. That's the part the player reads.

The *effects* are mapped onto what Beyond actually simulates, in three tiers:

  exact    - a stat Beyond already has: damage, radius, duration, count,
             tick rate, cooldown. Mapped 1:1 through EFFECT_MAP below.
  trait    - a named behaviour flag ("trait:rad_line:RadiationLineExplosion").
             The sim honours the ones it implements and ignores the rest.
  approx   - PDTD cards whose whole point is a bespoke mechanic Beyond has no
             equivalent for (teleporting lasers, railgun-casts-waterdrop, …).
             Rather than ship a card that reads well and does nothing, these get
             a damage bonus of equivalent weight and are tagged "approx": true
             in the output so they're easy to find and upgrade later.

Nothing here invents a card PDTD doesn't have. Cards for weapons Beyond doesn't
run (Railgun) are skipped rather than reassigned.
"""

import json
import os
import re

REF = os.path.expanduser("~/Work/pdtd-reference/config-json/config/data")
REPO = os.path.join(os.path.dirname(__file__), "..")
OUT = os.path.join(REPO, "data", "skillcards.json")

# PDTD skill id -> Beyond OrbitalWeaponDef.Kind. 10003 Railgun has no Beyond
# counterpart (the orbital cannon was removed in v0.26.4) so it is left out.
WEAPONS = {
    10001: ("battery", "Missile"),
    10002: ("waterdrop", "Waterdrop"),
    10004: ("laser", "Laser"),
    10005: ("beam_laser", "Beam"),
    10006: ("rad_line", "Radiation Link"),
    10007: ("rad_zone", "Radiation Zone"),
    10008: ("space_bomb", "Space Bomb"),
    10009: ("force_field", "Force Field"),
    10010: ("lightning", "Chain Lightning"),
    10011: ("shock_orb", "Ball Lightning"),
}

# PDTD stat field -> Beyond per-weapon stat. Percent fields are divided by 100;
# "dec" fields are absolute adds.
PCT_MAP = {
    "defaultDmgBonus": "damage",
    "skillRadiusBonus": "radius",
    "explosionRadiusBonus": "explosion_radius",
    "durationBonus": "duration",
    "tickRateBonus": "tick_rate",
    "rotateSpeedBonus": "rotate_speed",
    "stunDurationBonus": "stun_duration",
    "stunChance": "stun_chance",
    "knockbackForceBonus": "knockback",
    "slowEffect": "slow",
    "searchRangeMultiplierBonus": "radius",
    "strikeRangeBonus": "radius",
    "laserDeflectSizeBonus": "radius",
    "laserSplitSizeBonus": "radius",
    "bounceAgainChance": "bounce_chance",
    "triggerOneMoreChance": "bounce_chance",
    "increaseCDSpeed": "rate",
    "maxCDSpeedBonusOnKill": "rate",
}

# Anything that adds *more of the thing* — bounces, chains, links, satellites,
# split shots — folds into one "count" stat, which is how Beyond's weapons are
# parameterised (each kind reads count for whatever its own multiplicity is).
DEC_COUNT = {
    "attackCount", "penetratedCount", "bounceCount", "chainTargetCount",
    "smallChainTargetCount", "strikeCount", "satelliteCount", "satelliteZoneCount",
    "laserSplitNum", "refractAgainCount", "refractOnHitCount", "refractAgainAgainCount",
    "deflectRefractCount", "refractDeflectCount", "laserDeflectCount", "beamRefractCount",
    "splitOnHitCount", "splitBulletCount", "splitMiniCount", "splitOnHitSmallRailgunCount",
    "maxSplitCount", "landMineCount", "smallCountOnHit", "smallBeamCount",
    "durabilityPoints", "splitBulletDP", "countMult", "missileCountPerSec",
    "aquaCountPerSec", "cdMissileFireCount", "cdMissilePerFire",
    "launchSpiralMissilesCount", "burstOutSubMunitionsCount",
}
DEC_MAP = {
    "extendedLineLength": "extend_length",
    "stunDurationBase": "stun_duration_flat",
    "vulnerableTime": "duration_flat",
    "extendLifeTime": "duration_flat",
    "landMineDuration": "duration_flat",
    "collapseAreaDuration": "duration_flat",
    "electricFieldDuration": "duration_flat",
    "stayTime": "duration_flat",
    "pullForce": "knockback_flat",
    "pullDuration": "duration_flat",
    "additionalExplosionRadius": "explosion_radius_flat",
}

PLACEHOLDER = re.compile(r"\{(statsPct|statsDec|statsField|atkMethodDmgBonusPct)\.([A-Za-z_]+)\}")


def fill(desc, card):
    """Substitute PDTD's {statsPct.foo} placeholders with the card's real numbers."""
    def sub(m):
        bucket, key = m.group(1), m.group(2)
        val = card.get(bucket, {}).get(key)
        if val is None:
            return m.group(0)
        if isinstance(val, float) and val == int(val):
            val = int(val)
        return str(val)
    # PDTD writes "-{-statsPct.defaultDmgBonus}%" for penalties; collapse the
    # double negative so it reads "-20%" and not "--20%".
    desc = desc.replace("{-statsPct.", "{statsPct.")
    out = PLACEHOLDER.sub(sub, desc)
    return out.replace("--", "-").replace("\n", " ").strip()


def effects_for(card, kind):
    """Map one PDTD card's stat block onto Beyond per-weapon effect keys."""
    eff = {}

    def put(stat, v):
        k = f"ow:{kind}:{stat}"
        eff[k] = round(eff.get(k, 0.0) + v, 4)

    for k, v in card.get("statsPct", {}).items():
        if k in PCT_MAP:
            put(PCT_MAP[k], v / 100.0)
    for k, v in card.get("statsDec", {}).items():
        if k in DEC_COUNT:
            put("count", float(v))
        elif k in DEC_MAP:
            put(DEC_MAP[k], float(v))

    # "cooldown speed +30%" is expressed as a script call, not a stat
    act = card.get("action") or ""
    m = re.search(r"reduceCoolDown\([^)]*?(\d+)\s*\)", act)
    if m:
        put("rate", int(m.group(1)) / 100.0)
    if "addHpPercent" in act:
        m2 = re.search(r"addHpPercent\(([\d.]+)\)", act)
        if m2:
            eff["planet_heal"] = float(m2.group(1))

    for t in card.get("traitIds", {}) or {}:
        eff[f"trait:{kind}:{t}"] = 1
    for bucket in ("statsField", "boolStatsField"):
        for k, v in (card.get(bucket) or {}).items():
            eff[f"trait:{kind}:{k}"] = float(v)
    return eff


def main():
    src = json.load(open(os.path.join(REF, "skill_upgrade_data.json")))

    out = []
    approx = 0
    for cid, c in sorted(src.items(), key=lambda kv: int(kv[0])):
        sid = c["skillid"]
        if sid not in WEAPONS:
            continue                      # global heal cards + Railgun
        kind, wname = WEAPONS[sid]

        eff = effects_for(c, kind)
        # Is there anything here the sim can actually act on numerically?
        numeric = any(k.startswith(f"ow:{kind}:") for k in eff)
        is_unlock = c["weight"] >= 5000    # PDTD flags the "Release a X Sentinel" card this way
        if not numeric and not is_unlock:
            eff[f"ow:{kind}:damage"] = 0.25
            approx += 1

        out.append({
            "id": f"{kind}_{cid}",
            "kind": kind,
            "weapon": wname,
            "title": c["title"],
            "text": fill(c["desc"], c),
            "unlock": is_unlock,
            "max_picks": c["selectableTimes"] or 1,
            "need_level": c["needLevel"],
            "need_star": c["depSuperStar"],
            "weight": c["weight"],
            "effects": eff,
            **({"approx": True} if (not numeric and not is_unlock) else {}),
        })

    doc = {
        "_note": (
            "PDTD's real in-run upgrade cards, generated by tools/gen_skillcards.py from "
            "the shipping game's skill_upgrade_data. Titles and text are PDTD's verbatim. "
            "'unlock' cards put the sentinel into play; the rest add a star to it, and every "
            "4th star levels it up. 'need_level'/'need_star' gate a card on the weapon's "
            "level and star count. Cards tagged 'approx' stand in for a PDTD mechanic Beyond "
            "doesn't simulate yet and carry a damage bonus instead — edit freely, this file "
            "is regenerable but hand-edits are expected to survive (regen only on purpose)."
        ),
        "cards": out,
    }
    with open(OUT, "w") as f:
        json.dump(doc, f, indent=1)
        f.write("\n")

    per = {}
    for c in out:
        per[c["kind"]] = per.get(c["kind"], 0) + 1
    print(f"wrote {OUT}: {len(out)} cards, {approx} approximated")
    for k, n in sorted(per.items()):
        print(f"  {k:12s} {n}")


if __name__ == "__main__":
    main()
