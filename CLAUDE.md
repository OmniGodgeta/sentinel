# Beyond — agent guide

Radial tower-defense for Android (Godot 4.7.2 **mono** / C#, `net9.0`). Personal
sideload, **100% free — no ads, no IAP, no premium-currency field, ever** (hard
architectural rule; see `docs/design-spec.md` §9).

The working dir / repo is `sentinel` and the C# assembly + namespace stay
`Sentinel`; the *product* is "Beyond" (visible strings, `com.shadowswords.beyond`,
`beyond-debug.apk` only).

## Start here

- **`docs/ROADMAP.md`** — current status, what's next, what's deferred, and why.
  Read it before planning any feature work.
- `docs/design-spec.md` — the full design. `docs/deviations.md` — where the build
  intentionally differs from the spec.
- The game is **survival**, not waves: every mission is a timed hold with a
  continuous escalating spawn director. See `src/Sim/Systems/SurvivalDirector.cs`.

## Non-negotiables

1. **The sim is deterministic and speed-independent.** Fixed 60 Hz ticks; 1×–4×
   is *more ticks per frame*, never faster animation. Nothing in `src/Sim/` may
   read wall-clock or frame delta. `scenes/SimTest.tscn` verifies same-seed
   determinism *and* 1×-vs-4× identity — run it after any sim change:
   `godot --headless --path . scenes/SimTest.tscn --quit` (exit 0 = OK).
2. **Balance values live in `data/*.json`, never in code.** If a tuning change
   needs a code edit, that's a bug — add the knob to config instead.
   `data/survival.json` holds every spawn/difficulty constant.
3. **No monetisation surface.** No payment SDK, no loot boxes, no random rewards,
   no premium currency, no FOMO timers — not even stubbed.
4. **Free assets only** — CC0 or CC-BY (never NC / "personal use"). Kenney is the
   primary source. Credit CC-BY in `assets/game/CREDITS.txt`. `assets/music/` is
   the one exception (copyrighted, personal build) and is flagged for removal
   before any public release.

## Release ritual

Bump **`config/version`** in `project.godot` **and** `version/code` +
`version/name` in `export_presets.cfg`, then push a `v*` tag. CI
(`.github/workflows/build-apk.yml`) builds the APK and attaches it to the
release. The in-app updater (`src/Meta/UpdateChecker.cs`) compares the running
`config/version` against the latest GitHub release.

## Build

```bash
dotnet build Sentinel.csproj                            # C# only
godot --headless --path . --import                      # import assets (first run / new assets)
godot --headless --path . scenes/SimTest.tscn --quit    # determinism + balance table
godot --path . scenes/Main.tscn                         # run the game
```
