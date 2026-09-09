#!/usr/bin/env python3
"""Generate the arc-1 mission files. Data stays as real JSON in data/missions/;
this just keeps the 8 curves consistent and easy to retune."""
import json, os, math

OUT = os.path.join(os.path.dirname(__file__), "..", "data", "missions")

# per mission: (id, name, intro, seed, waves, roster with debut wave)
# roster entry: enemy -> first wave it may appear (1-indexed)
MISSIONS = [
    ("m01", "Seed Vault", "The vault world. Nothing complicated yet — learn the ship and the arcs.", 20260901, 16,
     {"skiff": 1, "hauler": 4}),
    ("m02", "Fast Movers", "Interceptors. Slow shells sail right past them — you need hitscan or the hero.", 20260902, 16,
     {"skiff": 1, "hauler": 3, "interceptor": 2}),
    ("m03", "The Shielded", "Aegis Cruisers regenerate a shield. Break it fast or you never touch the hull.", 20260903, 17,
     {"skiff": 1, "hauler": 3, "interceptor": 4, "aegis_cruiser": 2}),
    ("m04", "Standoff", "Bombards stop outside your turret range and shell the planet. Reach out or send the hero.", 20260904, 17,
     {"skiff": 1, "hauler": 4, "aegis_cruiser": 3, "bombard": 2}),
    ("m05", "The Carrier", "A Carrier prints Skiffs until it dies. Decide what to kill first.", 20260905, 18,
     {"skiff": 1, "hauler": 4, "interceptor": 3, "bombard": 4, "carrier": 3}),
    ("m06", "Blink & Bite", "Phase Runners skip your obstacles. Leeches ride a turret and switch it off for ten seconds.", 20260906, 18,
     {"skiff": 1, "hauler": 4, "aegis_cruiser": 3, "phase_runner": 2, "leech": 3}),
    ("m07", "Wardens", "Wardens heal and shield everything near them, and Siege Crawlers ignore every slow you own.", 20260907, 19,
     {"skiff": 1, "hauler": 3, "aegis_cruiser": 3, "bombard": 4, "warden": 2, "siege_crawler": 3}),
    ("m08", "The Threshing Gate", "Everything the Harvest has sent so far, and then the thing behind them.", 20260908, 20,
     {"skiff": 1, "hauler": 2, "interceptor": 3, "aegis_cruiser": 3, "bombard": 4, "carrier": 5,
      "phase_runner": 6, "leech": 6, "warden": 7, "siege_crawler": 7}),
]

# rough "threat weight" so wave budgets scale sanely
WEIGHT = {"skiff": 1, "interceptor": 2, "leech": 2, "phase_runner": 3, "hauler": 6, "aegis_cruiser": 6,
         "bombard": 5, "warden": 6, "siege_crawler": 8, "carrier": 12}


def wave_budget(w, total):
    # ramp from ~6 to ~60 across the mission
    f = w / total
    return 6 + 56 * (f ** 1.35)


def build_wave(wnum, total, roster, rng):
    avail = [e for e, dbut in roster.items() if wnum >= dbut]
    budget = wave_budget(wnum, total)
    groups = []
    # debut a new enemy small and alone
    debut = [e for e, d in roster.items() if d == wnum and e != "skiff"]
    for e in debut:
        groups.append({"enemy": e, "count": 1 if WEIGHT[e] >= 6 else 3,
                       "interval": 1.2, "arc_center_deg": -1})
        budget -= WEIGHT[e] * (1 if WEIGHT[e] >= 6 else 3)

    spent = 0
    guard = 0
    # bias toward skiffs early, heavies later
    while spent < budget and guard < 20:
        guard += 1
        f = wnum / total
        pool = avail[:]
        pick = pool[rng() % len(pool)]
        wt = WEIGHT[pick]
        if wt >= 6 and f < 0.3 and rng() % 2 == 0:
            continue
        maxcount = max(1, int((budget - spent) / wt))
        if pick == "skiff":
            count = min(maxcount, 6 + int(28 * f) + rng() % 6)
        elif wt <= 2:
            count = min(maxcount, 3 + int(10 * f) + rng() % 4)
        else:
            count = min(maxcount, 1 + int(4 * f) + rng() % 3)
        count = max(1, count)
        interval = 0.7 - 0.4 * f if pick == "skiff" else max(1.0, 2.6 - 1.4 * f)
        g = {"enemy": pick, "count": count, "interval": round(interval, 2)}
        if rng() % 3 == 0:
            g["start_delay"] = round(1 + 4 * (rng() % 5) / 5, 1)
        if rng() % 4 == 0 and pick == "skiff":
            g["arc_center_deg"] = (rng() % 12) * 30
            g["arc_spread_deg"] = 60 + rng() % 40
        else:
            g["arc_center_deg"] = -1
        groups.append(g)
        spent += count * wt
    return {"groups": groups}


def rng_factory(seed):
    s = [seed & 0xFFFFFFFF]
    def nxt():
        s[0] = (s[0] * 1664525 + 1013904223) & 0xFFFFFFFF
        return s[0] >> 8
    return nxt


for mid, name, intro, seed, waves, roster in MISSIONS:
    rng = rng_factory(seed)
    mission = {
        "id": mid,
        "name": name,
        "intro": intro,
        "seed": seed,
        "waves": [build_wave(w, waves, roster, rng) for w in range(1, waves + 1)],
    }
    if mid == "m08":
        mission["waves"].append({"groups": [
            {"enemy": "hauler", "count": 4, "interval": 1.5, "arc_center_deg": -1},
            {"enemy": "boss_threshing_gate", "count": 1, "interval": 1, "start_delay": 3, "arc_center_deg": -1},
            {"enemy": "skiff", "count": 20, "interval": 0.5, "start_delay": 6, "arc_center_deg": -1},
        ]})
    path = os.path.join(OUT, f"{mid}.json")
    with open(path, "w") as f:
        json.dump(mission, f, indent=2)
    print(f"wrote {path}  ({len(mission['waves'])} waves)")
