# Sentinel

Radial tower defense for Android. Zero ads, zero microtransactions, 100% free —
a hard architectural rule, not a launch decision (see `docs/design-spec.md` §9).

Built with **Godot 4.7.2 (.NET / C#)**. Target: Android arm64, personal sideload.

## Status — Phase 1 (greybox)

Per the design spec's build order (§15), this is step 1–2: *one mission, no art,
verify the core loop is fun at 4x before building the five progression systems.*

Working now:
- Deterministic fixed-timestep sim (`SimClock` + `SimWorld`), 1×/2×/3×/4× speed —
  4× runs 4× the sim ticks per frame, not an animation multiplier.
- Verified: two runs from the same seed + input stream are byte-identical
  (`scenes/SimTest.tscn`). Gives replays and reproducible bug reports for free.
- Build phase: place / sell turrets in 12 radial slots (Autocannon, Flak).
- Wave phase: drag the hero battleship, tap to fire the 15s missile volley,
  3 abilities on cooldown (Kinetic Barrage, Aegis Barrier, Overdrive).
- Turrets have real firing arcs — a north turret can't defend the south face.
- 20-wave mission (Skiff + Hauler). Rewards paid per wave cleared, **win or loss**.
- End-of-run damage attribution (turrets / hero / abilities).
- Everything tunable lives in `data/*.json`; no balance value is a code literal.

Not yet: art, sound, the research tree / progression, card draft, the other
6 turrets / 8 enemies / 9 abilities, bosses, offline collectors, the shop.

## Layout

```
src/Sim/        deterministic simulation — no rendering deps
  SimClock.cs        fixed-timestep driver + speed
  DetRandom.cs       PCG PRNG (the only randomness the sim may touch)
  SimWorld.cs        authoritative state + fixed per-tick step order
  Systems/*          spawn, enemy, hero, turret, projectile, ability
src/Config/     JSON → typed defs (ConfigDb)
src/Render/     SimRenderer — greybox immediate-mode draw (MultiMesh pass comes later)
src/UI/         Hud — built in code for now
src/Game/       GameRoot (input→commands), SimTest, ShotRunner
data/           balance, hero, turrets, enemies, abilities, missions/*
```

## Build

```bash
# desktop editor / run
godot --path . scenes/Main.tscn

# determinism + perf check (headless)
godot --headless scenes/SimTest.tscn --quit

# Android debug APK  ->  build/sentinel-debug.apk
dotnet build Sentinel.csproj
godot --headless --export-debug "Android" build/sentinel-debug.apk
```

Requires: Godot 4.7.2 **mono** build, .NET 9 SDK, Android SDK + JDK 21
(paths in Godot editor settings). TFM is `net9.0` to match the export template.
