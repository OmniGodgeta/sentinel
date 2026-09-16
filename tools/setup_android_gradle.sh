#!/usr/bin/env bash
# Sets up res://android/build for a custom Gradle Android export — needed for the
# in-app updater (UpdateInstaller.kt hands a downloaded APK straight to the system
# installer via a FileProvider, which needs a <provider> manifest entry that plain
# APK export can't add). android/build/ itself is NOT committed (it's ~200MB, almost
# entirely Godot's own prebuilt libs/ — regenerable, same idea as export_templates).
# What IS committed is android/overlay/ (our own small additions) — this script
# unzips Godot's stock template fresh, then layers the overlay on top. Run this
# before any Android export/build, locally or in CI, whenever android/build/ is
# missing or was deleted.
set -e
cd "$(dirname "$0")/.."

GODOT_VERSION="4.7.2.stable.mono"
TEMPLATE_DIR="$HOME/.local/share/godot/export_templates/$GODOT_VERSION"
SOURCE_ZIP="$TEMPLATE_DIR/android_source.zip"

if [ -d android/build ] && [ -f android/build/build.gradle ]; then
  echo "android/build already present — skipping unzip (delete it first to force a clean re-unzip)"
else
  if [ ! -f "$SOURCE_ZIP" ]; then
    echo "error: $SOURCE_ZIP not found — install Godot's export templates first" >&2
    exit 1
  fi
  mkdir -p android/build
  unzip -q -o "$SOURCE_ZIP" -d android/build
  # tells the Godot editor's own resource filesystem to ignore this tree (the
  # interactive "Install Android Build Template" installer adds this too; a
  # plain unzip doesn't) — harmless for CI, keeps the editor's FileSystem dock
  # sane for local dev
  touch android/build/.gdignore
  echo "unzipped $SOURCE_ZIP -> android/build"
fi

# ---- layer our overlay on top ----
# (no custom <provider>/file_paths.xml needed — UpdateInstaller.kt reuses the
# FileProvider Godot's own godot-lib .aar already declares, see
# android/overlay/AndroidManifest.xml's comment)
cp android/overlay/AndroidManifest.xml android/build/src/main/AndroidManifest.xml
mkdir -p android/build/src/main/java/com/godot/game
cp android/overlay/BeyondApp.kt android/build/src/main/java/com/godot/game/BeyondApp.kt
cp android/overlay/UpdateInstaller.kt android/build/src/main/java/com/godot/game/UpdateInstaller.kt

echo "android/build ready (overlay applied)"
