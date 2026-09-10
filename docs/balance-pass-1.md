# Balance pass 1 — PDTD calibration (pre-playtest)

**Not the on-device balance pass** (that's still the user's — §4 of `ROADMAP.md`).
This is a config-only recalibration so the playtest starts from numbers derived
from `~/Work/pdtd-reference/` (the real shipping values of *Planet Defense: Space
TD*) instead of the original untuned guesses.

**Confirmation: every balance value changed lives in `data/*.json`.** One code
change (`SimWorld.StepTick`) — a determinism fix, not balance (below).

## Result

SimTest bot (no research, greedy first-card, doesn't adapt to mechanics):

| | before | after |
|---|---|---|
| m01–m03 | win / lose ~2 min | **win**, comfortable margins |
| m04 | lose ~2 min | lose at ~4:20 (290 kills — very close) |
| m05–m08 | lose ~2 min | lose 2–4 min |
| m07 **+ research mods** | integ 0 (loss) | **integ 593 (survives)** |
| 1×-vs-4× determinism | ok | ok |

The bot is a deliberately weak proxy. The key signal is the last row: a player
*with* the research tree flips m07 from loss to survival — the meta-progression
now carries its weight. A real arc player should clear ~m01–m06 and contest
m07–m08.

## What changed and why (calibrated against PDTD ratios)

### `data/enemies.json`
PDTD enemies scale **tanky, not deadly** (per-level hpMult ×1→×26 by L20, atkMult
only ×1→×7) and use almost no flat armour. Beyond's flat-armour subtraction was
punishing low-damage turrets into uselessness against heavies.

| enemy | field | before → after |
|---|---|---|
| skiff | hp / contact | 24→22 / 16→9 |
| hauler | armor / contact | 5→2 / 75→42 |
| interceptor | hp / contact | 40→38 / 20→12 |
| aegis_cruiser | armor / shield_hp / contact | 2→0 / 140→150 / 55→30 |
| bombard | armor / ranged_dmg / contact | 3→0 / 26→16 / 40→24 |
| carrier | armor / spawn_interval / contact | 6→3 / 2.6→3.0 / 90→50 |
| phase_runner | hp / contact | 70→66 / 30→18 |
| leech | leech_disable / contact | 10→8 / 8→6 |
| warden | armor / aura heal+shield / contact | 4→0 / 14+10→12+8 |
| siege_crawler | hp / armor / contact | 340→380 / 8→4 / 95→55 |
| boss | hp / armor / contact / bounty | 6000→9000 / 10→6 / 400→220 / 200→240 |

Net: contact damage down ~40 % across the board (planet has 1100 integrity + a
faster self-repair, and a leak shouldn't be near-lethal); armour mostly zeroed so
every turret can *hurt* everything, with a little left on siege/carrier/boss as a
"bring armour-pen" signal; heavy HP nudged up so the difficulty comes from
sponginess, not one-shots.

### `data/turrets.json`
No single dominant pick — spread the value, buff the weak (spec rule: buff the
unbuilt, don't nerf the good).

| turret | change |
|---|---|
| autocannon | damage 8→7, +armor_pen 2, `slug` fork armor_pen +6→+8 & dmg ×1.1→×1.15 |
| flak | damage 6→7, interval 0.6→0.58, splash 52→62, arc 70→74, forks stronger |
| railgun | damage 70→74, interval 1.7→1.6, armor_pen 8→10 (the heavy-killer) |
| tesla | damage 12→14, chain_range 100→105 |
| missile_silo | damage 34→40, interval 1.4→1.35 (the reliable homing mid) |
| laser_lattice | damage 14→18, ramp/s 0.8→1.0 (needs uptime, now rewards it) |
| graviton | damage 0→4, `strong` fork now dmg ×1.4 (was a dead-weight utility) |
| nanite_forge | support_buff 0.25→0.32, range 62→66, forks → Overclock / Broadcast |

### `data/survival.json` (the spawn director)
PDTD stage ≈ 330–360 s (Beyond's 300 already fine); their FORCE_RECOVERY threshold
of "5 enemies alive" implies normal play sits at low double-digit enemy counts,
not the swarms Beyond was pushing.

| knob | before → after | effect |
|---|---|---|
| eps_base | 0.35→0.28 | gentler opening rate |
| eps_ramp | 2.7→1.9 | ~3.0/s peak instead of ~4.5/s |
| level_spawn_factor | 0.085→0.075 | high-level missions open calmer |
| surge_a / surge_b | 0.30/0.18 → 0.28/0.16 | slightly flatter surges |
| soft_cap_base / ramp | 38/95 → 30/66 | ~95 concurrent peak instead of 150+ |
| scale_level_factor | 0.075→0.075 (kept) | L8 base scale ≈ 1.6× |
| scale_ramp | 0.70→0.95 | end-of-hold still tough (≈ 3.1× at L8 endgame) |
| endless_ramp_seconds | 200→150 | endless escalates faster (it's a score mode) |
| self_repair_frac_per_sec | 0.0016→0.0026 | ~15 %/min planet regen — chip damage recoverable |
| reward_credits_mult | 1.6→1.7 | slightly richer real-time economy |
| pincer_chance | 0.28→0.26 | marginally fewer tight clusters |

### `data/balance.json`
- `planet_integrity` 1000 → **1100**
- `starting_credits` 460 → **520** (a 5–6-turret opening, not 4)

### `data/hero.json`
- `max_hull` 400→440, `point_defense_dps` 16→20 (less idle between volleys),
  `point_defense_range` 170→175, `missile_damage` 55→62,
  `missile_splash_radius` 46→48. **`volley_cooldown` kept at 15 s** (spec
  non-negotiable).

## Code change (not balance)

`SimWorld.StepTick` now early-returns when `Phase is Won or Lost`. Without it, a
4× frame that batches multiple ticks could run 1–3 ticks *past* the moment the
outcome is decided, so the 1×-vs-4× SimTest check saw a 1-tick divergence
(state was identical, only the tick counter differed). This makes the sim
byte-identical regardless of frame batching — the determinism guarantee is now
airtight. No gameplay effect (the real game already pauses the clock on Won/Lost).

## Still for the on-device pass

Everything in `ROADMAP.md` §4 — fps at 4×, GC hitches, hero *feel*, the real
difficulty percentages with a real player, dominant-turret / dead-ability checks.
This pass only moved the numbers into a sane, PDTD-anchored range.
