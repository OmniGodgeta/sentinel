# Beyond

Radial tower defense for Android — hold the line over Earth. **Zero ads, zero
microtransactions, 100% free** —
a hard architectural rule, not a launch decision (`docs/design-spec.md` §9,
`docs/deviations.md`).

Built with **Godot 4.7.2 (.NET / C#)**. Target: Android arm64, personal sideload.

Download the latest APK from the [Releases](../../releases) page, or grab the
artifact from any green [Actions](../../actions) run.

## Status — v0.11.0

Playable end to end. **Balance is untuned** — that pass is the current blocker
(see [`docs/ROADMAP.md`](docs/ROADMAP.md), the canonical "what's done / what's
next / what's deferred"). Agents: read [`CLAUDE.md`](CLAUDE.md) first.

- **Deterministic fixed-timestep sim** — 1×/2×/3×/4× speed is *more sim ticks per
  frame*, not sped-up animation. Same seed + inputs → identical run, and 1× vs 4×
  are byte-identical (both verified in `scenes/SimTest.tscn`). ~90 sim-ticks/ms
  headless.
- **Survival loop** — every mission is a 5-minute hold with a continuous,
  escalating spawn director (no discrete waves). Real-time base management,
  per-minute upgrade drafts. Endless / Weekly are open-ended.
- **Build** — 12 radial turret slots with real firing arcs. A north
  turret cannot defend the south face.
- **8 turrets** (Autocannon, Flak, Railgun, Tesla, Missile Silo, Laser Lattice,
  Graviton, Nanite Forge), each with in-run L1→L3 upgrades and an A/B fork at L3.
- **10 enemies** with signature behaviours (evasion, regenerating shields,
  standoff shelling, a Skiff-spawning Carrier, blink, turret-leech, heal/shield
  aura, CC immunity) + a boss (The Threshing Gate — periodic invulnerable shell).
- **12 Sentinel abilities**, tap-to-cast, queue to the next tick (safe at 4×).
- **Hero battleship** — drag to reposition, 15s manual missile volley, auto
  point-defence.
- **Campaign** — 8-mission arc, each debuts one enemy.
- **Progression** — research tree (5 branches × 6 tiers), Commander level, hero
  level 1–20 (ability slots at L8 / L20), ability leveling via Sentinel Cores.
- **Card draft** during the hold — run-scoped modifier cards, 4 options; the sim
  freezes for the pick.
- **Loadout presets** — 3 saved ability loadouts (Protocols screen).
- **Endless mode** — one planet, no timer, best *time survived* saved.
- **Ascension** — replay the arc at 10 escalating difficulty tiers.
- **Weekly Challenge** — the shared endless seed under one of 8 rotating twists.
- **Shop** — Commendations (earned from stars / Ascension / weekly / Codex) buy
  cosmetics only: hero hulls, planet skins, ordnance colours.
- **Codex** — in-world lore, first-encounter gated.
- **Art / sound** — Kenney CC0 sprites + audio, a shader Earth, NASA/ESA space
  backdrops per level. Music is copyrighted and personal-build-only.

Not yet (see [`docs/ROADMAP.md`](docs/ROADMAP.md)): campaign arcs 2–6, the
Command Deck / Field Supplies / Archive shop tabs, hold-to-inspect, cloud save,
an AI sprite pipeline.

## Layout

```
src/Sim/        deterministic simulation
  SimClock / DetRandom / SimWorld / Systems/*  (Systems/SurvivalDirector = the spawn loop)
src/Meta/       SaveGame, ModifierSet, Progression, ResearchDb, Shop, WeeklyChallenge, UpdateChecker
src/Config/     JSON -> typed defs (ConfigDb, Defs)
src/Render/     SimRenderer, PlanetView, Starfield, Art, ScreenFx
src/UI/         Hud, MenuScreen + MenuBackground + GlowButton, StarMapScreen,
                ResearchScreen, AbilityScreen, ShopScreen, CodexScreen,
                SettingsScreen, LevelUpScreen, UiTheme
src/Game/       AppRoot (shell), GameRoot (one mission), SimTest, ShotRunner
data/           balance, survival, hero, turrets, enemies, abilities, cards,
                levelcards, research, ascension, shop, codex, arc_01, missions/*
assets/game/bg/ per-level space backdrops (NASA/ESA + EHT)
tools/          gen_missions.py, gen_research.py
```

## Build

```bash
dotnet build Sentinel.csproj                            # C# only
godot --headless --path . --import                      # import assets (first run / new assets)
godot --path . scenes/Main.tscn                         # run
godot --headless --path . scenes/SimTest.tscn --quit    # determinism (+ 1x/4x) + balance table
godot --headless --path . --export-debug "Android" build/beyond-debug.apk
```

Release: bump `config/version` in `project.godot` **and** `version/code` +
`version/name` in `export_presets.cfg`, then push a `v*` tag.

Requires Godot 4.7.2 **mono**, .NET 9 SDK, Android SDK + JDK (paths in Godot
editor settings). CI (`.github/workflows/build-apk.yml`) does all of this on
every push and attaches the APK to tagged releases.
