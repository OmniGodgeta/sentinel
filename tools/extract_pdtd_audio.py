#!/usr/bin/env python3
"""
Extract individual named SFX samples out of Planet Defense: Space TD's FMOD
sound bank, for use as reference/replacement audio in Beyond.

Per the user's explicit direction (2026-09-15): "take the sounds for each
animations and attacks and add them to Beyond replacing the current ones we
have" — and the same-session note that copyrighted assets are fine for this
personal, never-published build (see CLAUDE.md §4). This is NOT redistributed
anywhere — it's a one-time local extraction into this repo's own asset files.

Requires (not installed by default, not in this repo):
  - The game's XAPK: `~/Downloads/Planet+Defense_+Space+TD.xapk`
    (com.cyberjoy.prjw5n2 — see ~/Work/pdtd-reference/README.md for how it was
    obtained). ~543 MB, deliberately not committed here.
  - A Python venv with the `fsb5` package (`pip install fsb5`).

Pipeline:
  1. The XAPK is a zip of 4 APKs; `UnityDataAssetPack.apk` (~415 MB) holds the
     actual game data, including `assets/fmod/{Master,Master.strings,Music,SFX}.bank`.
  2. An FMOD `.bank` file is itself a RIFF container (magic "RIFF", form type
     "FEV "). Its chunks are `FMT `, `LIST` (event/parameter metadata) and
     `SND ` (the audio payload) — walk the RIFF chunk list to find `SND `.
  3. The `SND ` chunk's payload is an 8-byte sub-header followed immediately by
     a raw **FSB5** blob (FMOD Sample Bank v5) running to EOF. Slice from the
     `FSB5` magic to the end of the file.
  4. Feed that FSB5 blob to the `fsb5` PyPI package (HearthSim's parser) — it
     lists every named sample and can rebuild each one as a playable file
     (SFX.bank's samples are Vorbis-in-Ogg, so `rebuild_sample()` returns
     ready-to-use .ogg bytes directly, no transcoding needed for that step).

IMPORTANT caveat learned the hard way: several of the 79 SFX.bank samples are
9–60 seconds long — these are the *raw source library clips* PDTD's FMOD
Studio project trims/loops per-event (e.g. "starship-rail-gun-33371" is 29s).
That trim/loop-region metadata lives in FMOD Studio's event graph, which is a
much deeper reverse-engineering job than the plain RIFF/FSB5 walk above and
was NOT attempted. Only samples that were ALREADY short (a natural one-shot
length, not a source-library length) were used as direct Beyond replacements —
see the SAFE_REPLACEMENTS map below and the "not used" note after it. Don't
blindly pipe every long sample into a rapid-fire slot (turret shots etc.) —
it'll sound like mush from overlapping tails. If you want the longer ones
(better laser/cannon/rocket candidates by name), you'll need to either find
the FMOD event trim points (Master.bank's event data) or just listen and
manually pick + trim an excerpt.

Usage:
  python3 -m venv /tmp/pdtd-venv && /tmp/pdtd-venv/bin/pip install fsb5
  /tmp/pdtd-venv/bin/python tools/extract_pdtd_audio.py --list      # see all 79 names/durations
  /tmp/pdtd-venv/bin/python tools/extract_pdtd_audio.py --apply     # (re)do the SAFE_REPLACEMENTS copy
"""
import argparse
import os
import re
import struct
import subprocess
import sys
import zipfile

XAPK = os.path.expanduser("~/Downloads/Planet+Defense_+Space+TD.xapk")
REPO_AUDIO_DIR = os.path.join(os.path.dirname(__file__), "..", "assets", "audio")

# target Beyond filename -> (sample index, sample name substring) — both the safe,
# already-short (<5s) one-shots pulled in on 2026-09-15, one per distinct game event
# (never a rapid-fire slot), verified via ffprobe duration before use.
SAFE_REPLACEMENTS = {
    "mission_won": (19, "战斗胜利"),       # "combatWin" — a proper end-of-run stinger
    "mission_lost": (16, "Jingle_Lose_00"),
    "card_pick": (27, "升级效果"),          # "upgrade effect" success chime
    "ability_cast": (32, "magical-spell-cast"),
    "explosion": (51, "爆炸效果音效"),      # "boom" explosion
    "explosion_b": (78, "electric-impact"),
    "shield": (74, "能量护盾解除音效"),     # "energy shield release"
    "ui_click": (69, "click"),
    # added 2026-09-15 (2nd pass): missile_launch is the hero's own Missile
    # Barrage volley (~6-15s cooldown depending on level) — infrequent enough
    # that a 1.6s sample is fine; do NOT reuse this length for battery_launch
    # (planet battery fires roughly every second, would overlap into mush).
    "missile_launch": (43, "未来主义榴弹发射器"),   # "futuristic grenade launcher"
    # sentinel_shot backs every orbital weapon except the ones broken out below
    # (cooldowns 0.55s-8s) — already a natural ~2.3s one-shot cannon report,
    # no trim needed.
    "sentinel_shot": (57, "炮击-mcx200705112"),
    # added 2026-09-16: giving PDTD's real "Laser" sentinel its own distinct
    # sound instead of the generic sentinel_shot cannon report. Its min cooldown
    # is 1.15s, comfortably above this clip's 1.95s length isn't quite true —
    # it CAN outrun the clip at high level, same overlap tradeoff already
    # accepted for sentinel_shot's reuse across weapons; the previous "borderline"
    # note above was about reusing it as the universal one-shot, not as one
    # weapon's own distinct sound, so it's fine here.
    "orbital_laser_fire": (34, "laser-fire"),
    # space_bomb and waterdrop were both silently broken (see docs/ROADMAP.md — a
    # platform-relative range-capped target search meant they often did nothing at
    # all) and got fixed + given their own distinct sound instead of sentinel_shot.
    "space_bomb_fire": (70, "炮击-mcx200705113"),   # a heavier alternate cannon-thud take
    "waterdrop_fire": (23, "咻的一声"),             # a quick "whoosh" bolt-launch
    "ball_lightning_fire": (10, "electric-shock"),  # trimmed below — 9.7s raw library clip
    "radiation_line_fire": (11, "radiation-213840"),  # trimmed below — 3.55s raw library clip
    # "same for all sentinels" (2026-09-16) — giving the last two orbital weapons that
    # were still on the generic sentinel_shot catch-all their own distinct sound too.
    # radiation_zone deliberately reuses radiation_line_fire rather than mining a less
    # fitting sample — both are radiation-type weapons, sharing the sound is thematically
    # correct, not a shortcut.
    "orbital_lightning_fire": (62, "thunder-sound-375727"),  # trimmed below — 60s raw library clip
    "beam_fire": (47, "太空武器激光枪激光射击"),  # trimmed below — 3.6s raw library clip
    # force_field intentionally has no separate extraction — it reuses the existing
    # "shield" key (already pulled from sample 74) since Force Field is thematically a
    # defensive field effect, same reasoning as radiation_zone sharing radiation_line's.
}

# explosion / explosion_b fire on EVERY regular enemy kill (AudioManager minGap
# only 0.03s) — the raw samples are 3.1-3.6s, which turns into an overlapping
# mush during a busy hold. Trim to a punchy one-shot (keep the transient,
# fade the long tail) instead of the full library-clip length. (start, dur,
# fade-out start, fade-out dur, extra dB trim) — all in seconds except dB.
TRIM = {
    "explosion": (0.33, 1.30, 1.05, 0.22, -1.5),
    "explosion_b": (0.0, 1.35, 1.10, 0.22, -1.5),
    # 未来主义榴弹发射器 is a 13.6s library clip — the launch transient is at the
    # very start, same heuristic as the explosion trims (no way to confirm the
    # exact onset without listening; a 1.6s prefix + short fade-out is a safe bet).
    "missile_launch": (0.0, 1.6, 1.3, 0.3, 0.0),
    # electric-shock-97989 is a 9.7s library clip — same "transient's at the start"
    # heuristic, trimmed to a punchy ~1.2s crackle for shock_orb's min ~3.2s cooldown.
    "ball_lightning_fire": (0.0, 1.2, 0.9, 0.3, 0.0),
    # radiation-213840 is a 3.55s library clip — trimmed to a ~1.4s activation hum.
    "radiation_line_fire": (0.0, 1.4, 1.1, 0.3, 0.0),
    # thunder-sound-375727 is a 60s raw ambience recording — the first crack is right at
    # the start, trimmed to a ~1s punchy zap instead of the long rolling-thunder tail.
    "orbital_lightning_fire": (0.0, 1.0, 0.75, 0.25, 0.0),
    # 太空武器激光枪激光射击 is a 3.6s library clip — trimmed to a ~1.5s laser-fire burst.
    "beam_fire": (0.0, 1.5, 1.2, 0.3, 0.0),
}
# Considered but NOT used (too long to safely reuse without FMOD event trim
# metadata — see the module docstring): laser-fire (34, 1.9s — borderline, left
# alone since Beyond's synthesized turret/sentinel lasers are correctly shaped
# for rapid re-fire and this isn't obviously better), SF-DS Energy Laser (36,
# 22s), starship-rail-gun (42, 29s), 太空武器激光枪 (47, 3.6s), rocket launch
# 火箭发射升空 (9, 44.7s — a bigger, longer rocket-launch alternative to 43 if
# you want to try it instead), the other two 炮击 cannon hits (58/70, 2.2-2.8s —
# alternate takes if 57 isn't quite right for sentinel_shot).
#
# Enemy/boss/sentinel *visual* replacement (user asked, 2026-09-15 2nd pass):
# investigated and NOT done, on purpose. PDTD's enemies/bosses/sentinels are
# real 3D meshes with UV-mapped materials (see the .png.ab paths under
# assets/Res/materials/enemy/**  — normal maps, shader inputs, boss diffuse
# textures baked for a specific 3D mesh's UVs) rendered in Unity's 3D pipeline;
# Beyond's SimRenderer draws flat 2D sprites (`Art.Enemy(id)` in
# src/Render/SimRenderer.cs DrawEnemies). Dropping a mesh's UV-mapped diffuse
# texture onto a flat sprite quad produces visible seams/stretching — it would
# look broken, not better. The missile texture swap below WORKED specifically
# because muzzle/trail effects are flat particle billboards even in a 3D game
# (assets/Res/gameobjects/weapon/missile/texture/supermissile01.png.ab) — that
# trick doesn't generalize to full character/ship art. A real version of this
# would need to either render each 3D model to a sprite from Beyond's camera
# angle (needs a 3D tool — Blender heedless render or similar, not attempted)
# or hand-pick more flat VFX/accent textures (engine glow, energy auras) to
# layer onto Beyond's *existing* sprites as an accent pass instead of a full
# swap. Left for a dedicated follow-up if the user wants to pursue it further.
#
# The missile *sprite* extraction lives in the venv-created
# `pdtd-extract/extract.py`-style workflow, not this file (this file is audio-
# only) — see docs/ROADMAP.md §6a for the exact steps used
# (UnityPy.config.FALLBACK_UNITY_VERSION = "6000.0.80f1", load
# supermissile01.png.ab, export the Texture2D, then a small PIL pass:
# luminance-as-alpha since the source has a black backing, rotate so the nose
# points "up" to match this renderer's sprite convention, downscale, save over
# assets/game/missile.png).


def find_fsb5_blob(bank_path: str) -> bytes:
    data = open(bank_path, "rb").read()
    assert data[:4] == b"RIFF", f"{bank_path} is not a RIFF/FMOD bank"
    pos = 12  # skip "RIFF" + size(4) + form type(4, e.g. "FEV ")
    snd_chunk = None
    while pos < len(data) - 8:
        cid = data[pos:pos + 4]
        (csize,) = struct.unpack("<I", data[pos + 4:pos + 8])
        if cid == b"SND ":
            snd_chunk = data[pos + 8:pos + 8 + csize]
            break
        pos += 8 + csize + (csize % 2)
    assert snd_chunk is not None, "no SND chunk found — bank layout changed?"
    fsb_start = snd_chunk.find(b"FSB5")
    assert fsb_start >= 0, "no FSB5 magic inside the SND chunk"
    return snd_chunk[fsb_start:]


def extract_apk_member(xapk_path: str, inner_apk: str, member: str, out_path: str):
    with zipfile.ZipFile(xapk_path) as outer:
        with outer.open(inner_apk) as inner_bytes_stream:
            # UnityDataAssetPack.apk is itself a zip; read it fully into memory
            # (it's ~415MB, fine for a one-off script) then pull the one member.
            import io
            inner_zip = zipfile.ZipFile(io.BytesIO(inner_bytes_stream.read()))
            with inner_zip.open(member) as f, open(out_path, "wb") as out:
                out.write(f.read())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--list", action="store_true", help="list every sample name + duration, then exit")
    ap.add_argument("--apply", action="store_true", help="extract + normalize the SAFE_REPLACEMENTS into assets/audio/")
    ap.add_argument("--workdir", default="/tmp/pdtd_audio_extract")
    args = ap.parse_args()

    try:
        import fsb5
    except ImportError:
        sys.exit("pip install fsb5 first (see the module docstring for the venv setup)")

    os.makedirs(args.workdir, exist_ok=True)
    bank_path = os.path.join(args.workdir, "SFX.bank")
    if not os.path.exists(bank_path):
        if not os.path.exists(XAPK):
            sys.exit(f"XAPK not found at {XAPK} — nothing to extract from")
        extract_apk_member(XAPK, "UnityDataAssetPack.apk", "assets/fmod/SFX.bank", bank_path)

    fsb_bytes = find_fsb5_blob(bank_path)
    ex = fsb5.FSB5(fsb_bytes)
    ext = ex.get_sample_extension()

    if args.list:
        for i, s in enumerate(ex.samples):
            print(f"{i:2d}  {s.samples / s.frequency:6.2f}s  {s.name}")
        return

    if args.apply:
        for target, (idx, needle) in SAFE_REPLACEMENTS.items():
            s = ex.samples[idx]
            assert needle in s.name, f"sample {idx} is now {s.name!r}, expected to contain {needle!r} — bank changed?"
            raw = ex.rebuild_sample(s)
            raw_path = os.path.join(args.workdir, f"{target}_raw.{ext}")
            open(raw_path, "wb").write(raw)
            out_path = os.path.join(REPO_AUDIO_DIR, f"{target}.ogg")
            af = "loudnorm=I=-18:TP=-1.5:LRA=11,aformat=channel_layouts=mono"
            extra_args = []
            if target in TRIM:
                start, dur, fade_st, fade_d, db = TRIM[target]
                extra_args = ["-ss", str(start), "-t", str(dur)]
                af += f",afade=t=out:st={fade_st}:d={fade_d},volume={db}dB"
            subprocess.run([
                "ffmpeg", "-y", "-v", "error", "-i", raw_path, *extra_args,
                "-af", af,
                "-ar", "44100", "-c:a", "libvorbis", "-q:a", "4", out_path,
            ], check=True)
            print(f"wrote {out_path}  (from sample {idx} {s.name!r})")
        return

    ap.print_help()


if __name__ == "__main__":
    main()
