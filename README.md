# Beyond

Radial tower defense for Android — hold the line over Earth. **Zero ads, zero
microtransactions, 100% free** —
a hard architectural rule, not a launch decision (`docs/design-spec.md` §9,
`docs/deviations.md`).

Built with **Godot 4.7.2 (.NET / C#)**. Target: Android arm64, personal sideload.

Download the latest APK from the [Releases](../../releases) page, or grab the
artifact from any green [Actions](../../actions) run.

## Status — greybox alpha (v0.1.0)

Everything is grey shapes. Balance is deliberately untuned. What works:

- **Deterministic fixed-timestep sim** — 1×/2×/3×/4× speed is *more sim ticks per
  frame*, not sped-up animation. Same seed + inputs → identical run (verified in
  `scenes/SimTest.tscn`). ~300 sim-ticks/ms headless.
- **Build / wave loop** — 12 radial turret slots with real firing arcs. A north
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
- **Card draft** between waves — run-scoped modifier cards.
- **Endless mode** — one planet, procedural waves forever, personal best saved.
- **Ascension** — replay the arc at 10 escalating difficulty tiers.
- **Codex** — in-world lore for the setting and the enemies.

Not yet: art, sound, the shop, weekly seeded runs, first-encounter codex gating.

## Layout

```
src/Sim/        deterministic simulation
  SimClock / DetRandom / SimWorld / Systems/*
src/Meta/       SaveGame, ModifierSet, Progression, ResearchDb
src/Config/     JSON -> typed defs (ConfigDb)
src/Render/     SimRenderer — greybox immediate-mode draw
src/UI/         Hud, MenuScreen, ResearchScreen, AbilityScreen, CodexScreen
src/Game/       AppRoot (shell), GameRoot (one mission), SimTest, ShotRunner
data/           balance, hero, turrets, enemies, abilities, cards, research,
                ascension, codex, arc_01, missions/*
tools/          gen_missions.py, gen_research.py
```

## Build

```bash
godot --path . scenes/Main.tscn                        # run
godot --headless scenes/SimTest.tscn --quit            # determinism + balance table
dotnet build Sentinel.csproj
godot --headless --export-debug "Android" build/beyond-debug.apk
```

Requires Godot 4.7.2 **mono**, .NET 9 SDK, Android SDK + JDK (paths in Godot
editor settings). CI (`.github/workflows/build-apk.yml`) does all of this on
every push and attaches the APK to tagged releases.
