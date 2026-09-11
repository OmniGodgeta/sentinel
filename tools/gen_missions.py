#!/usr/bin/env python3
"""Generate the arc-1 mission files.

Beyond is a survival game: each mission is a timed hold (default 5 minutes) with a
continuous, escalating spawn director instead of discrete waves. This file just
keeps the 8 difficulty curves consistent and easy to retune.

Mission shape consumed by the sim (see src/Config/Defs.cs MissionDef):
  survival  : true
  duration  : seconds to hold (0 = endless)
  level     : difficulty scale index — higher = stronger/tougher/faster enemies
  roster    : { enemy_id: unlock_fraction }  fraction of the run before it appears
  boss      : optional enemy id spawned once near the end
"""
import json, os

OUT = os.path.join(os.path.dirname(__file__), "..", "data", "missions")
DURATION = 300

# per mission: (id, name, intro, seed, level, roster{enemy: unlock_fraction})
MISSIONS = [
    ("m01", "Seed Vault",
     "The vault world. Nothing complicated yet — learn the ship and the arcs. Hold for five minutes.", 20260901, 1,
     {"skiff": 0.0, "hauler": 0.25}),
    ("m02", "Fast Movers",
     "Interceptors. Slow shells sail right past them — you need hitscan or the hero.", 20260902, 2,
     {"skiff": 0.0, "hauler": 0.15, "interceptor": 0.2}),
    ("m03", "The Shielded",
     "Aegis Cruisers regenerate a shield. Break it fast or you never touch the hull.", 20260903, 3,
     {"skiff": 0.0, "hauler": 0.15, "interceptor": 0.25, "aegis_cruiser": 0.2}),
    ("m04", "Standoff",
     "Bombards stop outside your turret range and shell the planet. Reach out or send the hero.", 20260904, 4,
     {"skiff": 0.0, "hauler": 0.2, "aegis_cruiser": 0.2, "bombard": 0.15}),
    ("m05", "The Carrier",
     "Carriers print Skiffs until they die. Decide what to kill first.", 20260905, 5,
     {"skiff": 0.0, "hauler": 0.2, "interceptor": 0.2, "bombard": 0.3, "carrier": 0.25}),
    ("m06", "Blink & Bite",
     "Phase Runners skip your obstacles. Leeches ride a turret and switch it off for ten seconds.", 20260906, 6,
     {"skiff": 0.0, "hauler": 0.2, "aegis_cruiser": 0.2, "phase_runner": 0.15, "leech": 0.25}),
    ("m07", "Wardens",
     "Wardens heal and shield everything near them; Siege Crawlers ignore every slow you own.", 20260907, 7,
     {"skiff": 0.0, "hauler": 0.15, "aegis_cruiser": 0.2, "bombard": 0.3, "warden": 0.15, "siege_crawler": 0.25}),
    ("m08", "The Threshing Gate",
     "Everything the Harvest has sent so far — and then the thing behind them.", 20260908, 8,
     {"skiff": 0.0, "hauler": 0.1, "interceptor": 0.2, "aegis_cruiser": 0.2, "bombard": 0.25,
      "carrier": 0.35, "phase_runner": 0.4, "leech": 0.4, "warden": 0.5, "siege_crawler": 0.5}),
]

# gentle early-campaign ramp — L1 is a near-pushover, easing to full difficulty by ~L6
DIFFICULTY = {1: 0.40, 2: 0.52, 3: 0.66, 4: 0.80, 5: 0.90, 6: 1.0, 7: 1.08, 8: 1.15}

for mid, name, intro, seed, level, roster in MISSIONS:
    mission = {
        "id": mid,
        "name": name,
        "intro": intro,
        "seed": seed,
        "survival": True,
        "duration": DURATION,
        "level": level,
        "difficulty": DIFFICULTY.get(level, 1.0),
        "roster": roster,
        "backdrop": mid,
        "music": f"eve_{level:02d}",
    }
    if mid == "m08":
        mission["boss"] = "boss_threshing_gate"
    path = os.path.join(OUT, f"{mid}.json")
    with open(path, "w") as f:
        json.dump(mission, f, indent=2)
    print(f"wrote {path}  (survival L{level}, {DURATION}s, roster {len(roster)})")

# endless: an open-ended survival with the full roster
endless = {
    "id": "endless",
    "name": "The Long Watch",
    "intro": "One planet. No timer. See how deep you can hold.",
    "seed": 77000,
    "survival": True,
    "endless": True,
    "duration": 0,
    "level": 4,
    "backdrop": "endless",
    "music": "eve_11",
    "roster": {
        "skiff": 0.0, "hauler": 0.03, "interceptor": 0.06, "aegis_cruiser": 0.1, "bombard": 0.13,
        "carrier": 0.18, "phase_runner": 0.2, "leech": 0.22, "warden": 0.28, "siege_crawler": 0.3,
    },
    "endless_roster": ["skiff", "hauler", "interceptor", "aegis_cruiser", "bombard",
                       "carrier", "phase_runner", "leech", "warden", "siege_crawler"],
}
with open(os.path.join(OUT, "endless.json"), "w") as f:
    json.dump(endless, f, indent=2)
print("wrote endless.json  (open-ended survival)")
