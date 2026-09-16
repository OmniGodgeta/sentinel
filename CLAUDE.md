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
4. **Free assets only by default** — CC0 or CC-BY (never NC / "personal use").
   Kenney is the primary source. Credit CC-BY in `assets/game/CREDITS.txt`.
   `assets/music/` is a standing exception (copyrighted, personal build) and is
   flagged for removal before any public release.
   **User exception (2026-09-15):** this app is sideloaded only and will never
   be published, so the user has said copyrighted sounds/animations/art are
   fine to use too, same as the music — no need to ask each time. Still default
   to free/original work; use copyrighted assets when they're a clear
   improvement (e.g. matching Planet Defense TD's actual look/feel more
   closely — see `docs/pdtd-reference` pointer in `docs/ROADMAP.md` §6), and
   note the source in `CREDITS.txt` regardless so a future "make this public"
   pass knows what would need to come out.

## Release ritual

Bump **`config/version`** in `project.godot` **and** `version/code` +
`version/name` in `export_presets.cfg`, then push a `v*` tag. CI
(`.github/workflows/build-apk.yml`) builds the APK and attaches it to the
release. The in-app updater (`src/Meta/UpdateChecker.cs` +
`src/Meta/UpdateDownloader.cs`) compares the running `config/version` against
the latest GitHub release, and on Android downloads the APK in-app and hands
it straight to the system installer (see the Android build note below) —
elsewhere (desktop dev builds) it just downloads and reveals the file.

## Build

```bash
dotnet build Sentinel.csproj                            # C# only
godot --headless --path . --import                      # import assets (first run / new assets)
godot --headless --path . scenes/SimTest.tscn --quit    # determinism + balance table
godot --path . scenes/Main.tscn                         # run the game
```

## Android build: custom Gradle, not plain APK export (since v0.26.0)

`export_presets.cfg`'s Android preset has `gradle_build/use_gradle_build=true`
— required because the in-app updater needs the `REQUEST_INSTALL_PACKAGES`
permission, which plain (non-Gradle) APK export can't add.

`res://android/build/` (Godot's generated Gradle project, ~200MB, almost all
prebuilt `libs/`) is **not committed** — regenerate it with
`bash tools/setup_android_gradle.sh` whenever it's missing (CI runs this
automatically; local exports need it run once, or after deleting `android/build/`).
What IS committed is `android/overlay/` — our own small additions the script
layers on top after unzipping Godot's stock template fresh:
- `AndroidManifest.xml` — adds the `REQUEST_INSTALL_PACKAGES` permission and
  `.BeyondApp` as the `<application android:name>`. Deliberately does **not**
  declare its own `<provider>` — Godot's own `godot-lib` `.aar` already
  registers a FileProvider at `${applicationId}.fileprovider` with a
  files-path covering the whole internal files root, which already covers
  where the updater downloads to (`user://update.apk`). A second `<provider>`
  for the same purpose collided with it in the manifest merge — don't re-add
  one without checking `godot_provider_paths.xml` in the Godot `.aar` first.
- `BeyondApp.kt` — a trivial `Application` subclass that stashes an
  Application Context in a companion object, so plain static Kotlin can reach
  a Context without needing the current Activity.
- `UpdateInstaller.kt` — the actual install trigger: builds a
  `FileProvider.getUriForFile` content URI for the downloaded APK and fires
  `ACTION_VIEW`. Called from C# via `Godot.JavaClassWrapper.Wrap("com.godot.game.UpdateInstaller").Call("install", path)`
  — a plain static-method bridge, not a full Godot Android Plugin (no `.gdap`,
  no separate Gradle module needed for something this small).

Note the Kotlin/Java source package is `com.godot.game` (Godot's own fixed
`namespace` for this template) — **not** `com.shadowswords.beyond` (the real
`applicationId`, resolved separately by Gradle/the manifest at build time).
`JavaClassWrapper.Wrap(...)` needs the real compiled class name, i.e. the
`com.godot.game.*` one.

Verified end-to-end locally this session: `godot --headless --path . --export-debug
Android build/beyond-debug.apk` succeeds, the manifest carries the permission,
and `com.godot.game.{BeyondApp,UpdateInstaller}` are present in the built
`classes*.dex` (checked via `strings`).

## Android build: the debug keystore MUST be committed and stable (v0.26.2 fix)

`android/debug.keystore` is **committed to the repo on purpose** — CI points
`export/android/debug_keystore` at it instead of generating one with `keytool`
on every run. This was the actual root cause of "every update needs an
uninstall + reinstall" (reported even after the v0.26.0/v0.26.1 in-app-updater
work): a freshly-**generated** debug keystore signs each build with a
different, random certificate, and Android refuses to install an app update
whose signing certificate doesn't match what's already on the device — no
matter how well the download/install-intent code works, the OS-level install
itself was always going to fail. `git log -p -- android/debug.keystore .github/workflows/build-apk.yml`
around v0.26.2 has the full before/after. It's a debug key (never used for a
Play Store release — this app is sideload-only) so committing it is the usual
debug-key tradeoff, not a production-signing one: fine for this project, but
don't casually replace/regenerate it once real users have installed builds
signed with it, since that breaks their update chain the same way (announce a
"you'll need to reinstall once" if it ever must change).
**Confirmed working**: exported an APK locally against the committed
keystore and verified its certificate SHA-256 (`apksigner verify --print-certs`)
matches `keytool -list -v -keystore android/debug.keystore`'s fingerprint
exactly. **Not yet verified**: an actual on-device update install (the one
thing left — should now work in-place from v0.26.2 onward, though the
v0.26.2 build itself still needs one final manual reinstall since whatever's
currently on-device was signed with an old CI-random key it can't update
from).

If a local machine's `export/android/java_sdk_path` (in Godot's
`editor_settings-4.7.tres`) points at a JRE-only install (no `javac`), the
Gradle build fails with a Gradle `JAVA_COMPILER` toolchain error — point it at
a real JDK (this box: `jdk17-openjdk`, matching CI's `actions/setup-java`
version). This is a local editor-settings issue, not a project one.
