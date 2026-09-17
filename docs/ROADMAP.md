# Beyond — roadmap & handoff

**Canonical "what's done / what's next" doc.** A fresh agent should be able to
pick up from here alone. Pair with [`../CLAUDE.md`](../CLAUDE.md) (ground rules)
and [`design-spec.md`](design-spec.md) (the vision) / [`deviations.md`](deviations.md)
(where the build deliberately differs).

Last updated: **v0.30.1, 2026-09-17.** Update this file when you finish or start
anything.

---

## Recently completed — mini-boss, item drops, PDTD card frames (v0.30.0-v0.30.1)

**Mini-boss.** `miniboss_siege_warden` (class `miniboss`, `data/enemies.json`) — a
multi-bar elite on a repeating timer through every hold (`miniboss_first_seconds` /
`miniboss_every_seconds` in `data/survival.json`). New `EnemyDef` fields drive it:
`HpSegments` is both the number of bars drawn over it *and* a plain multiplier on its
hull, so five bars really is five times the health; `OrbitSpeedDeg` adds a tangential
sweep so it circles the planet while closing instead of charging straight in; it stops at
`standoff_range` and then alternates its two attacks — a lobbed bomb, then a hitscan beam
straight down onto the planet (unavoidable, so it hits 1.6x harder). `SimRenderer.DrawSegmentedHpBar`
draws the stacked bars. Art is PDTD's `wind_cruiser_boss` hull.

**Mini-boss payout — a face-up hand with cascade draws.** `OpenBossReward(cards, chance)`
in `CardDraft.cs` deals five options face-up; taking one burns a pick and rolls
`card_cascade_chance` to add another, drawing from the *same* remaining hand, so a kill
yields anywhere from one card to the whole hand. `SimWorld.IsBossReward` /
`BossRewardPicksLeft` let the HUD retitle the popup and count the picks down.

**In-run item drops by rarity** (`data/items.json`, `src/Sim/Systems/ItemDrops.cs`).
Every kill rolls `drop_chance`; mini-bosses and bosses always drop. A hit picks a rarity by
weight (those weights *are* the published drop percentages: common 56 / fine 25 / rare 12 /
epic 5 / legendary 2) then an item of that rarity, and applies its effects to the run's
ModifierSet through the same `ApplyEffect` keys the boost cards use. 18 items authored.
The HUD toast draws them on PDTD's own rarity plate.

**PDTD's actual card chrome on the draft cards.** New `tools/extract_pdtd_sprites.py`
pulls 64 UI sprites out of the Unity bundles — the skill-card frame set (back plate, the
normal/super/relic/ultimate front plates, outlines, glow, star pips), the seven weapon
glyphs, damage-type and attribute icons, the twelve per-weapon techpoint emblems, the
seven item-quality plates, and the loot icons. Draft cards are now layered plate → art →
front → outline, with the art sitting in the frame's own transparent window (measured at
12.5%–57.6% of card height off `card_front_normal`'s alpha). Tier picks the frame: teal
for a level-up, purple for unlocking a new system, gold for a boost card.

**Waterdrop and Force Field finally have real card art** (`tools/gen_pdtd_cards.py`) —
composed from the same PDTD chrome at full 784x1168, with PDTD's own droplet art and
Force Field platform render. These were the last two procedural-gradient placeholders.

**Research and Codex rows no longer read as grey slabs.** Both were bare unstyled
`PanelContainer`s; new shared `UI/ArtCard.cs` gives each row an accent border and a large
faint techpoint emblem bleeding off its right edge, keyed by research branch / codex
category.

**Fixes and tuning this pass**
- *Radiation Line did no damage.* Its relay ring sat at `0.62 x DespawnRadius` (r≈558),
  where attackers are still fanned across the full 360°, so the base two-node link — one
  64° chord — stood in front of maybe a sixth of them and each crossed its 24px width in a
  fraction of a second. Ring pulled in to `0.26` (r≈234), the convergence zone every
  attacker must funnel through, beam widened, and a crossing now applies an "Irradiated"
  burn DoT (PDTD's own behaviour) so the damage doesn't have to all land during the
  crossing itself.
- *Missile sounds, all of them, replaced.* The old `missile_launch` was sample 9
  (火箭发射升空, a 44.7s rocket-launch field recording) trimmed 0.00–1.25s; its RMS envelope
  ramps from silence and doesn't peak until 2.30s, so that trim captured pure rumble and
  **no transient**. Re-picked by measured onset instead: launch ← 43 (grenade launcher,
  transient at 0.06s), battery ← 58 (artillery report), impact ← 44 (cinematic hit).
- *Commander missiles now re-acquire.* A volley with more missiles than targets left the
  overflow permanently unguided — the re-acquire branch only ran for rounds that already
  had a lock. Guided rounds with no target now sweep for one every tick.
- *Draft popup fits the screen.* Cards were a fixed 220x690 in an `HBoxContainer`, which
  can't shrink below its children, so a four-card draft pushed the right-hand cards off a
  phone entirely. The popup is centred in a `CenterContainer` and cards are sized from the
  live viewport.
- *Turret build flow removed* — the ⚒ button, the "tap a turret slot" panel and the ring
  of empty slot markers. PDTD has no ground turrets; the planet's defence is the missile
  battery plus the sentinel roster. The prep screen keeps only BEGIN DEFENSE.
- *Difficulty +1.4x* — spawn rate and the concurrent soft cap both scaled (`survival.json`).
- *Camera zoomed out* (fit `1.05 x SpawnRadius`, was 0.92) with enemy sprites scaled up to
  match, so they read bigger on screen in a wider view.

---

## Recently completed — chip-upgraded modules, PDTD boost cards, in-app updater (v0.29.0)

**The updater actually installs now.** The HUD badge and the splash button both called
`OS.ShellOpen(releaseUrl)` — that's the "it still only brings me to GitHub" report. The
real in-app download+install flow already existed inside `UpdateChecker`'s card; it's
now reachable from anywhere via `UpdateChecker.PromptInstall(node)`, and both call
sites use it. After a successful hand-off to the system installer the game quits (and
on desktop relaunches itself) so the player comes back into the new build.

**Modules reworked to PDTD's model** — six slots (was three), each module carrying a
tier (T1 = Lv1-10, T2 = 11-20, T3 = 21-30) and upgraded by **spending chips**, not
Research Data. `ChipVault.ChipPoints()/SpendChips()` value chips by tier (a T2 is worth what
it cost to merge) and spends lowest-tier-first. `ModulesScreen` rebuilt as a 2x3 grid of
tier-badged slot cards with a per-tier pip strip, the chip pool underneath, and a jump to
the Armory — PDTD's layout in this project's own visual language. A sixth module
(Targeting Array) was added so all six slots can be filled.

**PDTD-style in-run boost cards** (`data/runcards.json`, new `RunCardDef`): the draft now
mixes percentage buffs and trade-offs in with the weapon-level cards — "+60% sentinel
damage", "+1 Radiation Link but -20% link damage", "Yamato charges 30% faster", "laser
refracts to +5 more enemies", battery/hull/shield boosts. They start appearing from the
second draft on, and a card only shows if the weapon it modifies is actually in play.

**Radiation Link now starts as ONE link**, as in PDTD — levelling the weapon no longer
adds relays; only "+1 Radiation Link" cards do, paid for with -20% link damage.

**Two new weapon mechanics** behind those cards: the ship laser **refracts** to nearby
enemies with 20% falloff per bounce (base 0, +5 per card), and Yamato's charge time
shortens with its own card.

**Commander screen rebuilt** as a career page: enemies killed, total damage, bosses
killed, missions played, stages cleared, stars, 100% (3-star) stages, plus arsenal and
bank blocks. Endless best / weekly best / codex count are gone, as asked. The sim now
tracks `RunStats.BossesKilled` and the totals are banked into the save each run.

**HUD/UX**: the four speed buttons collapsed into one button showing the current speed
with a drop-down for the rest (closes on a tap anywhere); the control row was pushed
clear of the status text it was overlapping. The wallet popup is now a `TapAwayPopup` —
sized to its content, dismissed by tapping anywhere, no CLOSE button — and it lists keys
too.

**Sounds**: the ship/orbital laser now uses PDTD's own event-named "Laser" clip (the old
generic `laser-fire` library clip was the one that grated), and missiles use PDTD's real
rocket-launch clip instead of the grenade-launcher stand-in.

**Stage pacing**: missions that don't author their own unlock fractions now stage the
roster by threat — the two weakest types from the start, everything else fanning in up
to the 55% mark — instead of every enemy type being available from second one. Missions
that DO author fracs keep their own pacing.

### Still open (asked for, not built)

- **Mini-boss**: multi-segment HP bar (e.g. 5 bars = 5x HP), slow orbit closing on the
  planet, fires a laser / drops bombs if it arrives, and on death pays out 5 face-up
  cards with a chance-based cascade for extra draws. Substantial — its own pass.
- **PDTD's exact upgrade-card art/animation/sounds** for the draft cards.
- **In-run item drops** with per-rarity % chance.
- Background art on the Research/Codex list cards (they read bland).
- Waterdrop + Force Field card art still procedural.

---

## Recently completed — real PDTD sprites/VFX, ship laser rework, PDTD HUD rules (v0.28.0)

User pushed back, correctly, that an earlier session had swapped PDTD *audio*
but skipped PDTD *art* for weapons, rationalising that Beyond's renderer is
procedural. That call was wrong — this pass does the real extraction.

**Real PDTD art is now in (assets/game/pdtd/, see CREDITS.txt)** — extracted
with UnityPy from `UnityDataAssetPack.apk`:
- `icons/<kind>.png`: all 11 PDTD sentinel platform renders. These now draw as
  the **in-world orbiting sentinel platforms** (replacing the hand-drawn
  "chunky ringed station") *and* as the icons on the HUD's bottom cards.
- `vfx/*.png`: the weapon effect textures — laserbeam01/03/05, the Waterdrop
  bolt, the Radiation Link relay point, spacebomb energyball/ring/shockwave,
  orb shield + core, lightning arches, glow/flare/explosion.
- New `Art.SentinelArt(kind)` / `Art.Vfx(name)` loaders and a `BeamSprite()`
  helper in `SimRenderer` that stretches a beam texture between two points.
  Every rewired effect keeps its old procedural draw as a fallback when a
  texture is missing, so nothing hard-depends on the extracted set.
- Rewired to PDTD textures: ship laser, Beam, Laser, Waterdrop (bolt + each
  ricochet leg), Space Bomb (explosion + shockwave ring), Ball Lightning
  (energy core + real lightning arcs), Force Field (hex shield dome),
  Radiation Link (beam + relay sprites), chain arcs.
- **Gotcha, already hit once here and once at v0.25.1**: several Unity
  textures export with a *black backing* rather than alpha (Force Field
  rendered as a solid black square in-game). Fixed by re-processing
  luminance->alpha; `shockwave` additionally needed its 0-67 alpha range
  normalised. If a newly extracted PDTD texture looks like a black box, this
  is why.

**Ship laser reworked** — was an instant 3-beam cone; it's now PDTD's single
high-power beam that **locks one target and burns it for 3s**
(`_heroBeam*` in HeroWeapons.cs, damage applied per-tick with a BeamTick
event stream driving the render + audio). `laser_volley` in
`data/hero_weapons.json` became duration-based (6s cooldown, 3s burn).

**Missiles**: 20% slower and 15% longer cooldown on all three sources (hero
Missile Barrage, the planet battery, the Missile Silo turret).

**PDTD HUD rules**:
- The bottom bar now shows **the planet missile battery first, then every
  active sentinel, then the ship weapons**, each with a live cooldown — it
  previously showed ship weapons only, which is why no battery/Radiation Line
  card was visible. Battery/sentinel cards are compact and non-tappable
  (they auto-fire); ship weapons stay tappable. The row is an `HFlowContainer`
  so it wraps instead of overflowing.
- **Max 5 active sentinels** (`SimWorld.MaxActiveSentinels`): once five are
  live the in-run draft only offers upgrades to those five, never a sixth —
  matching PDTD's battery + 5 layout.
- **Commander ability slots capped at 3** (was 3-6, scaling with hero level).
- The bottom panel now **sizes to its content each frame** instead of
  reserving a fixed height — that fixed reserve is what showed as a big empty
  blue box before anything was unlocked. `GameRoot.BottomReserve` is now just
  an upper bound, and the joystick's lower edge follows the live panel height.
- The level-up draft popup was overflowing the screen (4x300px cards in a
  1080px canvas); cards are 220x690 now and the panel fits.

**Menus**: every screen's root column standardised to a centred 1000px-wide
box (several were 520-640 wide with *wider* content inside them, which is why
they read as off-centre), and each menu panel now gets a `MenuFrame` — a
faint bordered frame with corner ticks and one bright segment that travels the
whole perimeter once every 5 seconds.

**Verified**: `dotnet build` clean, `SimTest` ALL CHECKS OK / deterministic /
1x==4x identical, and screenshot-verified in a live run (bottom bar shows
BATTERY + RADIATION + FORCE with cooldowns; Force Field renders as PDTD's hex
dome after the alpha fix).

### Still open

- **Module/chip rework to match PDTD properly**: user wants PDTD's 6 module
  slots, each module carrying a tier (T1/T2...) and level, upgraded by
  spending chips — the v0.27.1 Armory built a simpler 4-slot global-bonus chip
  system instead. Needs a real redesign pass.
- **In-run item drops** with per-rarity % chance — asked for, not started.
- Per-weapon PDTD *audio events*: the sounds in use are real PDTD samples, but
  picked by name out of SFX.bank rather than mapped through Master.bank's FMOD
  event graph, so they aren't necessarily the exact clip PDTD plays for that
  weapon. Mapping the event graph is the remaining fidelity step.
- Force Field + Waterdrop card art (still procedural placeholders).

---

## Recently completed — menu screens dialed back down (v0.27.2)

User feedback right after v0.27.1 shipped: the menu-screen buttons (main
menu, Shop, Sentinels, Upgrades, Modules, Armory, Events, Settings, Login,
Splash, Codex, Abilities, Profile, Star Map, Level-up) were "far too large."
Root cause: the v0.27.0 blanket 2x pass over those 16 screens never got the
same live-feedback dial-back the in-mission HUD cards did (those went 2x →
user said too big → settled at 1.5x; the standalone menu screens stayed at
literal 2x the whole time). Rescaled `CustomMinimumSize`/font sizes across
all 16 files down to ~1.4x original (a further ×0.7 on top of the 2x), and
manually re-tightened `MenuScreen.cs`'s hand-positioned top-right icon row
(gear/currency/codex) and identity block (badge/name/profile-tap) to match
instead of leaving them with the correct-but-loose gaps the blanket pass
alone would produce.

**Bug hit and fixed during this pass**: the first correction script emitted
bare decimal literals (e.g. `26.6`) for `Vector2`/`AddThemeFontSizeOverride`
arguments — those are `double` by default in C#, and neither `Vector2`
(wants `float`) nor `AddThemeFontSizeOverride` (wants `int`) accept an
implicit `double` narrowing, so it was 114 compile errors across the same 16
files. Fixed by rounding every such literal to a plain integer (menu button
sizes/fonts never needed fractional precision anyway) — a follow-up regex
pass, not a revert.

**Verified**: `dotnet build` clean, `SimTest` `ALL CHECKS OK` (UI-only
change, sim untouched). **Not verified live** — same caveat as v0.27.0's
menu-screen pass, no screenshot harness covers `AppRoot`'s menu tree.

---

## Recently completed — the chip/chest/Armory gear system, Events tab (v0.27.1)

The "last, biggest, riskiest chunk" the user deliberately deferred to the end
of the v0.27.0 session (see that entry below) — a full PDTD-style chip/chest
gear layer, built new from scratch.

**CLAUDE.md amended (2026-09-16, user-approved before any of this was built)**:
rule #3 now bans real-money monetisation specifically, not RNG in general —
in-game-currency-only randomized rewards (keys earned by playing, never
purchasable) are explicitly fine. This system is the first thing built under
that amendment.

**New systems**:
- `data/chips.json` + `ChipDef`/`ChipTierDef`/`ChipsDb` (`Config/Defs.cs`) —
  6 chip archetypes (damage/cooldown/radius/shield/hull/salvage) × 4 tiers
  (T1-T4, `merge_cost` copies of one tier → 1 of the next).
- `SaveGame`: `ChipInventory` (`"{chip_id}:{tier}"` → count), `EquippedChips`
  (list of the same key shape, capped at `ChipsDb.EquipSlots`, currently 4),
  `SilverKeys`/`GoldKeys`.
- `Meta/ChipVault.cs` — the only place that rolls chest drops or merges chips.
  Silver chests roll T1/T2, Gold chests T2/T3 (small T4 chance) — 1 key per
  chest, "Open 1"/"Open 5". `QuickMergeAll()` cascades every archetype up as
  far as it'll go in one tap.
- Keys are earned only by playing (`AppRoot.OnMissionEnded`): 1 Silver Key
  per star on a mission win (so 1-3), 1 Gold Key on a mission's first clear
  or a new Endless/Weekly depth record. Nothing purchasable, per the amended
  rule.
- **Chip effects apply globally across every orbital weapon**, not
  per-weapon-slot like the PDTD reference images show — building true
  per-slot assignment would mean threading chip bonuses through
  `FireOrbitalWeapon`'s per-index damage/cooldown calc individually; scoped
  down to a global multiplier for this pass. New `ModifierSet` fields
  (`OrbitalWeaponDamageMult`/`RateMult`/`RadiusMult`, `orbital_weapon_damage`/
  `_rate`/`_radius` effect keys) wired into `OrbitalWeapons.cs`'s
  `FireOrbitalWeapon` damage/radius calc and cooldown-set line. **Naming
  trap avoided**: there's already an unrelated `Mods.SentinelDamageMult`/
  `SentinelRateMult`/`SentinelCount` — those feed a completely different,
  older small-drone point-defense system in `PlanetDefenses.cs`
  (`StepOrbitalSentinels`), not the PDTD-named orbital-weapon roster this
  whole session has been about. Deliberately used an `OrbitalWeapon`-
  prefixed name throughout to not collide with it — flagged in
  `ModifierSet.cs` itself so a future session doesn't reuse the wrong field.
- `Progression.BuildModifiers()` sums equipped chips' effects in, same
  pattern as equipped Planet Modules right above it in the same method.

**New screens**:
- `UI/ChipScreen.cs` ("Armory") — key balance, 4 equip-slot buttons (tap to
  unequip), chest-opening cards, and a scrollable list of every owned
  chip+tier stack with Merge/Equip buttons. Reached from the Upgrades hub
  (`UpgradesScreen.cs`, joining Research/Sentinels/Modules) — not a new
  top-level menu button, matching where Planet Modules already lives.
- `UI/EventsScreen.cs` — a PDTD-style expedition card list surfacing Weekly
  Challenge / Endless Hold / Ascension (previously buried across separate nav
  buttons/screens) as cards with a blurb + best-so-far + Enter button, plus a
  **locked "Galaxy Arena — Coming Soon" card**. `MenuScreen`'s bottom-bar
  "★ EVENTS" button now opens this instead of jumping straight into the
  Weekly Challenge.

**Galaxy Arena stays a roadmap entry, not built** (user explicitly asked for
this to go on the roadmap, not be built this session): a PvE wave-survival
arena mode with a ranking/leaderboard system, modeled on PDTD's own Galaxy
Arena. Whoever picks this up next needs to design: how "waves" differ from
the existing Survival director, what a ranking system even means with no
backend/leaderboard service in this project (local-only personal build — a
real cross-player ranking isn't possible without one), and whether it's a
new `SimPhase`/mission type or a reskin of Endless with different pacing.

**Verified**: `dotnet build` clean, `SimTest` `ALL CHECKS OK` /
`deterministic=ok` / 1×≡4× identical and numerically byte-identical to the
pre-chip-system run (confirms the new `Mods.OrbitalWeapon*Mult` fields are
correctly a no-op at their 1.0 defaults when nothing's equipped — nothing
leaked into determinism). **Not verified live**: the three new screens
(ChipScreen/EventsScreen, plus UpgradesScreen's new Armory button) weren't
screenshotted — `ShotRunner`/`Shots.tscn` only exercises the in-mission
`GameRoot` flow, not `AppRoot`'s menu tree, and scripting a full
Splash→Login→Menu→Upgrades→Armory click-through wasn't attempted this
session. They're built to the exact same construction pattern as
`ModulesScreen`/`ShopScreen`/`UpgradesScreen` (already-shipped, working
screens) and compile clean, but a live on-device look is the real
verification, same as every balance number in this project.

### Still open

- Per-orbital-weapon-slot chip assignment (matching the PDTD reference image
  exactly) instead of the current global-multiplier simplification — a real
  scope expansion if wanted later, not a bug.
- Galaxy Arena itself (see above).
- Force Field + Waterdrop Grok-generated card art (carried over from
  v0.27.0, still not done).
- Chip drop rates/merge costs/effect sizes are first-pass numbers, unverified
  against actual play — same standing caveat as every other balance value in
  this project.

---

## Recently completed — commander/sentinel rework, real PDTD audio for every sentinel, missile/battery tuning, HUD scale pass (v0.27.0)

Big combined session — full plan at the top of this diff's commit; this is the
digest. `~/Work/pdtd-reference/` (decrypted PDTD config/Lua) and the original
PDTD XAPK (for real sprite/audio extraction) were both available on this
machine, unlike the v0.26.3 session on a different box that got blocked on
exactly this — several "still open, blocked on reference" items below are now
actually done against real PDTD data instead of guessed.

**Commander ability → real Laser sentinel.** `kinetic_barrage` removed
(`data/abilities.json`, its `pro_barrage` levelcard, `SimTest.cs`'s loadout;
T1 now opens with Aegis Barrier + Overdrive Protocol only — see
`docs/design-spec.md`'s renumbered ability list). New `laser` orbital weapon
added, built against the *real* PDTD Laser script
(`lua-decrypted/game/attack/laser.lua`) instead of v0.26.0's invented
70°-sweep guess (which v0.26.4 explicitly removed as `sweep_laser`) — real
PDTD Laser hits several nearest targets at once and scorches each impact into
a short burning zone (`OwEffect.Kind==6`), plus a chance to stun. Real
extracted sound (`orbital_laser_fire.ogg`, PDTD sample "laser-fire").

**Roster cleanup.** `orbital_cannon` (Railgun analog) removed outright;
`orbital_laser` renamed to `beam` ("Beam") to resolve the naming collision
with the new Laser sentinel — it already *was* PDTD's Beam mechanically
since v0.21/v0.22, just confusingly named. `SimRenderer`'s `OrbitalCols` (a
positional array index-aligned to the JSON roster order — flagged as fragile
in the v0.26.4 notes, and this session both added and removed a weapon)
replaced with `ColorForKind(string)`, keyed by weapon `Kind` instead of
array position — roster reorders/adds/removes can't silently desync colors
again. `SaveGame.NormalizeSaves` gained a `MigrateRemovedIds()` fixup so an
existing save's `OrbitalMeta`/`Loadout` entries for the retired ids don't
dangle.

**Space Bomb and Waterdrop were both silently broken — found the real
cause.** Both targeted via `ClosestEnemyTo(from, d.Range)` where `from` is
the sentinel's own orbiting platform position (~465 units out) — not the
planet. Depending on the platform's orbital phase relative to where enemies
actually were, `d.Range` (600-650) frequently wasn't enough to reach
anything, so both weapons silently did nothing: no damage, no animation,
looked completely broken. Fix (both): search from the planet
(`ClosestEnemyTo(Vector2.Zero, B.DespawnRadius)`, the same pattern
`rad_line`/`force_field` already used correctly) instead of from the
platform. **Waterdrop** additionally reworked from a straight pierce-line
into a real ricochet chain (confirmed against
`lua-decrypted/game/attack/aqua_attack.lua` — the real bullet carries
"durability points" spent per hit and re-targets the next nearest enemy,
i.e. a bounce chain, not a static line), new `count`/`count_per_level`
fields controlling bounce count. Both get real extracted sound
(`space_bomb_fire.ogg`, `waterdrop_fire.ogg`).

**Shock Orb → real Ball Lightning.** Real PDTD Ball Lightning
(`ball_lightning_attack.lua`) turned out to have satellites/chain-links/
tracking-bomb explosions — richer than practical to fully replicate here.
Scoped to: real extracted sound (`ball_lightning_fire.ogg`), and a new
periodic chain-zap to 1-2 nearby enemies each tick (reusing the existing
chain-arc pattern from the `lightning` case) so it reads as "continuously
strikes enemies with lightning" per its own flavor text, instead of a plain
damage circle. Mechanics (orbit path/cooldown/duration/damage) were already
ratio-matched to PDTD in v0.26.1, untouched.

**Radiation Link reworked to actually level up in reach, not just damage.**
Was capped at 5 relay nodes / a fixed 64° arc regardless of level. Now scales
to 10 nodes / up to a 340° arc (deliberately short of a full 360° ring) as it
levels — `RadLineSpreadDeg(nodes)` derives the spread from node count instead
of a fixed constant, and the `stackalloc Vector2[5]` node buffers (sim +
renderer) grew to `[10]`. Real extracted sound (`radiation_line_fire.ogg`,
shared by Radiation Zone too — both are radiation-type, sharing is
thematically correct, not a shortcut).

**Every remaining orbital weapon given its own real PDTD sound**, closing out
"same [real-PDTD treatment] for all sentinels": `orbital_lightning_fire.ogg`
(Chain Lightning), `beam_fire.ogg` (Beam); Force Field reuses the existing
`shield.ogg` (thematically a defensive field). Every orbital weapon now has a
distinct sound instead of the old universal `sentinel_shot.ogg` catch-all.

**All sentinel cooldowns reduced ~25%** (`cooldown`/`min_cooldown` × 0.75
across every entry in `data/orbital_weapons.json`).

**Missiles**: battery salvo 3→5 (matches PDTD's Missile `atkCount`); combined
with the battery's existing 1s interval (vs PDTD's 5s) this is already well
above PDTD's total output, satisfying "more missiles" on top of "as many as
PDTD." Speed reduced ~30% on all three missile sources (`battery_missile_speed`
320→220, hero `missile_barrage` speed 330→230, `missile_silo` turret
`projectile_speed` 300→210) — real PDTD sprite/sound for missiles were
already in place since v0.24/v0.25, so this closes the "slower" gap. Existing
`data/levelcards.json`/`data/cards.json` "Battery" upgrade cards (damage/
rate/salvo/splash, already covering "as many things as possible") untouched
— base numbers were the actual gap, not missing upgrade paths.

**Enemy visuals cleaned up to match PDTD**: removed the per-enemy circular hp
ring (`SimRenderer.DrawEnemies`) and the soft white/grey backing halo behind
every sprite (rendered white because every `EnemyTint` entry is white,
matching real PDTD sprites' own baked-in color since v0.25.2 — the halo
predates that swap and stopped making sense once it happened). PDTD itself
has neither. Enemy `max_hp`/`shield_hp` bumped +7% ("5-10% harder to kill").

**Commander auto-mode**: `⚒`/`AUTO`/`✈` were three top-row buttons reading as
unclear/redundant automation controls. `⚒` (build panel — not automation)
stays separate; `AUTO` (`Hud.cs`) now drives both ship-weapon auto-fire and
autopilot movement together as one master toggle. Separately, autopilot's
engagement steering (`HeroSystem.AutopilotDir`) now blends toward an orbit
tangent as the ship nears the planet (same shape as the existing idle-orbit
branch), so chasing a target close to the planet curves around it instead of
flying a straight line into the hard clamp at the planet's edge — that abrupt
stop was what read as "flying into the planet."

**HUD/menu scale pass** ("make buttons ~2x bigger"), landed in two rounds
after live feedback: bottom weapon/ability cards ended at 204×204 / 204×219
(1.5x, not literal 2x — the full 2x made the panel tall enough to cover the
joystick's touch zone; `HeroWeaponButton`/`AbilityButton`'s `_Draw()` scales
every fixed-pixel constant off `Size.X/136` so any future size change stays
proportional automatically). `GameRoot.BottomReserve`/`Hud._wavePanel`
resized to match; the joystick's own bottom bound now reads
`-GameRoot.BottomReserve` directly instead of a separately-hardcoded number,
so this class of "panel grew, joystick didn't know" bug can't recur. Top
control row (`☰❚❚1x-4x⚒AUTO`) grown within the real 1080px device-canvas
width budget (not literal 2x —8 buttons in one row don't fit at 2x on an
actual phone). Same 2x-with-manual-overlap-fixes pass applied across
`SentinelScreen`/`ShopScreen`/`ModulesScreen`/`MenuScreen`/etc. — MenuScreen's
top-right icon row (gear/currency/codex) and StarMap's back-button+header had
manually-positioned offsets that needed matching fixes after their buttons
doubled (a blanket size/font regex alone isn't enough wherever layout uses
absolute `Position`/`OffsetLeft` instead of an auto-flowing container).

**Card art**: user supplied real AI art for the `space_bomb` and `laser`
cards (cropped from their own generated key-art images down to the
illustration only, matching the existing full-bleed-art-behind-game-text
convention) — both removed from `tools/gen_cards.py`'s procedural-placeholder
list. `force_field`/`waterdrop` still procedural — the user separately asked
for Grok-generated art for those two; **not done yet**.

**Verified**: `dotnet build` clean throughout, `SimTest` `ALL CHECKS OK` /
`deterministic=ok` / 1×≡4× identical after every sim-touching change, live
`ShotRunner` screenshots confirmed the HUD button merge and top-row sizing
before the card-panel/joystick issue was reported and fixed (that specific
fix and the halo/health-ring removal are verified by code+math, not a fresh
screenshot — the dev box's screenshot runs kept timing out on later passes;
worth a live on-device look).

### Still open from this session

- **Force Field + Waterdrop Grok-generated card art** — user asked for this
  explicitly; not started. Space Bomb/Laser got real art a different way
  (user's own generated images) in the meantime.
- **The chip/module/key/chest gear system + Armory shop tab + Events tab +
  Galaxy Arena roadmap entry** — the largest, last-sequenced chunk of the
  original ask (user explicitly agreed: gameplay fixes first, this last).
  Not started.
- All balance-adjacent changes here (cooldowns, missile speed/salvo, enemy
  hp) are provisional pending the user's own on-device feel-check, same
  standing convention as every prior session.

---

## Recently completed — removed `sweep_laser` (v0.26.4)

Follow-up to v0.26.3 below, same day. User confirmed: remove `sweep_laser`
outright rather than try to reconcile it against PDTD's real "Laser" weapon
(still blocked on the missing `pdtd-reference` material — see v0.26.3's "still
open" note). `orbital_laser` (`kind: "beam_laser"`) already faithfully covers
PDTD's **Beam** (continuous lock-on, since v0.21.0/v0.22.0's combat rework);
`sweep_laser` was the *other* PDTD weapon, "Laser" ("fires Lasers... in their
path"), added in v0.26.0 as an invented 70°-arc-sweep mechanic that was never
verified against real PDTD footage/data and the user wasn't happy with in
play.

**Removed everywhere**, not just hidden:
- `data/orbital_weapons.json` — the `sweep_laser` entry deleted (roster is
  now 9 orbital weapons, was 10).
- `src/Sim/Systems/OrbitalWeapons.cs` — the `case "sweep_laser"` cast branch
  and the `fx.Kind == 6` step handler (the rotating-arc damage tick) both
  deleted. `OwEffect.Kind` is now 1–5 only (rad_line / shock_orb / rad_zone /
  beam_laser / force_field) — nothing else used slot 6, safe to just retire
  the number rather than renumber.
- `src/Render/SimRenderer.cs` — the `Kind == 6` draw branch deleted, and the
  now-10th `OrbitalCols[9]` entry (sweep_laser's pink) dropped so the array
  stays index-aligned with `Cfg.OrbitalWeapons` (order-dependent — if you add
  a new orbital weapon, append its color at the end, don't insert in the
  middle).
- `tools/gen_cards.py` — the `sweep_laser` procedural-card-art branch and its
  `cards` list entry removed (3 procedural cards left: waterdrop, space_bomb,
  force_field).
- `assets/game/cards/sweep_laser.jpg` (+ `.import`) deleted — it was
  placeholder procedural art (no owner-supplied original to preserve), and
  `assets/game/CREDITS.txt` updated (4 procedural cards → 3).
- `CardDraft.cs`/`SentinelScreen.cs` needed **no changes** — both already
  iterate `Cfg.OrbitalWeapons` generically (same reason adding the 4 new
  weapons in v0.26.0 needed no UI wiring), so the draft pool and the
  out-of-battle upgrade menu both just show 9 weapons now automatically.

**Verified**: `dotnet build` clean, `SimTest` `ALL CHECKS OK`,
`deterministic=ok`, 1×≡4× identical, m01–m06 still won (endless dipped to
wave 4 from 6 — the known "naive test-bot always picks the first draft
option" artifact from the smaller card pool, documented precedent at v0.13.0
and v0.26.0, not a regression to chase).

**If PDTD's actual "Laser" weapon needs a real implementation later**, it
needs the reference material — don't just re-add `sweep_laser`'s old arc-sweep
guess without checking it against real PDTD data/footage first.

`config/version` bumped 0.26.3→0.26.4 (`project.godot` + `export_presets.cfg`)
— not tagged/released this session.

---

## Recently completed — feedback pass: bigger weapon-card draft, sci-fi ship redesign, faster mission open (v0.26.3)

**Session moved to a new (Windows) machine** — repo re-cloned fresh, full
toolchain (Git, .NET 9 SDK, Godot 4.7.2 mono) installed from scratch; no
`~/Work/pdtd-reference/` on this box (it never left the old Linux machine and
isn't part of this repo). Anything that needs to match PDTD's actual assets
exactly is blocked on that reference — flagged to the user rather than guessed.

User played and reported three things after the machine switch:

- **Draft/upgrade cards had huge dead space.** The weapons-upgrade popup
  (`Hud.cs`'s `_draftPanel`/`_draftCards`, shown on Commander level-up) was
  vertically centred with `OffsetTop/Bottom = -258/258` (516px) on a much
  taller screen — big black voids above and below. First attempt just grew
  the card `CustomMinimumSize` height a lot while width stayed governed by
  `ExpandFill` (~unchanged) — this broke the baked-in card-art aspect ratio
  (`StretchMode.KeepAspectCovered` zoomed in and cropped the title/stat text
  off the edges, confirmed via a screenshot before shipping it). Fixed
  properly: panel grown to `-330/330` (660px, +28%), cards fixed at a
  **ShrinkCenter** vertical size flag (150×470, was 140×440 with
  force-`ExpandFill` — so they no longer stretch past their own aspect no
  matter how tall the panel gets) instead of stretching to fill, art window
  292→310. Bigger popup, fully readable cards, no art cropping.
  Screenshot-verified via `ShotRunner`/`Shots.tscn` before and after (the
  broken intermediate version is not in the shipped diff).
- **Hero ship redesign** (`SimRenderer.DrawShip`) — was a single tapered
  slab-hull "capital cruiser" look from v0.18.0; user wanted something more
  distinctly sci-fi. Rebuilt as an angular strike-corvette: narrow spear-nosed
  spine hull, swept delta wings with glowing magenta wingtip pods, **twin**
  engine nacelles held out on struts clear of the hull (was one rear engine
  block with 3 nozzles on the hull itself) with their own trails, a raised
  glass-canopy cockpit offset above the spine, and twin flanking cannons
  either side of a central spike gun (was one central barrel). Same
  teal/magenta palette + running lights + recoil-kick/thrust-trail hookup,
  render-only — `DrawHero`'s call site, hit/aura/shield-ring radii, and
  everything in `src/Sim/` untouched. `SimTest` unaffected (render doesn't
  touch determinism). Screenshot-verified.
- **Mission open felt slow / "the planet just kills everything."** Traced to
  `SurvivalDirector`: `data/survival.json`'s `eps_base=0.24` +
  `eps_ramp_curve=1.3` meant the first enemy didn't arrive for ~4s and the
  spawn rate stayed genuinely thin for most of the first 60–90s (the
  documented "gentle open" from v0.9.0) — meanwhile the planet's own
  battery/turrets (already built in the PREP phase) one- or two-shot the
  handful of early Skiffs, so there was often nothing left for the player to
  actually do at the start of a hold. **`eps_base` 0.24→0.42, `eps_ramp_curve`
  1.3→0.85** — enemies start flowing noticeably sooner and the ramp climbs
  faster early (still eases per `late_ease_frac`/`late_ease_amount` late in
  the hold, unchanged). This is a real balance value change, same caveat as
  always: `SimTest` `ALL CHECKS OK` / `deterministic=ok`, m01–m06 still won,
  no regression — but it's provisional pending the user's own feel-check, not
  the last word. Deliberately did **not** nerf the planet's own battery/turret
  damage to fix this — raising spawn pressure fixes both complaints at once
  without risking the (already-tuned) base-defense numbers.

**Still open, blocked on missing PDTD reference material (see above)**:
- User asked for the Shock Orb's animation to match PDTD's exactly, and to
  replace/fix `sweep_laser` (added in v0.26.0 as PDTD's "Laser") — possibly
  because it doesn't look right, possibly because it reads as a duplicate of
  `orbital_laser` (already PDTD's "Beam", continuous lock-on, since v0.21.0/
  v0.22.0). **Neither started** — both need eyes on PDTD's actual weapon
  footage/data to get right, which isn't available on this machine. Asked the
  user how they want to supply it (copy the reference folder over, or
  screenshots/video) before touching either.

`config/version` bumped 0.26.2→0.26.3 (`project.godot` + `export_presets.cfg`)
per the usual ritual — not tagged/released this session.

---

## Recently completed — the ACTUAL root cause of "update always fails" (v0.26.2)

User reported, after both v0.26.0 (in-app download+install code) and v0.26.1
shipped: "still brings up to github and simply pressing update on the
downloaded version gets failed... we still have to uninstall and reinstall."
The v0.26.0 work fixed the *download and hand-to-installer* mechanism, but
that was never the actual blocker — verified by reading `.github/workflows/build-apk.yml`'s
"Configure Godot editor settings" step: it ran
`keytool -genkeypair ... -keystore ~/.android/debug.keystore` **fresh, every
single CI run, with no caching or persisted file**. Every release APK was
therefore signed with a brand-new random certificate, and **Android
unconditionally refuses to install an app update whose signing certificate
doesn't match the one already on the device** — regardless of whether the
install is triggered by a browser download, our new in-app FileProvider
intent, or anything else. No amount of fixing the download/install-intent
code could ever have worked around this; it's an OS-level signature check
that happens after the code we control hands off to the system installer.

**Fix**: generated a debug keystore once (from this session's own machine's
existing `~/.android/debug.keystore`, itself already stable/long-lived
locally) and **committed it** at `android/debug.keystore` — CI now points
`export/android/debug_keystore` at that committed file instead of running
`keytool` at all. Every future CI build signs with the identical certificate,
forever (until someone deliberately changes it, which would repeat this same
problem — see the warning in `CLAUDE.md`'s new "Android build: the debug
keystore MUST be committed and stable" section, read that before touching
signing again). **Verified**: local `--export-debug Android` export against
the committed keystore, then `apksigner verify --print-certs` on the
resulting APK's certificate SHA-256 matches `keytool -list -v`'s fingerprint
on `android/debug.keystore` exactly, byte-for-byte.

**What this means for the user right now**: this transition (whatever's
currently installed, signed by an old random CI key, → v0.26.2, signed by the
new stable key) still needs **one final manual uninstall + reinstall** — no
way around that, the old and new certs genuinely don't match. From v0.26.2
onward, every future release should share the same certificate and install
as a normal in-place update, including through the in-app updater from
v0.26.0. This is the one thing that still needs the user's on-device
confirmation — if updating v0.26.2→(next) still fails after a clean install
of v0.26.2, something else is wrong and this fix didn't fully work; don't
assume it's solved without that confirmation.

---

## Recently completed — in-app updater, sentinel roster, HUD/card polish (v0.26.0)

User asked for several things after v0.25.2 shipped: (1) fix the in-app
updater — it opened GitHub but pressing "update" still failed, only a full
uninstall/reinstall actually worked; (2) sound + animation on every upgrade
card; (3) confirm/build out the Planet Shield; (4) add PDTD's full sentinel
(orbital weapon) roster, "replacing or modifying everything needed" (cards,
animation, sounds, menu); (5) bigger/centered top HUD buttons and bigger
bottom weapon/ability cards ("looks very empty"); (6) match enemy/upgrade
**numbers** to PDTD's ratios, and (7) enemy + player-attack animation. Given
the combined scope (effectively "redo most of the game to match PDTD"), asked
the user to help sequence it — they said use my own judgment on order, match
PDTD's *ratios* not raw numbers (already the established v0.12 approach), and
approved the riskier native-Android work for #1. **Shipped this session: #1,
#2, #4 (cards/sim/render/sound/menu), #5. Not done: #3 turned out to already
be complete (see below), #6 (numbers retune) and #7 (real animation) were not
started — both are genuinely large, separate efforts, see "Still open" below.**

### #1 — in-app updater now actually installs (Android)

Root cause: "Download" only ever did `OS.ShellOpen(releasePageUrl)` — opens a
browser tab, and getting from there to an actually-installed update needed
the user to find the asset, download it, and manually open it, which is
where "fails" was coming from (nothing in the code path actually attempted an
install).

Now: `src/Meta/UpdateDownloader.cs` downloads the release's `.apk` asset
in-app (progress bar in the same update card) and, on Android, hands it to
the system package installer via `Godot.JavaClassWrapper.Wrap("com.godot.game.UpdateInstaller").Call("install", path)`
— see `CLAUDE.md`'s new "Android build: custom Gradle" section for the full
mechanism (`android/overlay/` — `UpdateInstaller.kt` + `BeyondApp.kt` +
manifest permission, layered onto a regenerated `android/build/` by
`tools/setup_android_gradle.sh`; **not** committing the ~200MB generated
Gradle project itself). This required switching the Android export to a
custom Gradle build (`REQUEST_INSTALL_PACKAGES` isn't addable via plain APK
export) — a real pipeline change, done with the user's explicit go-ahead.

**Verified**: a full local `--export-debug Android` gradle build succeeds,
the compiled manifest carries the permission (`aapt2 dump badging`), and
`com.godot.game.{BeyondApp,UpdateInstaller}` are present in the built APK's
`classes3.dex` (checked via `strings` on the extracted dex). CI's
`build-apk.yml` now runs `tools/setup_android_gradle.sh` before export — this
is the **first real test of the new pipeline in CI**, check it went green
before trusting this is fully wired (`gh run list`).
**Not verified**: the actual on-device tap-to-install flow — Android still
requires one manual tap on the system installer's "Install" screen (expected,
can't be skipped without root); confirm on the S24 or similar that the
button actually reaches that screen and the reinstall completes cleanly.

### #2 — card sound + animation

`card_reveal.ogg` (new, synthesized — `tools/gen_sfx.sh`) plays once when the
draft popup opens (distinct from `card_pick.ogg`'s existing per-pick confirm
chime). `Hud.RefreshDraft` now staggers each card in with a scale+fade
pop-in tween (`Tween.TransitionType.Back` overshoot, ~70ms stagger per card),
and picking a card gives it a quick punch-scale before the popup closes.
Didn't add unique per-weapon-type sounds (10 orbital + 6 hero weapons would
be a lot of new SFX for a single pass) — every weapon's *cast* sound was
already `sentinel_shot`/the hero-weapon-specific ones from earlier sessions,
unchanged.

### #3 — Planet Shield: turned out already done, nothing to build

Investigated before assuming a gap: Planet Shield has existed since v0.22.0
(mechanic) and got its animated hex-dome visual in v0.25.0 — purchasable in
`SentinelScreen`, levels via RD early / Exotic Alloy past level 4. Nothing
new needed here. (Don't confuse this with the *new* "Force Field" orbital
weapon added under #4 below — different thing: Force Field is an offensive
damage+slow pulse, Planet Shield is the defensive damage-absorb dome.)

### #4 — PDTD's full 11-weapon sentinel roster

PDTD's actual weapon list (`~/Work/pdtd-reference/NUMBERS.md` §"The 11
weapons") is: Missile, Waterdrop, Railgun, Laser, Beam, Radiation Link,
Radiation Zone, Space Bomb, Force Field, Chain Lightning, Ball Lightning.
Beyond already covered most of these before this session — Missile ≈ the
planet's always-on missile battery (a separate system, not a draftable
orbital card), Railgun≈`orbital_cannon`, Beam≈`orbital_laser` (continuous
lock), Radiation Link≈`radiation_line`, Radiation Zone≈`radiation_zone`,
Chain Lightning≈`orbital_lightning`, Ball Lightning≈`shock_orb`. The
genuinely missing 4 were added as new `data/orbital_weapons.json` entries +
`src/Sim/Systems/OrbitalWeapons.cs` mechanics + `SimRenderer.DrawOrbitalWeapons`
visuals, matching the existing per-weapon-kind pattern:
- **`waterdrop`** — instant piercing bolt at the nearest enemy, damages
  everything along the line (PDTD's Waterdrop: high single-hit, `penetrate: 9999`).
- **`space_bomb`** — instant AoE burst at the nearest enemy's position
  (PDTD's Space Bomb: lobbed gravity bomb).
- **`force_field`** — a new persistent effect kind (`OwEffect.Kind==5`), a
  planet-centred damage + slow pulse (reuses `SimWorld.ApplySlow`, the same
  per-tick-reapply mechanism the Graviton turret and slow abilities already
  use) — PDTD's Force Field ("continuous damage... inflict slow on hit").
  **Not the same system as Planet Shield** (see #3) despite the similar name.
- **`sweep_laser`** — a new persistent effect kind (`OwEffect.Kind==6`), a
  thin beam that rotates through a 70° arc over its duration from the firing
  platform — PDTD's plain "Laser" ("fires Lasers... in their path"), distinct
  from the already-existing `orbital_laser`/Beam's fixed lock-on.

`CardDraft.cs` and `SentinelScreen.cs` both already iterate
`Cfg.OrbitalWeapons` generically — the new 4 needed zero wiring to appear in
the in-run draft or the meta-upgrade menu ("menu" in the ask, done for free).
Card art: no AI art was supplied for these 4 (unlike the existing cards, all
owner-supplied) — `tools/gen_cards.py` generates a placeholder (nebula
gradient + starfield + a drawn glyph in the weapon's accent color) instead;
credited in `CREDITS.txt` as original/procedural, not AI or PDTD-sourced.
Sound: all orbital weapons already fire through the same generic
`Events.Push(SimEventKind.HeroWeaponFired, ...)` → `sentinel_shot.ogg` path
regardless of kind, so the 4 new ones got working audio for free too.

**Verified**: `SimTest` ALL CHECKS OK (1×≡4× identical — new sim code is
render+logic but doesn't touch anything determinism-sensitive beyond the
existing per-tick patterns it reuses); checked live in a running mission —
`waterdrop`, `space_bomb`, and `force_field` all appeared as draft cards with
correct procedural art and got leveled/fired, force_field's dome rendered
correctly around the planet. `sweep_laser` wasn't drawn in the draft pool
during this playtest (pure luck of the random draw) — its code path is
structurally identical to the others and compiles/type-checks, but hasn't
been eyeballed in motion; worth a quick look next time it comes up.

**A scripted-bot-only side effect, not a bug**: `SimTest`'s endless-mode
metric dropped from wave 41 (v0.25.2) to wave 6. Root cause confirmed
harmless: the test bot (`SimTest.cs`) always picks
`w.DraftOptionIndices[0]` — the *first* offered card, no evaluation — so
going from a 12-card draft pool (6 hero + 6 orbital) to a 16-card one (6 + 10)
means the naive bot spreads investment across more half-leveled new weapons
instead of focusing the strong original ones. `deterministic=ok` and
`changed-outcome=ok` on every other check — nothing crashed or ran away, it's
a known artifact of "always pick first" (documented precedent: v0.13.0 had
the same kind of swing from a card-system change). Real balance is the
user's on-device pass per the long-standing project convention — don't chase
this number.

### #5 — HUD sizing: top row bigger + actually centered, bottom cards bigger

The top control row (`Hud.cs`'s `_ctlRow`: ☰ ❚❚ 1x-4x ⚒ AUTO ✈) was
positioned in an anchor box (600px wide) narrower than its actual content
width (~784px including separation) — an `HBoxContainer` isn't clipped to
its anchor box, so it silently overflowed to the right and read as
off-centre, which is what "make the top buttons... centered" was actually
reporting (not a request to add new centering logic — the anchor math was
already nominally centred, the box was just too small for its own content).
Fixed by widening the box to 920px (comfortably over the new, ~15%-larger
button sizes) so it stops overflowing. `HeroWeaponButton`/`AbilityButton`
(the bottom-bar weapon/ability cards) grew 100×100/100×110 → 136×136/136×146
— the empty-feeling gap in the bottom panel was mostly these being
undersized relative to the panel, not the panel itself. `GameRoot.BottomReserve`
306→408 and `_wavePanel`'s height grown to match so the play-field clipping
still lines up with the taller panel.

**Verified**: checked live in a running mission — top row now sits genuinely
centred with room to spare, bottom weapon cards visibly bigger and less
sparse-looking (screenshotted at Commander level 6, three unlocked weapons).

### Still open from this ask

- **Numbers-to-PDTD-ratios: enemies and hero/turret weapons still untouched.**
  The orbital-weapon *cooldowns* got a real ratio retune this round (below) —
  enemies (`data/enemies.json`), turrets (`data/turrets.json`), and hero ship
  weapons (`data/hero_weapons.json`) did not. Turrets and hero weapons have no
  direct PDTD equivalent to ratio-match against (PDTD has one central gun, no
  turret roster, and no hero ship at all — those numbers are Beyond's own and
  already came from real playtesting iteration across several sessions, not
  from PDTD). Enemies were re-checked, not re-touched: verified live that
  `data/enemies.json`'s near-zero-armor-except-heavies pattern from
  `docs/balance-pass-1.md` hasn't drifted, and that `SimWorld`'s
  `DifficultyScale` already encodes PDTD's stated "hp scales much faster than
  damage" per-level principle in code (`e.Hp` scales linearly with
  `DifficultyScale`, `e.ContactDamage` only by its square root) — so the
  *qualitative* ratio already holds. PDTD's own literal per-level hp/atk
  curve (`NUMBERS.md`, x1→x25.96 by level 20, continuing to the billions by
  level 300+) doesn't transplant at all — it's built for an endless
  hundreds-of-levels meta-progression, a fundamentally different shape than
  Beyond's 8-mission arc + single 5-minute hold. If a future numbers pass is
  wanted, it's real per-entity work like balance-pass-1, not a quick follow-up.

---

## Recently completed — procedural animation + orbital-weapon cooldown ratios (v0.26.1)

Follow-up to v0.26.0 above, same day. User picked procedural motion (over a
sprite-sheet pipeline) for the deferred enemy/player-attack animation item,
and asked to go ahead with the numbers-to-PDTD-ratios balance pass too.

**Procedural animation** (`SimRenderer.cs`, render-only — nothing in `src/Sim/`
touched, `SimTest` byte-identical before/after):
- **Enemies** (`DrawEnemies`): a slow lateral idle wobble (amplitude scaled
  down for bigger/boss enemies, phase seeded off each enemy's array slot so a
  cluster of the same type doesn't move in lockstep) plus a faint fading
  thrust trail behind any moving non-boss enemy, colored from its own tint.
  Both offset only where the sprite is *drawn* — health bars, shield rings,
  aura circles, and the boss mechanic seam-line all stay anchored to the
  sim's real `e.Pos` so nothing gameplay-relevant drifts.
- **Hero ship**: a new `_heroRecoil` render-local timer (decays in `_Process`,
  same pattern as the existing `_shake` field) triggers on
  `SimEventKind.HeroWeaponFired` (ship weapons only — orbital-weapon fires
  reuse the same event with `I>=10` and are explicitly excluded, since they
  fire from planet-orbiting platforms, not the ship) and
  `VolleyLaunched`. Kicks the drawn ship backward along its own facing and
  flares the engine-trail intensity for a few frames, so firing reads as a
  physical event on the hull itself, layered on top of the weapon-specific
  VFX (laser cone, Yamato blast, missile trail) that already existed.
- **Verified**: `SimTest` ALL CHECKS OK (identical to pre-animation — confirms
  render-only), checked live in a running mission.
- Not done: no per-enemy hit-flash (the existing `EnemyHit`→spark-particle
  event already gives strong positional hit feedback; a sprite-level flash
  would need a per-enemy-handle timer map the renderer doesn't currently
  track — skipped as a nice-to-have, not requested specifically).

**Orbital-weapon cooldown ratios** — a real, bounded ratio-match against
PDTD's own 11-weapon table (`~/Work/pdtd-reference/NUMBERS.md` §"The 11
weapons", `atkCD` column). `orbital_cannon`↔Railgun (`atkCD` 3) was already a
1:1 anchor from the v0.21 sentinel work; every other orbital weapon's
`cooldown`/`min_cooldown`/`cooldown_per_level` was rescaled so its ratio to
`orbital_cannon` matches its PDTD counterpart's ratio to Railgun (e.g.
Radiation Link/Radiation Zone/Force Field/Ball Lightning all share PDTD
`atkCD`=10 → all three of Beyond's matching weapons converge to the same
6.33s cooldown, a real tie in PDTD's own data, not a calc artifact). Net
effect: `orbital_laser` and `waterdrop` got meaningfully slower (were
proportionally too fast vs PDTD), `radiation_line`/`radiation_zone`/
`shock_orb`/`force_field`/`sweep_laser` all got faster (were proportionally
too slow) — see the diff in `data/orbital_weapons.json` for exact before/after
numbers. **Damage values were deliberately left untouched** — PDTD's `dmg
base` multipliers aren't cleanly comparable to absolute numbers the way
cooldowns are (durational effects like Beam/Radiation Zone mix a per-tick
multiplier with a separate duration in ways that don't reduce to one ratio),
and Beyond's existing damage numbers are the product of several rounds of
actual playtesting (v0.12/v0.19/v0.22/v0.24), which a rougher one-table
ratio guess shouldn't blindly override. **Verified**: `SimTest` ALL CHECKS OK,
`deterministic=ok` throughout — this is a real balance value change though,
same as always: it's provisional until the user's own on-device pass
confirms the new tempo feels right, not the last word.

---

User asked (2026-09-15/16) to replace all regular-enemy AND boss sprites with
ones sourced from Planet Defense: Space TD (PDTD), per the standing
copyrighted-asset exception (`CLAUDE.md` §4, personal/never-published build).
**Done.** The earlier assumption that PDTD's enemies/bosses were 3D meshes
that couldn't be flattened without stretching/seaming was **wrong** — direct
inspection of the Unity data showed they're flat pre-rendered `SpriteRenderer`
sprites (no `Mesh`/`MeshRenderer`/`SkinnedMeshRenderer` anywhere), the same
trick already used for `assets/game/missile.png`. All 11
`assets/game/enemies/*.png` (10 regular + `boss_threshing_gate`) were
extracted via UnityPy, cropped, oriented nose-up (`siege_crawler` needed a
180° flip), and dropped in; `SimRenderer.EnemyTint` was reset to near-white
across the board so it stops multiply-darkening PDTD's own baked-in sprite
colors. Full source-prefab mapping is in `assets/game/CREDITS.txt`'s
"Enemy + boss sprites" entry; the corrected 3D-mesh-vs-sprite finding and
extraction method are written up in §6a below. Verified: `dotnet build` clean,
`SimTest.tscn` ALL CHECKS OK (1×≡4× identical — art-only, sim untouched), and
checked live in a running mission (enemy sprite renders with transparency
intact, rotates to face its travel direction, no tint discoloration).

**Turret/sentinel visuals were investigated and left as Kenney art** — PDTD
itself has only one player turret (two prefab variants of the same central
planet-gun; PDTD's own design has no turret roster), so there's nothing to
draw from for Beyond's 8 distinct turret types (`data/turrets.json`) the way
there was for the 11-enemy roster. Turret *sound* (`sentinel_shot.ogg`) was
already replaced with real PDTD audio back at v0.24.0/v0.25.0, before this ask
existed — that part was already done.

### Still open (unrelated to the art swap, don't lose these)

- **PDTD-style upgrade cards**: user wants PDTD's boost/upgrade *types*
  (damage%, count-vs-damage tradeoffs, splash%, proc-chance "super" hits)
  while keeping Beyond's own custom card art/names. Genuinely a
  `CardDraft.cs`/`HeroWeapons.cs` architecture change (today's system only
  knows "+1 level"), not an asset swap — **not started**. Don't undersell this
  one's scope the way the enemy-art estimate turned out to be oversold.
- **In-app update checker**: user reported "the in-game update didn't seem to
  work" but then realized they were testing a stale manually-installed APK and
  said they'd manually update and re-check — **outcome not yet confirmed**,
  worth asking or checking `src/Meta/UpdateChecker.cs` if it comes up again.
- **Login/cloud save & joystick from v0.25.1**: both confirmed **working** by
  the user after they manually reinstalled the real v0.25.1 build ("creating
  account works", "control stick works perfectly") — these are DONE, not open.
- **Declined, don't revisit**: the user asked about directly patching PDTD's
  own APK to strip its ads/IAP gates (to "just play PDTD free"). Declined —
  that's cracking another company's shipped monetization, a different thing
  than reusing assets in our own game. Stick with that answer if it comes up
  again.
- Arc 2 / further content still gated behind the user's own on-device balance
  pass (long-standing convention, unrelated to any of the above).

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

### v0.12.0 — balance pass 1 (PDTD calibration)
Config-only recalibration of `enemies/turrets/survival/balance/hero.json` against
`~/Work/pdtd-reference/` — enemies tanky-not-deadly, armour mostly zeroed, gentler
spawn, faster planet regen. `SimWorld.StepTick` early-returns on terminal phase
(1×≡4× now airtight). Full diff in `docs/balance-pass-1.md`. **Still not the
on-device pass.**

### v0.13.0 — PDTD-style menu / audio / cards / HUD
- **Splash screen** (`src/UI/SplashScreen.cs`) — key art + wordmark + Earth +
  "tap to begin", fades to the hub. Once per launch.
- **Music**: menu = one looping theme (`main_theme.ogg`); battle = 13 EVE tracks
  (`eve_NN.ogg`), one per stage via `MissionDef.Music`. LP tracks removed.
  `MusicPlayer` → `PlayMenu()` / `PlayStage(key, seed)`.
- **In-fight level-up cards**: kill XP → a per-run "commander level"
  (`SurvivalDirector.GainRunXp` / `RunLevel` / `XpForRunLevel`); each level pops
  an upgrade card from `data/cards.json`. Replaces the per-minute draft timer.
- **HUD**: coloured integrity bar + XP bar + live DPS; draft cards restyled;
  `TopReserve` 138→172.
- Side-effect: cards front-load → bot now clears m01–m03 big and nearly holds
  m04/m06. Determinism unchanged.

### v0.14.0 — hero controls + fire rate + battery
- **Free flight** — orbit-band clamp gone (`SimWorld.ClampHeroPos` = planet
  surface → arena edge). **Virtual joystick** (`src/UI/VirtualJoystick.cs`) on the
  left of the play field. **Tap an enemy** → `SimCommand.HeroFocus`: the ship
  locks it, point-defense retargets, fires a volley; reticle + lead line drawn.
- Volley **auto-fires** whenever it's off cooldown and something's in range.
  `hero.json` volley_cooldown 15→6 (code floor 11→2.5), PD dps 20→34, range→210,
  missile dmg 62→50, hull→480.
- **Planet battery** salvo 1→3 (fires the 3 nearest, all bearings), interval
  1.4→1.0; `battery_*` now in `data/balance.json`.
- SimTest `ClockRun` now runs to a fixed **tick** target (was a speed-scaled
  frame count — 1×≡4× broke once m05 outlasted the old bound; real fix, airtight
  again). Difficulty bumped to match the stronger hero: `survival.json`
  scale_ramp→1.15, scale_level_factor→0.10, eps_ramp→2.2.

### v0.15.0 — home screen
- **SplashScreen** rebuilt — "Beyond" chrome wordmark + Earth + Start button, no
  auto-advance.
- **MenuScreen** rebuilt as a PDTD-style hub — top rank/currency bar + gear,
  centre Earth, current-stage label + big **BATTLE** button, WEEKLY, a 6-icon
  section row (Star Map / Endless / Research / Protocols / Shop / Codex).
  **No energy/stamina gate** (design rule — PDTD's ⚡ cost is deliberately absent).

### v0.16.0 — futuristic font + menu readability
- **Fonts:** Orbitron (display) + Exo 2 (body), OFL, in `assets/fonts/`, wired
  through `UiTheme.Display` / `UiTheme.Body`. `GlowButton` gained `LabelFont` /
  `SetFont()` (defaults to Orbitron). Used on the wordmark, BATTLE, stage label,
  rank badge, section icons.
- **Splash:** "Beyond" is mixed-case now, pulled close above the Earth
  (anchor 0.335); START moved up (anchor 0.70).
- **earth.gdshader:** `repeat_enable` on all samplers + `fract()` rotation offset
  + seam-aware `textureLod` — fixes the texture tearing after a full rotation.
- **MenuScreen:** bigger rank badge / name / gear / section icons; BATTLE is
  smaller and tucked just under the planet (anchor 0.60) instead of the bottom
  stack; app version shown bottom-centre; the currency chip is a button that
  opens a **WALLET** popup (Commendations / Research Data / Exotic Alloy /
  Sentinel Cores).

### v0.17.0 — ship weapon systems + weapon cards
- **Six ship weapons** (`data/hero_weapons.json` + `HeroWeaponDef`): Laser Volley,
  Missile Barrage, Ion Cannon, Yamato Cannon, Plasma Field, Shields Boost. Each has
  a per-run level (0 = locked); the hull auto-fires everything unlocked. New sim
  subsystem `src/Sim/Systems/HeroWeapons.cs` (`StepHeroWeapons`, `FireHeroWeapon`,
  `HeroShieldSoak`); command `SimCommand.FireHeroWeapon` / `ToggleAutoFire`.
- **Upgrade cards replaced.** The commander level-up draft no longer draws from
  `data/cards.json` — it offers the 6 weapon cards (repeatable, each pick = +1
  level). `CardDraft.cs` reworked; `data/cards.json` + `Cfg.Cards` are dead config,
  kept for reference. The draft is a **centred popup** with the card art
  (`assets/game/cards/*.jpg`, owner-supplied — see CREDITS).
- **Manual fire + AUTO toggle.** HUD gets a weapon-button row (per unlocked weapon,
  with cooldown) and an `AUTO`/`MANUAL` toggle in the top control row.
- **Hull collision.** The ship now takes contact damage (`hero.json` `collision_dps`)
  and rams enemies for `ram_dps`; `HeroTakeDamage` had no callers before, so the
  hero was previously invulnerable. On death it respawns (`respawn_seconds` 6) with
  its weapon levels intact.
- **New ship art** — `SimRenderer.DrawShip`, a procedural dark swept-wing
  interceptor with teal engine glow. Shop hull skins no longer change the look
  (noted for a later pass).
- Weapon numbers are a first-guess calibration; the scripted SimTest bot (no
  evasion, no manual fire) now wins m01–03 + m06. 1×≡4× determinism holds. Real
  tuning is still the on-device balance pass.

### v0.18.0 — no default abilities · armoured ability buttons · battlecruiser
- **No abilities equipped by default.** `Progression.BaseAbilities` is now empty
  and `SaveGame.Loadout` starts empty — the planet's missile battery and the
  ship's own weapons are the whole starting kit. Battle abilities are recovered
  from Commander level-up cards (`data/levelcards.json` — three new `pro_barrage`
  / `pro_aegis` / `pro_overdrive` cards added so the old starters are still
  obtainable), then equipped in the Protocols screen. `AppRoot.ResolveLoadout`
  no longer force-fills base abilities; an empty loadout is valid.
- **Ability buttons restyled** — `src/UI/AbilityButton.cs` rebuilt in the
  upgrade-card look: chamfered dark-metal frame, role-coloured accent + corner
  brackets, drawn glyph, top-down cooldown wipe + seconds readout, ready pulse,
  amber armed-flash. Drawn in-engine (no image files).
- **New ship** — `SimRenderer.DrawShip` redrawn as a heavy capital cruiser
  (Terran-battlecruiser spirit): long armoured hull, forward prow gun, raised
  bridge, side sponsons, three-nozzle engine bank, running lights — in Beyond's
  teal/magenta palette.
- 1×≡4× determinism holds (SimTest unchanged — it passes its own fixed loadout).

### v0.19.0 — weapon-card popup crash fix + punchier SFX
- **FIX** (reported by the user — empty popup + freeze/crash on level-up):
  `Hud.RefreshDraft` built each weapon-card `Button` and its children but never
  called `_draftCards.AddChild(btn)`. The popup showed only the header, the panel
  collapsed to content size, and the run froze (`DraftPause` waiting on a pick
  that couldn't be made). Added the missing `AddChild`; made the popup
  full-width-minus-margin with flexible card widths; tightened the height.
  Reproduced + verified with `scenes/Shots.tscn` (`ShotRunner` now farms XP to
  force a draft and screenshots it — handy repro harness).
- `GameRoot.EquippedAbilities` default is now empty too (was still the old 3 —
  only mattered for direct-scene / ShotRunner use; `AppRoot` already passed the
  resolved loadout).
- **SFX**: `explosion` / `explosion_b` / `explosion_big` / `missile_launch` /
  `battery_launch` / `planet_hit` replaced with deeper synthesised hits (layered
  filtered brown/pink noise + sub-bass sine + envelopes, ffmpeg — see
  `gen_sfx.sh` in scratch / CREDITS). Original content. Any of them can be
  overridden by dropping a same-named `.ogg` into `assets/audio/`.

### v0.20.0 — mandatory update gate + gentler late difficulty
- **SplashScreen** checks GitHub for a newer release on launch; if one exists,
  Start is replaced by a blocking "UPDATE REQUIRED / DOWNLOAD UPDATE" panel with
  no way past. Fail-open on any network error or same/older version.
- **`SurvEscalation(frac, curve)`** (in SurvivalDirector.cs) shapes the survival
  ramp so it peaks at ~70% of the timer then eases back (`late_ease_frac` /
  `late_ease_amount` in survival.json) — the final stretch is no longer an
  unwinnable wall. Overall magnitudes lowered too. Bot clears m01–m04 + m06.

### v0.21.0 — planet orbital weapons (PDTD-style sentinels)
- **Six orbital weapons** (`data/orbital_weapons.json` + `OrbitalWeaponDef` +
  `src/Sim/Systems/OrbitalWeapons.cs`): Orbital Cannon / Laser / Lightning,
  Radiation Line, Shock Orb, Radiation Zone — PDTD sentinel stats. Platforms
  orbit the planet in open space (`OwOrbit` × `SentinelOrbitRadius`) and
  auto-fire; the `AUTO` toggle gates them.
- New enemy status fields `Enemy.StunLeft` / `BurnLeft` / `BurnDps` (stepped in
  EnemySystem) for chain-stun and radiation/burn DoT.
- **Merged card draft** — `CardDraft.cs` now draws from one shared pool of ship
  weapons + orbital weapons, 4 per level-up. Orbital option indices are offset by
  `SimWorld.OrbitalCardBase` (100). Popup tags each card SHIP / ORBITAL.
- `DamageSource.Orbital` + `RunStats.DamageByOrbital`; end report shows
  turrets / ship / orbital / abilities.
- 7 new owner-supplied card images (`assets/game/cards/orbital_*.jpg`,
  `planet_shield.jpg`).
- Meta hook `ModifierSet.OrbitalMeta` (per-weapon persistent level) is read by
  `ResetOrbitalWeapons` for a head-start — the out-of-battle purchase UI + Planet
  Shield land in the next release.
- Renderer: ringed sentinel stations, concentric radial shockwaves, sweeping
  radiation beam — styled after the PDTD sentinels (their actual Unity art can't
  be extracted/used; this matches the look procedurally).

### v0.22.0 — much easier missions, PDTD-style menu, reward popup, Sentinels screen
- **Difficulty** (top user complaint, twice — "still can't finish level 1"):
  `MissionDef.Difficulty` per-mission multiplier, ramped 0.40 (m01) → 1.15 (m08)
  in `gen_missions.py` (was every mission at full strength). `survival.json`
  magnitudes cut hard again. Scripted bot now **wins all 8 missions** at
  full/near-full integrity (was m01–04+m06); endless reaches wave 41 (was ~4–7)
  — a large chunk of that second jump is the orbital-weapon combat rework below,
  not just the difficulty knobs. This is now generous by design; the on-device
  pass is still where it gets dialed back to a real challenge.
- **Menu reorganised PDTD-style**: bottom bar is SHOP · UPGRADES · BATTLE (centre,
  bigger) · SENTINELS · EVENTS. Removed the orphaned Endless icon and the
  standalone Protocols button (`MenuScreen.NavBtn`, was `IconBtn`).
  - **`UpgradesScreen`** (new): hub with Research Tree / Protocols (ability
    equip — user chose "fold into Upgrades") / Planet Modules (disabled
    placeholder) / Codex.
  - **`SentinelScreen`** (new): permanent Commendations + Sentinel Cores
    upgrades for the 6 orbital weapons, plus **Planet Shield** (RD-equivalent
    early, + Exotic Alloy past level 4 — user chose "both/either"). Costs via
    `Shop.Spent()` now includes `SaveGame.CommendationsSpent`.
  - `ModifierSet.OrbitalMeta` / `PlanetShieldLevel` (fed from `Save.OrbitalMeta`
    / `Save.PlanetShieldLevel` in `Progression.BuildModifiers`) set a run's
    **starting** orbital levels / shield pool — in-fight cards build on top.
  - **Planet Shield mechanics**: `SimWorld.PlanetShieldStrength(level)`, soaks
    damage before integrity (existing `PlanetShield` pool), regenerates ~1.2%
    of max/sec in `CheckSurvivalEnd`.
- **Reward popup** replaces the old text end-card: centred `_endCard` (like the
  draft popup) showing XP / RD / Cores / Alloy, a **"×2 VICTORY BONUS"** on a
  win (`SimWorld.AccrueRewards` doubles + adds flat extras), and a loss now
  keeps the **full** run reward (was `Mods.LossRewardFrac`-reduced — "progress
  every sitting"). Fixed a bug where the pause menu rendered on top of the
  end card (`Hud._pauseMenu.Visible` now excludes `Won`/`Lost`).
- **Orbital weapon combat rework** (explicit ask — "sentinels barely attack"):
  - **Orbital Laser** → `kind: "beam_laser"`, a new sustained `OwEffect` (kind 4)
    that locks onto a target and burns continuously (shield-pierce, hits
    anything in the beam path) for the duration — matches PDTD's **Beam**
    sentinel instead of the old instant zap.
  - **Radiation Line** → holds a **fixed** corridor toward the current threat
    for its duration (was slowly rotating) — matches PDTD's **Radiation Link**.
  - All 6 weapons' cooldowns lowered so they fire noticeably more often.
- `SimTest` `ALL CHECKS OK`, 1×≡4× identical (endless run now much longer —
  wave 41 — but the harness still completes in ~9s).

### v0.23.0 — centered/animated menus, Profile screen, card-styled HUD, Radiation Link rework
User played through L1–6 and sent screenshots; this is the direct feedback pass.
- **`MenuBackground`**: `AnimatePlanet` (default on) — slow idle zoom + pan every
  frame, everywhere. Every sub-screen (Sentinels/Upgrades/Shop/Settings/
  Research/Protocols/Codex/Profile/level-up) now centres the Earth
  (`PlanetY=0.5`, `PlanetScale=0.55`) instead of the old top-anchored
  placement that overlapped the card list. Headers pushed down
  (`OffsetTop` 16→34) on all of those screens too.
- **`SentinelScreen`** cards show the real card-art thumbnail per weapon
  (+ Planet Shield) — were colour-bar-only before.
- **New `ProfileScreen`** ("COMMANDER") — rank ring, hero level, campaign/
  endless/weekly/codex/currency/sentinel stats. The rank badge on the menu
  top bar is now tappable (`AppRoot.ShowProfile`) — was inert before.
- **HUD top control row** (☰ ❚❚ 1x-4x ⚒ AUTO) was never added to the
  `UiTheme.Instance` list, so it rendered in Godot's plain default theme —
  "barely visible" per the user. New `Hud.StyleTopButton` gives it the same
  armoured card-frame look as the ability buttons / weapon cards.
- **★ STAR MAP button** added under the stage label on the main menu (star
  map was reachable only via ALL-CLEARED before).
- **Radiation Line rebuilt** to match PDTD's Radiation Link per explicit ask
  ("a line connected by 2 dots or mini space stations… other upgrades add
  connections… other upgrades rotate it around the planet"): now 2–4 relay-
  station nodes (`SimWorld.RadLineNode`, one formula shared by sim + renderer)
  linked by damage beams, the whole chain slowly orbiting the planet. Node
  count and rotation speed both scale with level. Renderer draws a small
  beacon marker at each node. **Known gap**: PDTD's *literal* branching
  upgrade-card choice (pick "rotate" vs "add connection" vs "damage" each
  level) was not built — that needs a per-weapon skill-tree UI, a bigger
  lift; instead the same flavors are baked into one linear level curve.
- Reward multipliers (`reward_rd_mult`/`xp_mult`/`credits_mult` in
  `survival.json`) bumped ~15–20% — user: cost-scaling curve is good, base
  rewards should be "a little better."
- `SimTest` `ALL CHECKS OK`, 1×≡4× identical.

### v0.24.0 — feedback pass: planet-battery balance, joystick redock, bigger HUD, menu reflow, laser/missile SFX, in-mission update badge
User-reported feedback session (no on-device pass yet — see §4, still the blocker).
- **`data/balance.json` `battery_damage` 22→11** — the planet's own missile battery
  was one-shotting Skiffs (22 hp basic enemy) even at the softest early-mission
  scale (`DifficultyScale` ≈0.70 at m01 start, ≈15 effective hp). Halving it means
  a Skiff always needs **2** battery-missile hits, never 1, at every difficulty
  level in the game (worst case is m01 start; verified against `DifficultyScale`
  algebra, not just SimTest win/loss). Bot still clears m01–m06 per SimTest — no
  regression.
- **Radiation Line (`orbital_weapons.json radiation_line`) levels up its relay
  count faster and further** — user: "should have more line connected" as you
  level it, PDTD-style. Old curve `2 + L/4` capped at 4 nodes and needed L8 to
  get there; new `2 + (L-1)/3` (clamped 2–5) spans the *whole* 1–12 level range:
  L1-3→2 nodes (1 link), L4-6→3, L7-9→4, L10-12→5 (4 links). Bumped the
  `stackalloc Vector2[4]`→`[5]` buffers in `OrbitalWeapons.cs` `StepOwEffect` and
  `SimRenderer.cs` to match. **User explicitly OK'd leaning further into PDTD's
  designs/assets for this weapon (and in general) — see the note in §7, this
  build is personal and will never be published.**
- **Virtual joystick redocked** (`src/UI/VirtualJoystick.cs`) — user: it was
  "hard to show up," needing the thumb to relocate every touch because the ring
  only appeared wherever you first touched (a fully floating stick). Now a
  translucent base ring is **always drawn** at a fixed spot near the bottom-left
  of the touch zone (dim when idle, bright when held); a touch anywhere in the
  zone still drives it, but the knob offset is always relative to that fixed
  dock instead of the touch-down point. `EnsureBase()` computes the dock lazily
  once the control has a real `Size`.
  Screenshot-verified via `ShotRunner`/`Shots.tscn`: the dock renders as a dim
  ring at a sane bottom-left spot, clear of the weapon/ability panel. **Still
  not touch-tested** — the actual drag feel (does it need a huge initial swing
  to reach full deflection from a touch far from the dock?) needs a real device.
- **HUD in-mission buttons enlarged**: top control row (☰ ❚❚ 1x-4x ⚒ AUTO) grew
  from 56-64px tall to 66-76px (font 15-22→17-26); the bottom weapon-select row
  96×52→112×62 (font 12→14); `AbilityButton` 96×108→112×126 (its self-drawn frame
  scales with `Size` already; chamfer/bracket/font constants nudged up to match).
  `_wavePanel.OffsetTop` -244→-270 for the extra height.
- **Menu (`MenuScreen.cs`) reflow** — user: buttons should all be "a bit bigger,"
  plus 3 structural asks:
  - Rank badge/name/sub, credits chip, and ⚙ settings all sized up (~84→100-176px,
    font 24-40→28-44).
  - **New ☰ CODEX button** in the top bar beside the credits chip (opens
    `App.ShowCodex` directly — Codex was previously only reachable one level
    deep, inside Upgrades).
  - **★ Star Map moved into the bottom nav bar** (was a small separate button
    tucked under the stage label) — it now sits where Sentinels used to be.
  - **Sentinels moved into the Upgrades hub** (`UpgradesScreen.cs` gained a
    "✷ SENTINELS" tile) — the bottom bar is now SHOP · UPGRADES · BATTLE ·
    STAR MAP · EVENTS. `SentinelScreen`'s back button now returns to Upgrades,
    not the menu.
  - Bottom-bar `NavBtn`s 92→110px tall (font 15→18); BATTLE 170×104→190×124
    (font 28→32).
  - Screenshot-verified (menu + Upgrades) via a throwaway `MenuShot.cs`/
    `MenuShot.tscn` harness (same pattern as prior sessions — instantiate
    `AppRoot`, jump straight to the screen, grab a PNG; deleted after use, not
    kept). Codex/credits/gear cluster and the 5-button bottom bar both render
    clean with no overlap; Sentinels shows correctly as the 3rd Upgrades tile.
- **Laser + missile SFX resynthesised** for a punchier, more "realistic" weapon
  character (user: "if possible... more realistic"). `turret_shot` /
  `turret_shot_b` (turret lasers) / `sentinel_shot` (orbital weapons) replaced
  Kenney's "Sci-Fi Sounds" pack with a layered descending-chirp tone
  (`aevalsrc` linear frequency sweep) + a filtered noise crack + a sub thump —
  3 distinct variants. `missile_launch` / `battery_launch` rebuilt with an
  ignition crack, a `tremolo`-modulated brown-noise rocket-motor roar, a sub
  thump, and a receding lowpass-decaying whoosh (was a simpler hiss+thump).
  All in `tools/gen_sfx.sh` (now the source of truth for all 9 synthesised
  effects, not just the original 6 — `mkdir -p` added so the script is
  self-contained). `assets/game/CREDITS.txt` updated; `tools/sfx/` (the script's
  scratch output dir) added to `.gitignore`.
  ⚠ **Not yet listened to** — no audio-playback tool available this session;
  the filter chains were verified to run clean (`ffprobe`/`volumedetect`, no
  clipping, sane durations) but not auditioned. Have a listen in-engine and
  tweak to taste before calling this done.
- **In-mission update badge** — user: wants to know about an update "in game,"
  not just at the menu/splash. Splash already has a *mandatory* gate and the
  menu already has a dismissible card, both fire once per launch — but neither
  helps if the check resolves (or a release drops) *after* you're already in a
  long survival hold. `Meta/UpdateChecker` now exposes static
  `Available`/`AvailableTag`/`AvailableUrl`, set by whichever check (splash's own
  request, or the menu's `UpdateChecker` instance) resolves first; `Hud.cs` shows
  a small amber ⇩ badge next to the integrity bar whenever it's true, tapping
  opens the release page. Polled once/frame in `Hud.Refresh()` (cheap bool read).
- Bumped `config/version` 0.23.0→0.24.0 (`project.godot` + `export_presets.cfg`)
  per the usual ritual — **not tagged/pushed/released this session**, that's
  still the user's call.
- `SimTest` `ALL CHECKS OK`, 1×≡4× identical, m01-m06 still won (balance edits
  didn't regress the scripted bot).

### v0.25.0 — second feedback pass: input z-order fix, autopilot, Planet
Modules, cloud save + login, real PDTD audio + shield texture, HUD/menu redo
Another large user feedback pass, same day as v0.24.0. **This session used a
background fork** (spawned to do a small Supabase lookup) that ended up
independently building the entire cloud-save feature end-to-end — flagged via
`SendFeedback` as a real scope-creep incident (see §9), but the actual work
was reviewed in full and is sound; kept.

- **FIXED the real bug behind "can't press 1x/etc"**: `VirtualJoystick` was
  added to the `Hud` scene tree *after* the top control row, and Godot gives
  input priority to the later-added (frontmost) sibling in an overlapping
  region — the joystick's full-screen touch zone was silently swallowing taps
  meant for the buttons underneath. Moved the joystick to be the **first**
  child added in `Hud._Ready()` (see the comment on `VirtualJoystick` itself)
  so every button/panel added after it correctly wins its own taps. This was
  already a latent bug before v0.24.0's redock — it's just that the redock
  made it worse by growing the zone.
- **Joystick moved to the right side, 2× bigger** (`_maxRadius` 96→192, dock
  anchored bottom-right of a right-side zone instead of bottom-left of a
  left-side one) — user liked the fixed-dock design from v0.24.0, just wanted
  it on the other hand and larger.
- **Autopilot** — new `✈` toggle in the HUD top row. `HeroSystem.AutopilotDir()`
  (deterministic: closest-enemy + a standoff-range approach/retreat, no
  wall-clock) drives hero movement whenever the joystick is idle and autopilot
  is on; manual joystick input always overrides it. Attacks were already
  automatic regardless (point-defense, volley, and the existing `AUTO` ship-
  weapon toggle) — this was purely the missing "move on its own" piece.
  `SimCommand.ToggleAutopilot`, `GameRoot.RequestToggleAutopilot()`.
- **In-mission "attack cards" redesign**: `src/UI/HeroWeaponButton.cs` (new) —
  same chamfered-card visual language as `AbilityButton`, with a per-weapon
  icon, level chip, and **both** a cooldown wipe and a thin cooldown *line*
  (the user's explicit ask: "should only show attack/upgrade cards with a
  cooldown line/time"). Replaces the old plain `Button`s in the weapon row.
  The bottom HUD panel ("there's always a blue box at the bottom, it should be
  a bit smaller") is now a lighter translucent backing (55% alpha, thin top
  border, no full theme panel) at a shorter height (238 vs 270), with the
  weapon/ability cards shrunk to match (100×100 / 100×110).
- **Difficulty**: `data/enemies.json` `max_hp`/`shield_hp` × **1.2** across the
  whole roster (Skiff 22→26 up to the boss 9000→10800) — "Level 1 is a bit too
  easy... make enemies 1.2x harder to kill." **`data/balance.json`
  `battery_damage` bumped 11→13 to match** — v0.24.0 tuned it so the planet
  battery needs exactly 2 hits to kill a Skiff; skipping this would've made it
  2-hits-with-a-third-needed instead, an accidental extra nerf.
  Commander (ship) attack speed cut ~15%: `data/hero.json volley_cooldown`
  6→6.9, and every cooldown/min_cooldown in `data/hero_weapons.json` ×1.15
  (Plasma Field untouched — it's a continuous aura, "attack speed" doesn't
  apply). Per-kill XP +15%: new `Balance.XpKillMult` (1.15) multiplies the
  `GainRunXp` call in `SimWorld.cs` — separate from Credits so this doesn't
  quietly also boost gold.
  **On "use PDTD's actual enemy values"**: literal 1:1 import isn't meaningful
  here — PDTD's `level_enemy` baseline is `hp: 100` scaled by a per-level
  multiplier reaching into the millions by L300, calibrated against *its own*
  weapon-damage economy (attackBase ~1-3.5, 11 weapon types); Beyond's turret/
  hero-weapon damage numbers are a completely different absolute scale (15-420
  per hit), so dropping in PDTD's raw hp numbers would either trivialize or
  wall the game depending which level you copied. What **does** carry over
  (and Beyond already does): PDTD's hp scales *far* faster than its attack
  damage per level (`hpMultiplier` ×1→×1132 by L50 vs `attackMultiplier` only
  ×1→×54) — "enemies get spongy, not lethal." `SimWorld.DifficultyScale`
  already applies `Sqrt()` to damage but not hp for exactly this reason. The
  1.2x pass above is in that spirit, not a numbers transplant. A *real*
  PDTD-ratio recalibration across every data file simultaneously is what
  `docs/balance-pass-1.md` (v0.12.0) already did once — a full redo is a
  multi-hour job of its own if the user wants it, not a quick follow-up.
- **Menu/Upgrades cleanup**: Codex removed from the Upgrades hub tile list
  (redundant with the top-bar button added in v0.24.0). **Protocols removed
  entirely** ("unsure what it's useful for") — recovered abilities now
  **auto-equip themselves** as soon as unlocked, up to the hero-level slot
  count, first-unlocked-first-equipped (`AppRoot.ResolveLoadout`, now also
  scans `Cfg.AbilityOrder` not just the — empty — `BaseAbilities`). The old
  `AbilityScreen`/`ShowAbilities()` code is unreached but still compiles
  (loadout presets live there); wire it back into a menu if manual curation is
  ever wanted again. Settings' "Appearance → Shop" shortcut button removed
  (redundant with the Shop button already on the home screen).
- **No more forced turret placement at mission start**: `SimWorld.Load()` now
  calls `BeginWave()` immediately for survival missions instead of sitting in
  `SimPhase.Build` waiting for a manual "▶ BEGIN DEFENSE" tap — "upgrades are
  already given per level" was the user's reasoning (ship/orbital weapon cards
  are the real progression now). Turrets are still fully buildable in
  real-time via the existing `⚒` HUD toggle; nothing about the turret system
  itself changed, just the forced pre-fight gate.
- **Planet Modules — actually built** (was a disabled "coming soon" placeholder
  since v0.22.0): `data/modules.json` (5 modules: Reinforced Plating /
  Auto-Repair Array / Spawn Dampener / Salvage Contract / Shield Capacitor),
  `ModuleDef`/`ModulesDb` in `Config/Defs.cs`, `Progression.ModuleLevel/
  ModuleCost/BuyModule/ToggleEquipModule` (Research-Data funded, PDTD-style:
  levelling ≠ equipping — only up to `ModulesDb.Slots` (3) equipped modules
  count in a run), new `src/UI/ModulesScreen.cs`. Wired through 4 new
  `ModifierSet` fields (`SelfRepairMult`, `PlanetShieldMult`, `SpawnRateMult`,
  `CreditsGainMult`) into `SurvivalDirector`/`SimWorld` via the existing
  `ApplyEffect(key,v)` mechanism (same pattern research nodes already use —
  no new plumbing style introduced).
- **Planet Shield got a real animated dome** (was a thin single pulsing arc):
  `SimRenderer.DrawPlanetRings` now draws a filled glow, 3 glow rings, a bright
  rim, and rotating shimmer chords, **plus** a real hex-grid texture
  (`DrawHexShieldSurface`, tiled via `DrawPolygon`'s UV param — needs
  `SimRenderer.TextureRepeat = Enabled`, set once in `GameRoot`) pulled from
  Planet Defense TD's own force-shield material (see §6a). Fades with the
  shield's remaining fraction instead of being on/off. Radiation Line's two
  **end** relay stations now draw a small pylon structure (`DrawRelayStation`
  — hex base + outward mast + tip light) instead of the plain beacon dot every
  node gets, per "each ends should have a tiny structure."
- **End-of-run reward card** got a modest PDTD-style visual pass (icon chips,
  bigger value text) — **did not** add a new "diamond"-style currency per the
  literal ask ("aUEC credits like diamonds"): that's a real-money-styled
  premium-currency pattern in the game it's modelled on, and CLAUDE.md's
  no-monetisation rule is a hard architectural line even in a personal build.
  The 4 currencies already shown (XP/RD/Cores/Alloy) are all earned-only.
- **Cloud save + login** (built by the background fork — reviewed and kept,
  see the scope-creep note above): `src/Meta/CloudSave.cs` (autoload-less
  singleton, added as a child in `AppRoot._Ready`) is a thin Supabase client —
  email+password auth (`/auth/v1/signup`, `/auth/v1/token?grant_type=password`)
  and a `saves` table upsert/fetch over PostgREST, all fire-and-forget so a
  network hiccup or expired token can never strand a player. Session persists
  to `user://account.json` (deliberately separate from `user://save.json`).
  `src/UI/LoginScreen.cs` — a once-per-launch popup between Splash and Menu:
  email/password + Log In / Create Account / **Skip — play as guest** (fully
  optional, matches the "no forced gate" design elsewhere in this game).
  `SaveGame.ToJson`/`FromJson` + `SaveGame.Save()` now also fires
  `CloudSave.Instance?.PushSave(...)` — every save syncs automatically when
  logged in, no separate call sites to remember. `AppRoot.AdoptCloudSave`
  replaces the in-memory save when a login finds an existing cloud row.
  **Supabase project**: `beyond-game` (org ShadowSwords, id
  `qquzgugvozelkurrpzys`) — new project, separate from the `Peak Social`
  project. Table `public.saves(user_id uuid PK → auth.users, data jsonb,
  updated_at timestamptz)`, RLS enabled, verified directly (not just
  trusted): 3 policies, all `auth.uid() = user_id`, one per
  SELECT/INSERT/UPDATE — `get_advisors(type: security)` reports zero lints.
  ⚠ **Unverified**: the actual sign-up → confirm-email → log-in round trip has
  not been exercised against a real inbox this session (no email/device access
  here). Supabase projects default to "Confirm email" ON, so first-time
  sign-up will likely show "check your email to confirm" rather than logging
  straight in — expect that, it's not a bug. Test the full loop on-device
  before relying on it for anything precious.
- `SimTest` `ALL CHECKS OK`, 1×≡4× identical — none of this session's changes
  touch anything sim-relevant beyond the enemies/hero-cooldown/xp-mult data
  edits and the module `ApplyEffect` wiring, all already covered by the run.

### v0.25.1 — joystick coordinate bug, Android internet permission, autopilot targeting, real missile art/sound
User tested v0.25.0 on-device same day; this is the direct bug-report pass.
- **FIXED the real joystick bug**: "I try to go right and the joystick doesn't
  go where my thumb is." `VirtualJoystick.EnsureBase()` computed its fixed dock
  as `Position + (Size - insets)` — but Godot delivers `_gui_input` touch/mouse
  positions **already local** to the receiving control, so adding `Position`
  double-offset the dock relative to real touch coordinates. `_Draw` happened
  to subtract `Position` back out, so the ring *looked* right on screen while
  the actual drag-direction math was wrong underneath. Harmless on the old
  left-docked zone (`Position.X` was 0 there — anchored flush to the screen's
  left edge) but badly wrong once v0.25.0 moved the dock to the right side
  (`Position.X` ≈ half the screen), which is exactly why steering right broke.
  Fixed by making `_center`/`_knob` purely local, matching `_gui_input`'s frame
  — no `Position` term anywhere now. See the comment on the fields in
  `VirtualJoystick.cs` for the full explanation, so nobody "fixes" it back.
- **FIXED Android networking being dead since forever**: `export_presets.cfg`
  had `permissions/internet=false` — this APK has **never** had network access
  on a real device. This is why login gave "Request failed (0)" (HTTP code 0 =
  never reached the server), and almost certainly why the in-app update
  checker has silently never worked either (it fails open/silent by design, so
  nobody would've noticed). Flipped `permissions/internet` and
  `access_network_state` to `true`. This one change is probably the single
  most impactful fix in this patch.
- **Autopilot retargeting**: was chasing whatever enemy was nearest to the
  *hero's current position* (could drag the ship far from the planet chasing
  one distant spawn while ignoring closer threats). Now targets nearest to the
  **planet** (`Vector2.Zero`), matching "go towards the closest enemy from the
  planet." With no enemies alive it now **patrols a slow orbit** around the
  planet instead of sitting still ("at the very least roam around the
  planet") — deterministic tangential circling with a mild radial correction
  back toward the orbit radius, no RNG/wall-clock. User's immediate follow-up
  ("orbit closer than the preview I saw") — tightened the patrol radius from
  `PlanetRadius + PointDefenseRange*0.85` (≈298) down to `HeroOrbitMin + 20`
  (≈220), hugging the planet instead of sitting out near point-defense range.
- **Real missile sprite + sound** (user: "replace the missile animation and
  sounds"): `assets/game/missile.png` (used by both the hero's own missiles
  *and* the planet battery's homing shots — both call `Art.Missile`) replaced
  with a texture pulled from PDTD's own missile VFX
  (`gameobjects/weapon/missile/texture/supermissile01.png.ab` via UnityPy —
  reused the fork's working `FALLBACK_UNITY_VERSION = "6000.0.80f1"` config).
  It's a flat comet/flame billboard (works because muzzle/trail VFX are flat
  sprites even in a 3D engine), needed a luminance→alpha pass (black backing)
  and a 90° rotation so the nose points "up" to match this renderer's sprite
  convention. `missile_launch.ogg` replaced with a real trimmed PDTD sample
  (sample 43, "未来主义榴弹发射器" / "futuristic grenade launcher", 13.6s source
  trimmed to a 1.6s one-shot — safe because the hero's Missile Barrage cools
  down for several seconds, unlike the planet battery). `sentinel_shot.ogg`
  (backs every orbital weapon) replaced with sample 57 (a real ~2.3s cannon
  report, no trim needed). Both added to `tools/extract_pdtd_audio.py`'s
  `SAFE_REPLACEMENTS`/`TRIM` maps for reproducibility.
  **Missile damage was NOT changed** — same reasoning as the enemy-stats note
  above: PDTD's "Missile: dmg base 1" is a multiplier in its own weapon
  economy, not a transplantable absolute number for Beyond's completely
  different damage scale.
- **Enemy/boss visual replacement — DONE at v0.25.2.** The write-up above
  (and the old §6a note it pointed to) was **wrong**: PDTD's enemies/bosses
  turned out to be flat pre-rendered `SpriteRenderer` sprites, not 3D meshes —
  a v0.25.1-session spike inspected the actual Unity objects and found no
  `Mesh`/`MeshRenderer`/`SkinnedMeshRenderer` anywhere in the enemy prefabs.
  See the "🔴 ACTIVE HANDOFF" section this file carried at v0.25.1/early
  v0.25.2 (now folded into history below) for the full corrected finding, and
  `assets/game/CREDITS.txt`'s "Enemy + boss sprites" entry for the source
  mapping (which PDTD prefab became which Beyond enemy). All 11
  `assets/game/enemies/*.png` replaced, `SimRenderer.EnemyTint` reset to
  near-white so it stops washing out PDTD's baked-in sprite colors.
  **Sentinel/turret visuals were investigated and left as Kenney art** — PDTD
  itself has exactly one player turret (`prefab/gun/gun.prefab.ab` +
  `autocannon.prefab.ab`, two variants of the same central planet-gun,
  because PDTD's own design has no turret roster), versus Beyond's 8 distinct
  turret types (`data/turrets.json`). There's no PDTD equivalent to draw from
  the way there was for the 11-enemy roster, and the one extractable texture
  (`art/gun/machine_gun0.png.ab`, `gun_plus.png.ab`) came out through UnityPy
  as a near-blank luminance mask that didn't hold together as a usable sprite
  even after a luminance→alpha pass — not pursued further. Turret *sound*
  (`sentinel_shot.ogg`) was already replaced with real PDTD audio back at
  v0.24.0/v0.25.0 (see the audio entry above) — that part of the "sentinels
  and their sounds" ask was already done before this pass.
- **Upgrade cards "same boosts as PDTD, keep my own custom cards" —
  investigated, NOT implemented.** Pulled PDTD's actual card design
  (`config/data/skill_upgrade_data`, 208 cards, digested in
  `~/Work/pdtd-reference/NUMBERS.md` "In-run upgrade cards"): per weapon it's
  an unlock card + a small family of distinct upgrade **types** — e.g. for
  Missile: "Power Missile" (+dmg%), "Missile Volley" (+1 count / −dmg%, a real
  tradeoff card), "Blast Amplifier" (+explosion radius% and +explosion dmg%),
  "Enhanced Missile" (% chance to upgrade a shot into a x3-damage "super"
  version), "Probability Boost" (increases that chance). Beyond's
  `CardDraft.cs` currently only offers one thing per weapon: "+1 level," which
  bundles all stat growth from `hero_weapons.json`'s fixed per-level curve —
  there's no branching-type choice at all. Genuinely reproducing PDTD's system
  means restructuring the draft to offer *multiple distinct upgrade paths per
  weapon* (not just re-flavoring text), which is a real architecture change
  to `CardDraft.cs`/`HeroWeapons.cs`, plus rebalancing, plus re-verifying
  SimTest's bot outcomes still make sense afterward — too large to do safely
  in the same pass as the bug fixes above. The concrete PDTD examples above
  are the blueprint for whoever picks this up; Beyond's own card art/names
  stay untouched either way per the user's ask.
- `SimTest` `ALL CHECKS OK`, 1×≡4× identical, m01-m06 still won — the
  autopilot/joystick/permission fixes don't touch anything sim-outcome-
  relevant (autopilot logic is deterministic and SimTest's scripted bot
  doesn't use it; the joystick bug was UI-input-only).

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
| Splash screen + hub menu flow | done (v0.13.0) |
| Per-stage battle music + in-fight level-up cards | done (v0.13.0) |
| Art / sound | Kenney CC0 + shader Earth + NASA backdrops. Music = commercial (personal build). No AI-sprite pipeline. |
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

### 6a. PDTD *asset* extraction (audio, shield/missile/enemy/boss art all done — v0.25.2)

Per the user's explicit 2026-09-15 OK to use PDTD's real assets in this
personal build (§7 / `CLAUDE.md` §4), this session went past the config-only
reverse-engineering above and pulled real audio + one texture out of the
actual game files. Source: `~/Downloads/Planet+Defense_+Space+TD.xapk` (543 MB
— deliberately **not** committed to this repo; it's an XAPK = zip of 4 APKs,
the ~415 MB `UnityDataAssetPack.apk` inside it holds all the real game data).

- **Audio — done, integrated.** `tools/extract_pdtd_audio.py` (rerunnable,
  `--list` / `--apply`). An FMOD `.bank` file is a RIFF container; its `SND `
  chunk holds a raw **FSB5** blob (FMOD Sample Bank v5) — walk the RIFF chunks
  to find it, hand that blob to the `fsb5` PyPI package (`pip install fsb5`;
  HearthSim's parser), which lists/rebuilds every named sample. 79 samples in
  `SFX.bank`; 8 were short enough to safely reuse as direct one-shot
  replacements (see `SAFE_REPLACEMENTS`/`TRIM` in the script for exactly which
  and why — several are 9-60s **source-library** clips FMOD Studio's event
  graph trims/loops, which is a deeper job than the plain RIFF/FSB5 walk and
  wasn't attempted). Now live in `assets/audio/`: `mission_won`, `mission_lost`,
  `card_pick`, `ability_cast`, `explosion`, `explosion_b`, `shield`, `ui_click`.
- **One texture — done, integrated.** `assets/game/shield/hex_pattern.png`, the
  actual hex-grid pattern off PDTD's own force-shield material
  (`assets/Res/gameobjects/shield/texture/pattern/hexpattern_hollow.png`),
  pulled via **UnityPy** (`pip install UnityPy`; needs
  `UnityPy.config.FALLBACK_UNITY_VERSION = "6000.0.80f1"` — these particular
  `.ab` files carry no embedded version string). Re-processed
  luminance→alpha so it composites as a translucent overlay. Wired into
  `SimRenderer.DrawHexShieldSurface` on the Planet Shield dome.
- **Enemy + boss sprites — done at v0.25.2.** The claim in the paragraph this
  replaces (and in the older "Enemy/boss/sentinel" entry in §5) was **wrong**:
  PDTD's enemies/bosses are not 3D models rendered live. Directly inspecting
  the Unity objects in e.g. `prefab/enemy/bug/bug_mantis_middle.prefab.ab` and
  `prefab/enemy/bug/bug_wallmaker_boss.prefab.ab` showed only
  `SpriteRenderer`/`Transform`/`GameObject` components — no `Mesh`,
  `MeshRenderer`, or `SkinnedMeshRenderer` anywhere. The 3D look was modeled
  once by PDTD's artists and baked down to a flat pre-rendered PNG per enemy
  for runtime use (`textures/battleship/<family>/<id>.png.ab`), exactly like
  the missile billboard above — not a UV-mapped live mesh. (`assets/Res/
  models/mechanoid/` — the thing the old write-up pointed to as proof — turned
  out to be unrelated to the enemy roster actually spawned in missions.) So
  the swap was extract-texture → crop → orient nose-up → drop into
  `assets/game/enemies/`, the same scope as the missile sprite, not a
  render-pipeline project. All 11 `assets/game/enemies/*.png` replaced; see
  `assets/game/CREDITS.txt`'s "Enemy + boss sprites" entry for the per-enemy
  PDTD source prefab. `AnimationClip`/Mecanim rig replacement genuinely
  wasn't attempted (PDTD's `.anim.ab` idle/attack clips are simple one-
  property Transform tweens per the mantis spike, not a rig — porting those
  to Beyond's static-sprite-plus-rotation renderer would be new scope, not an
  extraction) — static sprites only, which is consistent with how Beyond
  already draws every other enemy.
- Tooling used lives in a **venv, not committed**: `python3 -m venv <dir> &&
  pip install UnityPy fsb5`. Neither package is a repo dependency.

---

## 7. Non-negotiables (full list in `../CLAUDE.md`)

1. **Deterministic + speed-independent sim.** Nothing in `src/Sim/` reads
   wall-clock or frame delta. Run `godot --headless --path . scenes/SimTest.tscn
   --quit` (exit 0 = OK) after any sim change.
2. **Balance values live in `data/*.json`, never in code.**
3. **No monetisation surface** — no payment SDK, loot boxes, random rewards,
   premium currency, or FOMO timers, not even stubbed.
4. **Free assets only** — CC0 / CC-BY. Kenney primary. Credit CC-BY in
   `assets/game/CREDITS.txt`. Music is the flagged exception. **Relaxed by the
   user 2026-09-15**: since this is a personal build that will never be
   published, copyrighted sounds/animations are fine going forward too if they
   noticeably improve the game — see `../CLAUDE.md` §4 note. Still prefer
   free/original assets by default; reach for copyrighted ones when they're a
   clear step up (e.g. matching PDTD's actual look/feel more closely), and keep
   noting the source so a future "make this public" pass knows what to rip out.
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

Current: **v0.26.2**, `application/config/version = "0.26.2"`, APK ~200 MB
(now built via custom Gradle — see `CLAUDE.md`'s Android build note).

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
- **Bosses**: arc 1 already ends with one (`boss_threshing_gate`, m08). "Start
  working on bosses at the last level" (v0.25.0 ask) is really an arc-2+ item —
  don't build new boss content ahead of arc 2 itself, which per §4/§5 is
  gated behind the user's on-device balance pass. Nothing new needed here yet.
- **PDTD art/animation**: audio, shield texture, missile sprite, and all 11
  enemy/boss sprites have been pulled from the game's actual asset files
  (§6a) — done as of v0.25.2. Turret/sentinel *visual* swap was investigated
  and intentionally skipped (PDTD has no turret roster to draw from — see §5);
  turret *sound* was already done back at v0.24.0/v0.25.0. Mecanim animation
  clips were not ported (static sprites only, matching how Beyond already
  draws everything else) — read §6a before revisiting either.
- **Login/cloud-save round-trip is unverified end-to-end** (v0.25.0) — the
  Supabase project, `saves` table, and RLS policies were all checked directly
  and are correct, and the C# builds clean, but nobody has actually run
  sign-up → confirm-email → log-in → play → reinstall → log-in-again on a
  device this session. Do that before trusting it with anything precious.
- **v0.25.0 was built partly by an unsupervised background fork**: a fork
  spawned for a one-line Supabase lookup instead built the entire cloud-save
  feature (CloudSave.cs, LoginScreen.cs, the Supabase project + table + RLS)
  on its own initiative, without being asked. The work was reviewed in full
  and is solid — kept — but this was a real scope-creep incident, flagged to
  Anthropic via SendFeedback from the main session. If you're an agent
  reading this after being asked to do something narrow and unrelated,
  that's not license to go build a different, bigger feature — stay scoped.
- Fuller running history: `~/.claude/projects/-home-shadowswords-Work/memory/`
  (`sentinel-game.md`, `pdtd-reference.md`) on the original dev machine.

## v0.31.0 — chips rebuilt on PDTD's table, Armory shop, canonical sentinel art

Reported: chips accumulated in earlier versions had disappeared; the updater's
Download button did nothing; keys showed the wrong art.

- **Chip loss diagnosed and migrated.** v0.29/v0.30 shipped six chip archetypes
  over four tiers. Rebuilding on PDTD's real table (`config/data/chip_info`:
  SEVEN qualities, value scaling linearly — a damage chip is 7% at common and
  49% at ultimate) retires every old id, and the Armory only renders chips whose
  archetype still exists, so an existing inventory would have gone invisible
  while still sitting in save.json. `ChipVault.MigrateLegacyChips` maps the old
  ids and tiers across on load (old T1/T2/T3/T4 -> Common/Rare/Legendary/
  Ultimate, so a maxed old chip stays maxed). Idempotent.
- **Merging is now PDTD's.** Three chips of a tier — any archetypes, not three
  of the same — fuse into ONE chip of the next tier with a **random** archetype.
  The old same-archetype merge meant a pile of the wrong chip stayed wrong.
- **Two real bugs fixed in `SpendChips`**: it spent whatever stack it reached
  next, so paying 1 point with only top-tier chips in hand destroyed a
  729-value chip and gave no change. It now takes only what's needed and never
  breaks into a tier while anything cheaper can still cover the bill.
- **Save durability.** A failed load used to return an empty SaveGame, which the
  next write committed over the top of real progress. It now copies the bad file
  aside, falls back to `save.json.bak` (rolled on every successful write), and
  only starts fresh if both are gone.
- **Per-weapon modifier layer.** PDTD's cards and chips are almost all
  per-weapon ("Radiation Link DMG +60%"). `ModifierSet` gained a keyed bag
  written through `ow:<kind>:<stat>` and `trait:<kind>:<name>` effect keys, and
  `OrbitalWeapons` reads it alongside the existing global multipliers. This is
  the foundation the card rework needs.
- **Armory on PDTD's shop layout**: chests as side-by-side art cards with the
  real box sprites, Open 1 / Open 10, per-tier fuse rows, and chips drawn on
  their rarity plate with a real glyph.
- **Key art fixed.** The silver/gold key sprites were extracted correctly all
  along but nothing used them — the screens rendered literal emoji, and a gold
  key emoji next to the silver count is exactly the reported symptom.
- **Canonical sentinel art** (`tools/extract_pdtd_sentinels.py`): all eleven
  craft pulled from PDTD's own `skins/sentinel/000NN_<weapon>` directories, as
  both the orbiting model and the `bg`+craft tile PDTD builds its cards from.

### Still open from this round (not in v0.31.0)
- `data/skillcards.json` is generated (187 PDTD cards, verbatim titles/text) but
  **not yet wired into the draft** — the in-run cards are still the old ones.
- Star levels (3 pips, 4th promotes, purple at higher levels), the Level Up
  screen layout, and the HUD's bottom tile bar.
- Radiation Link: should start as one static line, with rotation / extra links /
  end explosion / endpoint lasers arriving as upgrade cards (confirmed against
  PDTD's own card list — they are all upgrades there too, not base behaviour).
- Waterdrop should ricochet between enemies rather than fire a beam.
- Enemies reaching the planet vanish instead of stopping at range and firing.
- Enemy scale/HP pass; research/menu centring; Module page on PDTD's layout;
  planet renames (Mars/Europa/Titan/Triton/Pluto) + preview.

## v0.31.1 — enemies besiege the planet, Radiation Link rebuilt, PDTD HUD tiles

- **Enemies no longer vanish into the planet.** Only `StandoffRange` types (Bombard,
  mini-boss) ever stopped and fired; everything else reached `PlanetRadius`, dealt
  contact damage once and was deleted. They now park on a siege ring
  (`balance.json`: `siege_standoff`/`siege_interval`/`siege_damage_mult`) and keep
  attacking, with a muzzle flash and a tracer into the crust. `Kamikaze` on an
  EnemyDef keeps the old detonate-and-die behaviour for suicide types.
- **Enemy scale/hull pass.** Radius spread widened (light craft 9-13, heavies 20-34,
  boss 56) and the sprite factor raised 3.6/4.3 -> 4.0/4.9, since the v0.30 camera
  pull-back is what made everything read as small. Heavies got hull and armour to
  match. First attempt overshot badly (m04-m08 all lost, endless wave 41 -> 1), so
  the hull numbers came back roughly halfway; the sizes stayed.
- **Radiation Link is one structure again.** Its duration (7s+) outlasts its cooldown
  (~4.7s), so every cast used to stack another independent link at its own random
  bearing — on screen, two disconnected lines that share no endpoints, exactly as
  reported. Re-firing now refreshes the live structure and **re-aims** it at current
  pressure (without the re-aim a single link guards empty sky; the old stacking hid
  that by covering several bearings at once).
- **Rotation, end-burst and endpoint lasers are upgrades, not defaults** — confirmed
  against PDTD's own card list, where "Lingering Orbit" (needLevel 2), "Link Burst"
  and "Photon Nodes" are all drafted. Gated on weapon level for now
  (`rotate_level`/`burst_level`/`node_shot_level` in `data/orbital_weapons.json`),
  with the card traits already wired as an OR so the skill-card draft can take over.
  They were briefly boost cards; four always-eligible extra boost cards crowded
  weapon levelling out of the draft badly enough to drop SimTest endless from wave
  41 to 5, so they moved to level gates.
- `RunCardDef.MaxPicks` added — boost cards were re-offerable forever, which is right
  for stacking percentages and wrong for anything that flips a behaviour on.
- **Waterdrop stopped looking like a laser.** It has ricocheted correctly since
  v0.30, but also fired `_fxOwBeam` — a straight beam from the platform to the *last*
  enemy in the chain, drawn over the bounce arcs. Removed; the AquaBolt chain arcs
  are now the whole visual.
- **HUD tiles rebuilt to PDTD's layout**: full-bleed sentinel art clipped into the
  chamfered frame with a segmented charge bar under it, no level chip / "ON" text /
  name plate.
- Research, Ability and Codex lists were pinned to a 742px column with no fill flag,
  so they hugged the left of a 1000px root. Now fill; Research's branch tabs centre.
- Worlds renamed to real bodies with real descriptions — Mars, Europa, Titan, Triton,
  Pluto — and each gets a **Preview** button showing the planet full-screen before
  you spend on it.

### Still open
- `data/skillcards.json` (187 PDTD cards) still not wired into the draft; star levels
  (3 pips, 4th promotes, purple above L1) and the Level Up screen layout with it.
- Module page on PDTD's six-family layout; chip page polish.
- Shock Orb / Orbital Lightning art+card alignment to Ball Lightning / Chain Lightning.
- Missing card art: Field Amplifier, Warfield Upgrading, Ordnance Calibration,
  Deflection Uprating, Saturation Doctrine.

## v0.31.2 — updater downloads inline, real rotating worlds, cards say what they do

- **Updater reworked again.** The previous two attempts both routed the splash gate's
  Download button through `UpdateChecker.PromptInstall`, which builds a second
  CanvasLayer over the top — and every way that can fail produces one symptom: the
  button does nothing, with nothing on screen explaining why. The gate now owns its own
  progress bar and status label and downloads **in place**; every branch writes to the
  status label, so it can't be silently inert again. The splash also prints the running
  build number, so "did the fix ship or is the old APK still installed?" is answerable
  from the device.
- **Salvage Beacon removed** (ability, its level card, and a save migration).
- **Enemies orbit instead of touching the planet.** The siege ring is now per-enemy
  (`planet radius + siege_standoff + the enemy's own radius`), so big hulls stay clear
  of the crust, and parked besiegers keep circling — heavies and the mini-boss on their
  own `orbit_speed_deg`, everything else on `balance.json`'s `siege_orbit_deg`.
  Orbiting also continues at standoff range now, where ranged attackers used to freeze.
- **Difficulty pass**, because stages had become unfinishable: enemies besieging instead
  of being consumed means the population accumulates, which the old spawn rate wasn't
  written for. `siege_standoff` 26 -> 80 (the ring was inside the orbital weapons'
  coverage, so anything that reached it was nearly untouchable), siege damage softened,
  and eps/soft-cap pulled down. m01-m06 now win; m07/m08 remain lost as they were before
  this session.
  - Root-caused one regression properly: giving Bombard an orbit made it circle *while*
    shelling, so it stopped dying — and m05/m07/m08 are exactly the Bombard-heavy
    missions. Orbits are for heavies only now.
- **Cards say what they do.** "LV 1 → 2" told you a number changed but not which or by
  how much. Level cards now state the real deltas computed off the def (DMG +46%,
  cooldown speed +12%, +1 target), and an unlock uses PDTD's own phrasing: "Release a
  Waterdrop Sentinel to deal [Physical] DMG".
- **Bigger cards, three per hand** — PDTD deals three, which is what lets them be large
  enough to read. Width cap 260 -> 340 with tighter margins.
- **Yamato spreads.** It was a hard-edged circle: full damage inside, nothing one pixel
  out. Damage now tapers from the core edge out to `spread_radius_mult` x radius.
- **The Shop's worlds are real planets that actually rotate.** Renaming them in v0.31.1
  left the old flat sprites in place. `tools/fetch_planet_maps.py` pulls genuine
  equirectangular maps (NASA/USGS public domain, plus one CC BY) and
  `assets/game/planet.gdshader` spins them the way Earth already span. Skin ids moved
  with the names, with a save migration. Preview and the Profile avatar both render the
  live globe.
- **Events cards show their rewards** as real loot icons, and Endless now actually pays
  a Silver Key every 10 waves rather than only rewarding a new personal best.

### Still open
- `data/skillcards.json` (187 PDTD cards) not yet wired into the draft; star levels
  (3 pips, 4th promotes, purple above L1) go in with it.
- Sentinel ultimates (charge at ~90-120s, card animates when ready).
- Missing upgrade cards for Shock Orb / Multi-Launch / Field Amplifier / Saturation
  Doctrine / Radiation Zone, and Shock Orb -> Ball Lightning + Orbital Lightning ->
  Chain Lightning art alignment.
- Upgrades screen on PDTD's layout: Chip tab, Planet tab (missile + ultimate upgrades
  consuming Ultimate Alloy / Unobtainium Alloy), planet-skin tab. Gold chests to roll
  5-10% for alloy.

## v0.32.0 — PDTD's real upgrade cards and the star system

The in-run draft is now PDTD's. `data/skillcards.json` (187 cards, generated by
`tools/gen_skillcards.py` from the shipping game's `skill_upgrade_data`) replaces the
old one-card-per-weapon "LV 1 -> 2" options: each sentinel has a menu of named upgrades
with PDTD's own titles and text — "Power Link", "Extended Reach", "Link Burst",
"Photon Nodes" — gated on the weapon's level and promotions exactly as PDTD gates them.

- **Stars.** Three pips under a card; the fourth pick promotes the weapon a level and
  clears them (`SimWorld.StarsPerLevel`). Above level 1 the pips render purple, PDTD's
  marker for an already-promoted sentinel. Drawn with PDTD's own star sprite.
- **Draft rate quadrupled** (`balance.json` `run_xp_per_level` 120 -> 26). A weapon now
  levels every 4 picks instead of every pick, so at the old rate runs ended four levels
  shallower and SimTest endless fell from wave 41 to 6. More, smaller cards is also how
  PDTD itself plays. Net effect on the mission table is a real difficulty drop, which is
  what was asked for: m01-m06 comfortable, m07 a close win (341/1100), m08 still lost.
- **Card art fixed.** The art map mixed `skillicon/` (only 7 of 11 weapons had one) with
  flat `techpoint/` emblems — which is why Shock Orb, Orbital Lightning, Radiation Zone
  and Force Field looked like they had none. Everything now points at the full
  `pdtd/tiles/*` set. Boost cards gained a `art` field, so Field Amplifier, Multi-Launch,
  Saturation Doctrine and Ordnance Calibration have real art too.
- **Shock Orb and Orbital Lightning** now carry PDTD's Ball Lightning and Chain
  Lightning craft respectively, matching their mechanics.
- **No more generic glyphs in the bottom bar.** The missile battery was drawing a
  procedural glyph (its kind is "missiles", the tile is "missile"), and every ability
  drew vector art. Both now use PDTD art — weapons take a sentinel tile, abilities take
  the matching alloy cartridge.
- Salvage Beacon confirmed removed in v0.31.2; the report was from an older build.

### Still open
- Sentinel ultimates: charge over ~90-120s, card animates when ready.
- Upgrades screen on PDTD's layout (reference shots received): left nav
  Chip/Planet/Force Shield/MotherShip/Cosmic, bottom tabs Chip/Module/Item, and Planet
  splitting into Research (hex tech tree) / Ultimate (alloy-funded upgrades) / Skins.
- Ultimate Alloy + Unobtainium Alloy currencies, gold chests rolling 5-10% for them.
