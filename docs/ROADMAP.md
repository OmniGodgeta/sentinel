# Beyond — roadmap & handoff

**Canonical "what's done / what's next" doc.** A fresh agent should be able to
pick up from here alone. Pair with [`../CLAUDE.md`](../CLAUDE.md) (ground rules)
and [`design-spec.md`](design-spec.md) (the vision) / [`deviations.md`](deviations.md)
(where the build deliberately differs).

Last updated: **end of the v0.9→v0.11 session, 2026-09-10.** Update this file when
you finish or start anything.

---

## The map

```
 DONE ─────────────────────────────────────────────►  v0.11.0 (shipped)
  greybox loop ─ full rosters+arc1+boss ─ research/levels/abilities ─
  card draft ─ ascension/endless/weekly ─ codex ─ art+sound pass ─
  WAVES→SURVIVAL ─ Shop ─ loadout presets ─ menu/backdrops ─ updater

                              │
                              ▼
        ┌───────────────────────────────────────────────┐
        │  ⛔ THE BLOCKER — balance pass                  │   owner: USER
        │  on a 4-yr-old Android, at 4×.                 │   deliverable:
        │  gates: determinism · perf · pacing · hero     │   numbers +
        │  feel · difficulty% · no dominant turret/dead  │   list of every
        │  ability.   all knobs in data/survival.json    │   value changed
        └───────────────────────────────────────────────┘
                              │  (loop proven)
                              ▼
   1. ARC 2  ──►  2. Shop tabs      ──►  3. UX polish     ──►  4. Cloud save
   (8 missions)   (Command Deck /       (hold-to-inspect,      5. AI sprite
   +new enemies    Field Supplies /      "Simulate")            pipeline
   scale curves    Archive)                                    6. weekly tuning
   from PDTD ref                                               7. APK slim / release build

   reference throughout:  ~/Work/pdtd-reference/NUMBERS.md   (the cloned game's real numbers)
```

## 0. TL;DR

- **What it is:** radial tower-defense for Android. Godot 4.7.2 **mono** / C#
  (`net9.0`). Repo `github.com/OmniGodgeta/sentinel` (**public**). Product name
  "Beyond"; assembly/namespace stay `Sentinel`. 100% free, no ads, no IAP — a hard
  architectural rule.
- **Where it's at:** **v0.11.0**, released, CI green. Playable end to end. The
  wave system was replaced this session with a **5-minute survival loop**.
- **The one thing blocking everything:** the **balance pass** (§4). It's the
  user's to run on a real cheap Android. Do not build arc 2 / shop tabs before
  the loop is proven — they'd get rebuilt.
- **Big asset this session:** `~/Work/pdtd-reference/` — the fully decrypted
  config of *Planet Defense: Space TD*, the game Beyond clones (§6).

---

## 1. What this session did (v0.8.0 → v0.11.0)

Starting point was v0.8.0 (wave-based, greybox-ish, all 5 progression layers built).

### v0.9.0 / v0.9.1 — survival redesign + music + updater
- **Waves → survival everywhere.** Every arc mission + Endless + Weekly is now one
  continuous, escalating 5-minute hold — no discrete waves. New
  `src/Sim/Systems/SurvivalDirector.cs`: deterministic spawn director, gentle
  open easing in over ~90 s then climbing; overlapping sine "surges"; concurrency
  soft-cap; boss spawned once near the end (m08).
- `DifficultyScale` (in `SimWorld`) **replaced `EndlessScale`** — enemy
  hp/shield/contact/bounty scale with `Mission.Level` **and** elapsed time.
- One initial **PREP** build phase, then **real-time** build/upgrade/sell/card-pick
  (`SimWorld.CanEdit`). `⚒` toggle in the HUD top row opens the build panel mid-fight.
- Card draft every in-run minute; the sim **freezes** (`GameRoot.DraftPause`,
  distinct from a user pause) until you pick — `RequestPickCard` hand-pumps one
  `StepTick` to apply it (v0.9.1 fix; without it the run froze forever).
- Win = hold the full `Duration` then clear the field; Endless/Weekly are
  open-ended, "best" tracked as **seconds survived** (mm:ss on the menu).
- Slow planet self-repair so steady chip damage isn't an automatic loss.
- `MissionDef` gained `survival / duration / level / roster{id:unlockFrac} / boss / backdrop`.
  `tools/gen_missions.py` rewritten. SimTest drives real-time base mgmt + drafts.
- **In-app updater** `src/Meta/UpdateChecker.cs` — first menu visit each launch,
  checks the public GitHub releases API vs `application/config/version`, shows a
  slim dismissible card pinned to the **top** of the menu.
- **HUD** drops below the display safe area (notch) —
  `DisplayServer.GetDisplaySafeArea()` → `GameRoot.SafeTopInset`, `TopReserve`
  grows by it. Bigger fonts/targets throughout.
- **Music**: curated Linkin Park mp3s in `assets/music/{menu,game}/` (~100 MB).
  **Personal build only** — copyrighted, flagged in `assets/music/README.txt`,
  must be removed before any public release. `MusicPlayer.LoadDir` forces
  `Loop=false` so the playlist advances.
- **4-option upgrade cards** (in-run draft + level-up).
- **Repo made public** so the updater can read releases.

### v0.10.0 — the Shop (design-spec §10)
- **Commendations** currency. Balance is **derived** (`Shop.TotalEarned()` −
  `Shop.Spent()`, clamped ≥ 0) — no stored counter to desync. Earned from mission
  stars / Ascension tiers / weekly / Codex entries / endless depth; rates in
  `data/shop.json`.
- `src/Meta/Shop.cs` + `src/UI/ShopScreen.cs`, 3 tabs, **cosmetic only**:
  - **Fleet Requisition** — 7 hero hulls (`assets/game/hulls/*.png`, Kenney CC0),
    mechanically identical. `Save.Options.HullSkin` → `Art.Hull(id)` → `SimRenderer.DrawHero`.
  - **Worlds** — the 6 planet skins, now Commendation-gated (removed from Settings).
  - **Ordnance Palettes** — 5 VFX tint sets → `Art.Ordnance(id)` → volley/missile FX.
- Item `apply` field is `"category:value"`; buying auto-equips; cost 0 = auto-owned.
- **Turret rank visuals** — L2/L3 scale gun+base up and brighten, L3 accent ring,
  fork shows a coloured tab + arrow (yellow A / blue B).

### v0.11.0 — menu rework + backdrops + presets + data-driven survival
- **Menu** `MenuBackground` rewritten: procedural "key-art" look (teal nebula
  left / magenta right, twinkling starfield, **rotating shader Earth centred**).
  Photo mode kept (`Image=`, used by star map = `bg/weekly.jpg`).
- `earth.gdshader` textures **mipmapped** (`filter_linear_mipmap` +
  `mipmaps/generate=true`) — fixes moiré banding on the globe at menu scale.
- `src/UI/GlowButton.cs` — self-drawn animated menu button (`BaseButton` + child
  `Label`): idle shimmer, hover lift+glow, magenta press flash, per-button teal /
  `Alt`=magenta accent, `Primary`=filled CTA. Used for PLAY/WEEKLY/nav grid.
- **Per-level space backdrops** `assets/game/bg/*.jpg` (~2 MB): NASA/ESA Hubble &
  JWST (public domain) + the EHT M87* black hole for m08 (CC BY 4.0, credited).
  `MissionDef.Backdrop`; `GameRoot.AddBackdrop` (CanvasLayer −5, `KeepAspectCovered`
  + scrim). `gen_missions.py` sets `"backdrop": mid`.
- **Loadout presets** (spec §14) — 3 slots, `1/2/3` row in the Protocols screen.
  `SaveGame.LoadoutPresets / ActivePreset / SwitchPreset / CommitLoadout /
  NormalizePresets` (migrates on load). Removes the re-equip friction for the
  balance pass.
- **Survival is 100% config-driven** — every spawn-director / difficulty constant
  that was a literal moved to `data/survival.json` + `SurvivalDef`.
- **SimTest** — new **1×-vs-4× speed-independence check** (`ClockRun`): same seed
  + tick-indexed inputs through a real `SimClock` at both speeds land
  byte-identical. Passes.

### Also this session
- `~/Work/pdtd-reference/` — decrypted PDTD config (§6). Pointer added below.
- `CLAUDE.md` + this file created as the agent entry points; `README.md` and
  `deviations.md` refreshed to current.

---

## 2. Architecture map

```
src/Sim/            deterministic simulation — nothing here reads wall-clock/frame time
  SimClock          fixed 60 Hz; speed = ticks/frame, not animation rate
  DetRandom         deterministic RNG (splitmix)
  SimWorld(.cs)     authoritative state + fixed-order StepTick; command queue
  Systems/
    SurvivalDirector  THE spawn loop (survival). tuned by data/survival.json
    SpawnSystem       legacy wave spawns (dead path, kept for reference)
    EnemySystem TurretSystem ProjectileSystem AbilitySystem HeroSystem
    PlanetDefenses    planet auto missile-battery + orbital sentinels
    CardDraft         in-run modifier-card draft (4 options)
src/Meta/
  SaveGame          user://save.json — NO premium-currency field, ever
  Progression       XP→Commander/hero levels, research gating, ability leveling
  ModifierSet       aggregated run modifiers (research + cards + hero level)
  Shop              Commendations + cosmetics (derived balance)
  WeeklyChallenge   ISO-week seed + 1 of 8 rotating twists
  UpdateChecker     GitHub-releases "update available" card
  ResearchDb
src/Config/
  ConfigDb          loads every data/*.json into typed defs at boot
  Defs              the typed records (BalanceDef, SurvivalDef, MissionDef, ...)
src/Render/
  SimRenderer       play-field draw (Kenney sprites + FX pool)
  PlanetView        shader Earth / Kenney planet skins
  Starfield  Art  ScreenFx
src/UI/
  Hud               in-mission HUD (status, speed, build panel, ability bar, drafts)
  MenuScreen + MenuBackground + GlowButton    landing screen
  StarMapScreen     level select (procedural star map)
  ResearchScreen AbilityScreen ShopScreen CodexScreen SettingsScreen LevelUpScreen
  UiTheme           shared dark teal/magenta theme
src/Game/
  AppRoot           app shell — swaps menu ↔ mission, banks rewards
  GameRoot          one mission — owns sim/clock/renderer/HUD, translates input
  SimTest           headless determinism + balance harness (scenes/SimTest.tscn)
  ShotRunner        dev screenshot harness (scenes/Shots.tscn)
data/               ALL balance values. survival.json = the whole spawn loop.
  missions/*.json   generated by tools/gen_missions.py (survival missions)
tools/              gen_missions.py, gen_research.py
assets/game/bg/     per-level space backdrops (NASA/ESA + EHT)
assets/music/       Linkin Park — personal build only, remove before public release
```

---

## 3. Current status table

| System | State |
|---|---|
| Deterministic sim, 1×–4×, 1×≡4× verified | done |
| Survival loop (`SurvivalDirector`) | done, **balance untuned** |
| 8 turrets (L1–3 + A/B fork), 10 enemies + boss, 12 abilities, hero | done |
| Research tree (5×6), Commander/hero levels, ability leveling | done |
| In-run card draft (4) + level-up cards (4) | done |
| Ascension (10 tiers), Endless, Weekly Challenge (8 twists) | done |
| Codex + first-encounter gating | done |
| Shop — Commendations + Fleet/Worlds/Ordnance (cosmetic) | done |
| Loadout presets (3 slots) | done |
| Menu key-art backdrop + rotating Earth + GlowButtons | done |
| Per-level backdrops | done |
| In-app updater | done |
| Art / sound | Kenney CC0 + shader Earth + NASA backdrops. No AI-sprite pipeline. |
| **Campaign** | **arc 1 only (8 missions).** Spec wants 6 arcs × 8 = 48. |
| Shop tabs Command Deck / Field Supplies / Archive | **not built** |
| Hold-to-inspect, "Simulate", cloud save | **not built** |
| Balance | **untuned — the blocker** |

---

## 4. THE BLOCKER — balance pass (owner: user, on device)

**Nothing in §5 gets built until the survival loop is proven fun on a
4-year-old mid-range Android at 4×.** If the loop needs restructuring, arc 2 and
the shop tabs would be rebuilt — don't spend the time twice.

All balance knobs are in config (no code edit should ever be needed for tuning):
- **`data/survival.json`** — the entire spawn director + difficulty-scale curve
  (eps base/ramp/curve, level factors, surge, soft-cap, boss timing, pincer
  chance, self-repair rate, per-minute reward mults, core cadence).
- `data/balance.json` `turrets.json` `enemies.json` `abilities.json` `hero.json`
  `research.json` `cards.json` `levelcards.json` `ascension.json` `shop.json`.
- `data/missions/*.json` — regenerate with `python3 tools/gen_missions.py`.

Gates the user set — **bring numbers, not impressions**:
1. **Determinism** (test first — everything depends on it): same seed + input
   sequence at 1× and 4× → identical end state. SimTest checks this now; keep it
   green. If it ever diverges, stop and fix the sim before any balance work.
2. **Performance**: 60 fps @ 1×, hard floor 30 fps @ 4×. Stress case: a Carrier
   spawning Skiffs + 300+ active entities + Nova Pulse and Kinetic Barrage
   firing together. No GC hitches mid-hold (means object pooling is incomplete).
   Confirm the particle budget actually scales down at 3×/4×.
3. **Pacing**: a hold reads in 6–10 min at the chosen speed — check at 2×/3×/4×,
   not just 1×.
4. **Hero feel** (most likely to change): 15 s volley cooldown ≈ 20–24 volleys
   over a hold — rhythm or wait? Volley readable at 4×? Drag responsive at 4×?
   If any fail, `hero.json volley_cooldown` is the first number to move.
5. **Difficulty**: early missions ~80% first-attempt clear, boss ~40%. Every loss
   pays out, visibly, on the defeat screen.
6. **Dominant turret / dead ability**: if one turret is built every run or the 12
   abilities collapse to ~3 real picks, **buff the weak ones, don't nerf the good
   one.**

**Deliverable back:** the numbers above + a list of every value changed +
confirmation each lived in config, not code. If any change needed a code edit,
that's a data-driving gap — flag it.

**Reference for calibration:** `~/Work/pdtd-reference/NUMBERS.md` (§6) has the
real shipping numbers from the game Beyond clones — use the *ratios*.

**Pass 1 done (v0.12.0, config-only, `balance-pass-1.md`)** — recalibrated
`enemies/turrets/survival/balance/hero.json` against PDTD ratios (enemies tanky
not deadly, armour mostly zeroed, gentler spawn rate, faster planet regen).
Bot now wins m01–m03, loses m04 at ~4:20, and — with research mods — *survives*
m07. Determinism made airtight (`StepTick` early-returns on terminal phase).
The on-device pass (fps, feel, real difficulty %) is still open.

---

## 5. Roadmap — after the loop holds up (in order)

1. **Arc 2** — 8 more survival missions. Templates: `tools/gen_missions.py` +
   `data/arc_01.json`. New arc file `arc_02.json`, wired in `ConfigDb` (currently
   hard-loads `arc_01.json`), `AppRoot`, `StarMapScreen` (currently one arc). Debut
   one new enemy per mission — the enemy roster (`data/enemies.json`) has 10 +
   boss; may need 2–3 new ones. Calibrate wave/roster ramps against
   `pdtd-reference` (their per-level hpMult goes ×1 → ×26 by L20 → ×1132 by L50;
   atkMult only ×1 → ×54 — enemies get spongy, not lethal).
2. **Remaining Shop tabs** (`src/Meta/Shop.cs` + `data/shop.json` +
   `src/UI/ShopScreen.cs` — pattern established):
   - **Command Deck** (Research Data) — QoL: extra card-draft reroll, turret
     blueprint presets, a 4th loadout preset slot.
   - **Field Supplies** (Research Data) — pre-run consumables, **≤15% swing,
     never required** (design-spec §10 hard rule). Needs a small "arm a supply"
     pre-launch UI. Watch for stockpiling in playtest.
   - **Archive** (Exotic Alloy) — Ascension reroll tokens, long-form codex,
     endless-leaderboard nameplates. Spec's own cut candidate — lowest priority.
3. **Spec §14 UX**: hold-to-inspect any enemy mid-hold (HP/armour/shields/
   immunities/codex line — great at 4×); "Simulate" for 3-starred missions
   (instant resolve, reduced reward). Damage-attribution already exists on the
   end card.
4. **Cloud save** — spec §14 calls it "day one"; deferred because it's a personal
   sideload. `SaveGame` is a single JSON — would need a backend (Firebase? the
   PDTD APK uses Firebase) or a simple gist/drive sync.
5. **AI sprite pipeline** — ComfyUI on the RTX 5070 for real enemy/turret art.
   Kenney greybox is the base now. CC0/CC-BY rule still applies to any download.
6. **Weekly twist tuning** — some twists reached much deeper than base in old
   tests; re-check after the balance pass.
7. **APK size** — ~214 MB (debug + ~100 MB music). Fine for sideload; if it ever
   goes near a store, the music must go anyway and the debug build should become
   release.

---

## 6. The PDTD reference — `~/Work/pdtd-reference/` (outside this repo)

The complete decrypted config of **Planet Defense: Space TD** (CyberJoy Games,
`com.cyberjoy.prjw5n2` v0.1.76) — the game Beyond is modelled on. Extracted this
session from the XAPK. **Read `~/Work/pdtd-reference/NUMBERS.md` first.**

Contents: `NUMBERS.md` (digest), `config-json/` (145 data tables as JSON),
`lua-decrypted/`, `tools/` (decrypt→parse→eval pipeline + full writeup in
`README.md`).

Highlights it revealed (validate Beyond's choices against these *ratios*, don't
lift tables or ship their strings — see the IP note in its README):
- **Stage length ≈ 330–360 s** (`battle_const.FORCE_RECOVERY`) — Beyond's 5-min
  hold is on target.
- **Wave format is time-windowed spawns**: `waves:[{beginTime,endTime,
  enemies:[{num,angle:[min,max],radiu,infos}]}]` — continuous timed spawning,
  exactly Beyond's `SurvivalDirector` model.
- **5** upgrade options shown, 10 s to pick (Beyond does 4).
- **Per-level enemy scaling**: `hpMultiplier` ×1 → ×26 (L20) → ×1,132 (L50) →
  ×132k (L100) → ×18M (L300); `attackMultiplier` only ×1 → ×54 → ×513. Big
  takeaway for arc 2/3 curves.
- **11 weapons** with concrete `attackBase` / `atkCDBase` (3–10 s) / range /
  penetration / damage type.
- Planet has a **"Force Shield"** HP layer with its own 303-level upgrade curve.
- Ultimate abilities run at **0.1× time-scale** (bullet-time).
- 208 in-run upgrade cards with a `chainDep`/`combo`/`mutex` dependency graph.

How it was cracked (if you need to re-extract or go deeper): Unity 6 / IL2CPP /
**xLua**. Config = encrypted Lua; cipher RE'd from `libxlua.so` arm64 disasm
(`llvm-objdump` in `~/Android/Sdk/ndk/*/`). Decrypts to **Lua 5.4 bytecode with
remapped opcodes** (stock lua/unluac crash). `tools/luac54.py` parses the
opcode-independent constant pool; `tools/luavm.py` is a mini-VM for the ~10
opcodes the data-literal configs use. Phone `R3CX40CAQ7T` (Galaxy S24, not
rooted) has the game installed if you need a live capture.

---

## 7. Non-negotiables (full list in `../CLAUDE.md`)

1. **Deterministic + speed-independent sim.** Nothing in `src/Sim/` reads
   wall-clock or frame delta. Run `godot --headless --path . scenes/SimTest.tscn
   --quit` (exit 0 = OK) after any sim change.
2. **Balance values live in `data/*.json`, never in code.**
3. **No monetisation surface** — no payment SDK, loot boxes, random rewards,
   premium currency, or FOMO timers, not even stubbed.
4. **Free assets only** — CC0 / CC-BY. Kenney primary. Credit CC-BY in
   `assets/game/CREDITS.txt`. Music is the flagged exception.
5. **Pronoun/attribution**: this session's commit trailer is
   `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>` +
   `Claude-Session: https://claude.ai/code/session_01DjdMzGRRkfZ2EixgmGYkBm`
   (that session id is this conversation's — a new agent uses its own).

---

## 8. Build & release

```bash
dotnet build Sentinel.csproj                              # C# only (fast)
godot --headless --path . --import                        # after adding assets
godot --headless --path . scenes/SimTest.tscn --quit      # determinism + balance table
godot --path . scenes/Main.tscn                           # run the game
```
Dev builds target `Sentinel.csproj` (the `.sln` lacks Godot's ExportDebug config).
Godot: `~/.local/bin/godot` (4.7.2 mono). Editor settings already have the Android
SDK + `~/.android/debug.keystore`.

**Release ritual:** bump `config/version` in `project.godot` **and** `version/code`
+ `version/name` in `export_presets.cfg`, then `git tag -a vX.Y.Z` + push. CI
(`.github/workflows/build-apk.yml`) builds `beyond-debug.apk` and attaches it to
the release. Pushing to `main` without a tag just makes an artifact. `gh` is
authed as `OmniGodgeta`.

Current: **v0.11.0**, `application/config/version = "0.11.0"`, APK ~214 MB.

---

## 9. Watch-list

- APK ~214 MB (debug + music).
- SimTest bot dies ~2 min on m03–m08 (see §4).
- `ConfigDb` hard-loads `arc_01.json` — arc 2 needs a small refactor there +
  `AppRoot` + `StarMapScreen`.
- The legacy wave path (`SpawnSystem`, `SimWorld.BeginWave` non-survival branch,
  `GenerateEndlessWave`) is dead code kept for reference — safe to delete once
  survival is locked in.
- Asset API keys (Sketchfab / Poly Pizza) in
  `~/.config/sentinel/asset-api-keys.env`, unused. Freesound key not created.
- Fuller running history: `~/.claude/projects/-home-shadowswords-Work/memory/`
  (`sentinel-game.md`, `pdtd-reference.md`) on the original dev machine.
