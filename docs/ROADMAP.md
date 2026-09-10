# Beyond — roadmap & what's left

Living doc. Update it when you finish or start something. Last touched: **v0.11.0,
2026-09-10**.

---

## Current status

**v0.11.0** shipped. The five progression layers from the spec are all built, and
the wave system has been replaced with a 5-minute survival loop everywhere.

| System | State |
|---|---|
| Deterministic sim, 1×–4× speed, tick queue | done, verified (incl. 1×-vs-4× identity) |
| Survival loop — continuous escalating spawn director, real-time base management, per-minute card drafts, timed win / open-ended endless | done (`src/Sim/Systems/SurvivalDirector.cs`), **balance untuned** |
| 8 turrets (L1–L3 + A/B fork), 10 enemies + boss, 12 abilities, hero | done |
| Research tree (5×6), Commander/hero levels, ability leveling | done |
| In-run card draft (4 options) + level-up cards (4 options) | done |
| Ascension (10 tiers), Endless, Weekly Challenge (8 rotating twists) | done |
| Codex with first-encounter gating | done |
| **Shop** — Commendations currency + Fleet / Worlds / Ordnance (cosmetic only) | done (v0.10.0) |
| Loadout presets (3 slots, Protocols screen) | done (v0.11.0) |
| Menu — procedural key-art backdrop, rotating Earth, animated `GlowButton`s | done (v0.11.0) |
| Per-level space backdrops (`assets/game/bg/`) | done (v0.11.0) |
| In-app updater (top-of-menu card) | done |
| Art / sound | Kenney CC0 sprites + audio, shader Earth, NASA/ESA level backdrops. No AI-sprite pipeline. |
| Music | Linkin Park tracks in `assets/music/` — **personal build only, remove before any public release** (`assets/music/README.txt`) |

---

## THE BLOCKER — balance pass (owner: user, on device)

**Nothing below this line gets built until the survival loop is proven fun on a
4-year-old mid-range Android at 4×.** If the loop needs restructuring, arc 2 and
the remaining shop tabs would get rebuilt — don't spend the time twice.

All balance knobs are in config:
- `data/survival.json` — the whole spawn director + difficulty scale
- `data/balance.json`, `data/turrets.json`, `data/enemies.json`,
  `data/abilities.json`, `data/hero.json`, `data/research.json`,
  `data/cards.json`, `data/levelcards.json`, `data/ascension.json`
- `data/missions/*.json` (regenerate with `tools/gen_missions.py`)

Gates the user set (bring numbers, not impressions):
- **Determinism** — same seed + inputs at 1× and 4× → identical end state.
  *(SimTest checks this now; keep it passing.)*
- **Performance** — 60 fps @ 1×, hard floor 30 fps @ 4×, with a Carrier spawning
  Skiffs + 300+ entities + Nova Pulse and Kinetic Barrage firing together. No GC
  hitches mid-hold (means pooling is incomplete). Confirm particle budget scales
  down at 3×/4×.
- **Pacing** — a hold reads in 6–10 min at the chosen speed; check at 2×/3×/4×.
- **Hero feel** — 15 s volley cooldown: rhythm or wait? Volley readable at 4×?
  Drag responsive at 4×? If any fail, `hero.json volley_cooldown` moves first.
- **Difficulty** — early missions ~80% first-attempt clear, boss ~40%. Every loss
  still pays out, visibly, on the defeat screen.
- **Dominant turret / dead ability** — if one turret is built every run or the
  12 abilities collapse to 3 real picks, buff the weak ones, don't nerf the good.

Deliverable back from the pass: the numbers above + a list of every value changed
+ confirmation each lived in config, not code.

---

## Next, in order (after the loop holds up)

1. **Arc 2** — 8 more survival missions. `tools/gen_missions.py` +
   `data/arc_01.json` are the templates (arc file would be `arc_02.json`, wired
   in `ConfigDb` / `AppRoot` / `StarMapScreen`). New enemies debut one per
   mission. Spec: 6 arcs × 8 = 48 missions total.
2. **Remaining Shop tabs** (`src/Meta/Shop.cs`, `data/shop.json`,
   `src/UI/ShopScreen.cs` — pattern established):
   - **Command Deck** (Research Data) — QoL unlocks: extra card-draft reroll,
     turret blueprint presets, extra loadout preset slots beyond 3.
   - **Field Supplies** (Research Data) — pre-run consumables, **≤15% swing, never
     required**. Needs a pre-launch "arm a supply" UI. Watch for stockpiling.
   - **Archive** (Exotic Alloy) — late-game prestige: Ascension reroll tokens,
     long-form codex, endless-board nameplates. (Spec's own cut candidate — low
     priority.)
3. **Spec §14 UX polish**: hold-to-inspect any enemy mid-hold (HP/armour/shields/
   immunities/codex line); "Simulate" for 3-starred missions (instant resolve,
   reduced reward); damage-attribution already exists on the end card.
4. **Cloud save** (spec §14, "day one" — deferred as it's a personal sideload).
5. **AI sprite pipeline** — ComfyUI on the RTX 5070 for real enemy/turret art;
   Kenney greybox is the current base. Free-asset rule still applies to anything
   downloaded.
6. Weekly twist tuning (some reached much deeper than base — see notes).

---

## Known gaps / watch-list

- **APK is ~214 MB** (debug + ~100 MB `assets/music/`). Fine for sideload; would
  need addressing for any store.
- Survival balance: the scripted SimTest bot wins m01/m02, dies ~2 min on
  m03–m08 (no research, doesn't adapt to mechanics). Real players will go
  further; the mid-game ramp likely still needs softening. Levers in
  `data/survival.json`.
- `MEMORY.md` / `~/.claude/.../memory/sentinel-game.md` on the original dev
  machine has the fullest running history; this file is the in-repo summary.
- **`~/Work/pdtd-reference/`** (outside this repo) — the decrypted config of
  *Planet Defense: Space TD*, the game Beyond is modelled on. `NUMBERS.md` there
  is the distilled reference: 11 weapons' base stats, enemy roster, the
  time-windowed wave format, per-level hp/atk multiplier curve, planet shield
  curve, 208 upgrade cards, global battle constants. Use it to calibrate ratios,
  not to copy tables/strings.
- Asset API keys (Sketchfab / Poly Pizza) in `~/.config/sentinel/asset-api-keys.env`,
  unused by any tooling yet. Freesound key not created.
