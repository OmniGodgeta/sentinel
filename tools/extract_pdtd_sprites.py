#!/usr/bin/env python3
"""Pull UI sprites out of Planet Defense TD's Unity asset bundles into assets/game/pdtd/.

Companion to extract_pdtd_audio.py (audio) — this one is images only. Same source
(~/Downloads/Planet+Defense_+Space+TD.xapk -> UnityDataAssetPack.apk) and the same
standing CLAUDE.md §4 user exception: sideload-only personal build, copyrighted PDTD
assets are fine, credit them in assets/game/CREDITS.txt regardless.

    python tools/extract_pdtd_sprites.py --list cardui      # see what matches a group
    python tools/extract_pdtd_sprites.py --apply            # write every group out

Needs UnityPy + Pillow (see extract_pdtd_audio.py's docstring for the venv setup).

THE BLACK-BACKING GOTCHA. A good number of PDTD's effect/frame textures ship with an
opaque black background instead of an alpha channel — they're composited additively in
Unity, so "black" *is* "transparent" there. Blitting one straight into this renderer
gives a solid black rectangle (Force Field shipped like that at v0.28, and missile.png
before it at v0.25.1). Any group below with `lum_alpha=True` gets luminance copied into
the alpha channel to undo that. If a newly-added sprite renders as a black box, that's
the flag it needs.
"""

import argparse
import io
import os
import zipfile

XAPK = os.path.expanduser("~/Downloads/Planet+Defense_+Space+TD.xapk")
INNER = "UnityDataAssetPack.apk"
REPO = os.path.join(os.path.dirname(__file__), "..")
OUT_ROOT = os.path.join(REPO, "assets", "game", "pdtd")

# group -> (subdir, [(bundle-path-suffix, output-name)], lum_alpha, max_size)
#
# "cardui" is PDTD's in-run skill-card chrome: the frame art the draft cards are built
# from (a back plate, a per-rarity front frame, an outline, a glow) plus the 1-5 star
# pips. "skillicon" is the per-weapon glyph that sits in the middle of one of those
# cards. "rarity" is the item-quality plate behind a dropped item. Together these are
# what "use PDTD's exact upgrade card images" needs.
GROUPS = {
    "cardui": ("cardui", [
        ("ui/sprites/skill/skillcard/slot_background.png.ab", "card_back"),
        ("ui/sprites/skill/skillcard/slot_front_normal.png.ab", "card_front_normal"),
        ("ui/sprites/skill/skillcard/slot_front_super.png.ab", "card_front_super"),
        ("ui/sprites/skill/skillcard/slot_front_relic.png.ab", "card_front_relic"),
        ("ui/sprites/skill/skillcard/slot_front_ultimate.png.ab", "card_front_ultimate"),
        ("ui/sprites/skill/skillcard/slot_outline_normal.png.ab", "card_outline_normal"),
        ("ui/sprites/skill/skillcard/slot_outline_super.png.ab", "card_outline_super"),
        ("ui/sprites/skill/skillcard/slot_outline_relic.png.ab", "card_outline_relic"),
        ("ui/sprites/skill/skillcard/slot_outline_ultimate.png.ab", "card_outline_ultimate"),
        ("ui/sprites/skill/skillcard/slot_light_normal.png.ab", "card_light_normal"),
        ("ui/sprites/skill/skillcard/slot_light_super.png.ab", "card_light_super"),
        ("ui/sprites/skill/skillcard/slot_selected.png.ab", "card_selected"),
        ("ui/sprites/skill/skillcard/icon_star_normal_1.png.ab", "star_1"),
        ("ui/sprites/skill/skillcard/icon_star_normal_2.png.ab", "star_2"),
        ("ui/sprites/skill/skillcard/icon_star_normal_3.png.ab", "star_3"),
        ("ui/sprites/skill/skillcard/icon_star_normal_4.png.ab", "star_4"),
        ("ui/sprites/skill/skillcard/icon_star_normal_5.png.ab", "star_5"),
    ], False, 512),

    # The glyph in the middle of a card. PDTD only ships one per weapon *family*, so
    # several Beyond weapons share one (beam/laser both map off focusedbeam/laser).
    # These seven export fully opaque, which LOOKS like the black-backing case in the
    # module docstring but isn't: they're full-bleed card illustrations (a launcher over
    # a planet, a satellite, a railgun barrel), meant to fill a card's art window and be
    # clipped by the frame around it — opaque is correct. Running luminance-into-alpha on
    # them ate the dark half of each picture. The docstring's fix is for *additive VFX*
    # textures only. The damage-type and attribute icons in the next group are true
    # cut-out glyphs and already carry real alpha, so neither group wants lum_alpha.
    "skillicon": ("skillicon", [
        ("ui/sprites/skill/missile.png.ab", "missile"),
        ("ui/sprites/skill/droplet.png.ab", "waterdrop"),
        ("ui/sprites/skill/laser.png.ab", "laser"),
        ("ui/sprites/skill/focusedbeam.png.ab", "beam"),
        ("ui/sprites/skill/radiationline.png.ab", "radiation_line"),
        ("ui/sprites/skill/railgun.png.ab", "railgun"),
        ("ui/sprites/skill/dimensionalgrenade.png.ab", "space_bomb"),
    ], False, 320),

    "skillattr": ("skillicon", [
        ("ui/sprites/skill/damagetype/energy.png.ab", "dmg_energy"),
        ("ui/sprites/skill/damagetype/physical.png.ab", "dmg_physical"),
        ("ui/sprites/skill/damagetype/radiation.png.ab", "dmg_radiation"),
        ("ui/sprites/skill/attribute/icon_skill_dmgbonus.png.ab", "attr_damage"),
        ("ui/sprites/skill/attribute/icon_skill_critrate.png.ab", "attr_crit_rate"),
        ("ui/sprites/skill/attribute/icon_skill_critbonus.png.ab", "attr_crit_damage"),
        ("ui/sprites/skill/attribute/icon_skill_explosiondmgbonus.png.ab", "attr_explosion"),
        ("ui/sprites/skill/attribute/icon_skill_impactdmgbonus.png.ab", "attr_impact"),
    ], False, 256),

    # Per-weapon "tech point" emblems — richer than the flat skill glyphs and one for
    # every sentinel in the roster, which makes them the right watermark for the
    # Research/Codex list rows as well as a card backdrop.
    "techpoint": ("techpoint", [
        (f"ui/sprites/item/techpoint_{n}.png.ab", n) for n in (
            "missile", "waterdrop", "railgun", "laser", "beam", "radiationlink",
            "radiationzone", "spacebomb", "gravitynova", "chainlightning",
            "balllightning", "random",
        )
    ], False, 256),

    # Item-quality plates, used behind an in-run item drop and in the loot popup.
    "rarity": ("rarity", [
        ("ui/sprites/item/itembg_common.png.ab", "common"),
        ("ui/sprites/item/itembg_fine.png.ab", "fine"),
        ("ui/sprites/item/itembg_rare.png.ab", "rare"),
        ("ui/sprites/item/itembg_epic.png.ab", "epic"),
        ("ui/sprites/item/itembg_legendary.png.ab", "legendary"),
        ("ui/sprites/item/itembg_supreme.png.ab", "supreme"),
        ("ui/sprites/item/itembg_ultimate.png.ab", "ultimate"),
        ("ui/sprites/item/item_outline.png.ab", "outline"),
        ("ui/sprites/item/item_star.png.ab", "star"),
        ("ui/sprites/item/itemslot.png.ab", "slot"),
    ], False, 256),

    # Enemy hull art. These are flat pre-rendered sprites (SpriteRenderer only, no mesh
    # anywhere in the Unity data) which is why they drop straight into this renderer — see
    # CREDITS.txt for the full per-enemy prefab mapping from the v0.25.2 pass. Output goes
    # to assets/game/enemies/ rather than pdtd/, matching what Art.Enemy() loads.
    "enemyart": ("../enemies", [
        ("textures/battleship/wind/wind_cruiser_boss.png.ab", "miniboss_siege_warden"),
    ], False, 320),

    # Loot/currency icons for the drop popups and the Armory.
    "loot": ("loot", [
        ("ui/sprites/item/silver_key.png.ab", "silver_key"),
        ("ui/sprites/item/golden_key.png.ab", "gold_key"),
        ("ui/sprites/item/chest.png.ab", "chest"),
        ("ui/sprites/item/coin.png.ab", "coin"),
        ("ui/sprites/item/exp.png.ab", "exp"),
        ("ui/sprites/item/researchcore.png.ab", "research_core"),
        ("ui/sprites/item/techpoint.png.ab", "techpoint"),
        ("ui/sprites/item/shield_capacitor.png.ab", "shield_capacitor"),
        ("ui/sprites/item/shield_integrator.png.ab", "shield_integrator"),
        ("ui/sprites/item/rebuildcore.png.ab", "rebuild_core"),
        ("ui/sprites/item/crystal_reforge.png.ab", "crystal"),
        ("ui/sprites/item/medal_icon.png.ab", "medal"),
    ], False, 256),

    # The Armory's chests. PDTD's shop sells one box per equipment family
    # (sentinel / planet / mothership / force shield) plus a generic chest, and
    # dresses the row with the two "chest slot" plates from the slot machine UI.
    "chestart": ("chest", [
        ("ui/sprites/item/box_sentinel.png.ab", "box_sentinel"),
        ("ui/sprites/item/box_planet.png.ab", "box_planet"),
        ("ui/sprites/item/box_mothership.png.ab", "box_mothership"),
        ("ui/sprites/item/box_shield.png.ab", "box_shield"),
        ("ui/sprites/slot/gray_chests.png.ab", "chest_silver"),
        ("ui/sprites/slot/purple_chests.png.ab", "chest_gold"),
        ("ui/sprites/shopping/bg_box_normal.png.ab", "box_bg_normal"),
        ("ui/sprites/shopping/bg_box_super.png.ab", "box_bg_super"),
    ], False, 512),

    # Chip screen chrome: the rarity-tinted chip plate, the equipped-slot plate,
    # the empty slot, and the 27 chip glyphs PDTD picks from.
    "chipui": ("chip", [
        ("ui/sprites/chip/chipicon_common.png.ab", "plate_common"),
        ("ui/sprites/chip/chipicon_fine.png.ab", "plate_fine"),
        ("ui/sprites/chip/chipicon_rare.png.ab", "plate_rare"),
        ("ui/sprites/chip/chipicon_epic.png.ab", "plate_epic"),
        ("ui/sprites/chip/chipicon_legendary.png.ab", "plate_legendary"),
        ("ui/sprites/chip/chipicon_supreme.png.ab", "plate_supreme"),
        ("ui/sprites/chip/chipicon_ultimate.png.ab", "plate_ultimate"),
        ("ui/sprites/chip/chiponslot_empty.png.ab", "slot_empty"),
        ("ui/sprites/chip/outline_slot.png.ab", "slot_outline"),
        ("ui/sprites/chip/quality_slot.png.ab", "slot_quality"),
        ("ui/sprites/chip/panel_slot.png.ab", "panel"),
    ] + [
        (f"ui/sprites/chip/chip_affix/icon_chip_{i}.png.ab", f"glyph_{i}") for i in range(1, 28)
    ], False, 256),

    # Module screen chrome. PDTD's Module page is six equipment families with a
    # glyph each, laid over a slot plate — Beyond's data/modules.json rows map
    # onto the same six, so these are the icons that page should be using.
    "moduleui": ("module", [
        ("ui/sprites/slot/icon_weapon.png.ab", "icon_weapon"),
        ("ui/sprites/slot/icon_shield.png.ab", "icon_shield"),
        ("ui/sprites/slot/icon_engine.png.ab", "icon_engine"),
        ("ui/sprites/slot/icon_reactor.png.ab", "icon_reactor"),
        ("ui/sprites/slot/icon_radar.png.ab", "icon_radar"),
        ("ui/sprites/slot/icon_quantacore.png.ab", "icon_quantacore"),
        ("ui/sprites/slot/icon_all.png.ab", "icon_all"),
        ("ui/sprites/slot/weapon.png.ab", "art_weapon"),
        ("ui/sprites/slot/shield.png.ab", "art_shield"),
        ("ui/sprites/slot/engine.png.ab", "art_engine"),
        ("ui/sprites/slot/reactor.png.ab", "art_reactor"),
        ("ui/sprites/slot/radar.png.ab", "art_radar"),
        ("ui/sprites/slot/quantacore.png.ab", "art_quantacore"),
        ("ui/sprites/item/designslot.png.ab", "slot"),
        ("ui/sprites/tech/techslot_back.png.ab", "tech_back"),
        ("ui/sprites/tech/techslot_front.png.ab", "tech_front"),
        ("ui/sprites/tech/techslot_outline.png.ab", "tech_outline"),
    ], False, 384),

    # Per-weapon "alloy" cartridges — a clean, uniform glyph for all eleven
    # sentinels (the skillicon group only covers seven), which is what the HUD
    # and the Codex want where a full tile would be too big.
    # Field/zone VFX. These are additive particle textures — black IS transparent for
    # them in Unity, so they need lum_alpha (see the module docstring's gotcha).
    "fieldfx": ("vfx", [
        ("gameobjects/vfx/forcefield/circle.png.ab", "ff_circle"),
        ("gameobjects/vfx/forcefield/circle02.png.ab", "ff_circle2"),
        ("gameobjects/vfx/sustainedrelease/textures/circle102.png.ab", "zone_circle"),
        ("gameobjects/vfx/sustainedrelease/textures/circlerainbow11.png.ab", "zone_rings"),
        ("gameobjects/vfx/sustainedrelease/textures/glow2.png.ab", "zone_glow"),
        ("gameobjects/vfx/sustainedrelease/textures/flash31.png.ab", "zone_flash"),
        ("gameobjects/vfx/glowingorb/textures/circle116.png.ab", "orb_core"),
        ("gameobjects/vfx/glowingorb/textures/flare20.png.ab", "orb_flare"),
        ("gameobjects/vfx/glowingorb/textures/noise34.png.ab", "orb_noise"),
        ("gameobjects/vfx/gravitynova/texture/masks/radialmask_02.png.ab", "nova_mask"),
        ("gameobjects/vfx/gravitynova/texture/flares/flare04.png.ab", "nova_flare"),
    ], True, 512),

    "alloy": ("alloy", [
        (f"ui/sprites/ultimateupgradeui/ultimate_alloy_{i}.png.ab", n) for i, n in (
            (10001, "missile"), (10002, "waterdrop"), (10003, "railgun"),
            (10004, "laser"), (10005, "beam_laser"), (10006, "rad_line"),
            (10007, "rad_zone"), (10008, "space_bomb"), (10009, "force_field"),
            (10010, "lightning"), (10011, "shock_orb"),
        )
    ], False, 256),
}


def open_inner():
    with zipfile.ZipFile(XAPK) as outer, outer.open(INNER) as f:
        return zipfile.ZipFile(io.BytesIO(f.read()))


def load_texture(zf, member):
    import UnityPy
    UnityPy.config.FALLBACK_UNITY_VERSION = "6000.0.80f1"
    env = UnityPy.load(zf.read(member))
    best = None
    for obj in env.objects:
        if obj.type.name != "Texture2D":
            continue
        img = obj.read().image
        if best is None or img.width * img.height > best.width * best.height:
            best = img
    return best


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--list", metavar="GROUP", help="print what a group would write, then exit")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--groups", default="", help="comma-separated subset (default: all)")
    args = ap.parse_args()

    if args.list:
        for suffix, name in GROUPS[args.list][1]:
            print(f"  {name:22s} <- {suffix}")
        return
    if not args.apply:
        ap.print_help()
        return

    from PIL import Image

    zf = open_inner()
    index = {n.split("assets/Res/", 1)[-1]: n for n in zf.namelist()}
    want = [g.strip() for g in args.groups.split(",") if g.strip()] or list(GROUPS)

    for group in want:
        subdir, entries, lum_alpha, max_size = GROUPS[group]
        out_dir = os.path.join(OUT_ROOT, subdir)
        os.makedirs(out_dir, exist_ok=True)
        for suffix, name in entries:
            member = index.get(suffix)
            if member is None:
                print(f"  !! {group}/{name}: no bundle at {suffix}")
                continue
            img = load_texture(zf, member)
            if img is None:
                print(f"  !! {group}/{name}: no Texture2D in {suffix}")
                continue
            img = img.convert("RGBA")
            if lum_alpha:
                r, g, b, _ = img.split()
                lum = Image.merge("RGB", (r, g, b)).convert("L")
                img.putalpha(lum)
            if max(img.size) > max_size:
                scale = max_size / max(img.size)
                img = img.resize((max(1, int(img.width * scale)), max(1, int(img.height * scale))),
                                 Image.LANCZOS)
            out = os.path.join(out_dir, f"{name}.png")
            img.save(out)
            print(f"  {group}/{name}.png  {img.size[0]}x{img.size[1]}")


if __name__ == "__main__":
    main()
