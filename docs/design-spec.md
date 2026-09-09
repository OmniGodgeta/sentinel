# Sentinel — Game Design Specification

*Working title. A radial tower defense game for Android.*

**Premise for the build team:** this is a better version of Planet Defense TD. Zero ads. Zero microtransactions. 100% free. Deep persistent progression, high-speed play, and a controllable hero battleship.

---

## 1. Core Loop

Radial tower defense. Enemies converge on a central planet from all directions. The player places and upgrades turrets in the build phase, controls a hero ship during waves, and fires abilities on cooldown.

Missions run **20–30 waves, roughly 6–10 minutes each**. Target session is **30+ minutes, once a day** — so 3–5 runs per sitting. Do not design 90-second bites. Include one endless/deep mode for single long runs.

**Losing a run must still pay XP and research currency.** No wasted sessions ever.

---

## 2. Speed Controls (hard requirement)

**1x / 2x / 3x / 4x.**

Build on a **deterministic fixed-timestep simulation** where 4x runs 4x the sim ticks per rendered frame — *not* an animation speed multiplier. All cooldowns and timers run in game-time, not wall-clock.

Every input must be queueable so it can be tapped at 4x without precision. This constraint drove several decisions below — nothing in this design requires frame-accurate input.

Optional toggle: auto-drop to 1x on boss spawn.

---

## 3. In-Wave Control

**Yes:**
- Active abilities on cooldown
- A hero unit dragged around the planet
- A manual hero attack on a short cooldown

**No:**
- Manual turret aiming — needs frame-accurate input, unplayable at 3x/4x, would force players back to 1x and defeat the speed controls
- Live power routing between turrets — same problem

**Build phase only (between waves):**
- Rotating and repositioning turrets — free action
- Placing deflector / gravity-well obstacles. Note this is radial defense, so classic mazing does not apply. Obstacles bend, slow, or split approach vectors rather than creating a path labyrinth.
- Card draft — the temporary boost layer

---

## 4. The Hero: Science Battleship

A large sci-fi science battleship. It orbits the planet, the player drags it to reposition, and it is the player's primary point of agency during a wave.

### Manual attack

- **Missile volley**, manually triggered
- **15-second cooldown**
- Player taps a target or an area; the battleship fires a missile salvo at it
- Missiles travel and impact with real flight time — readable and satisfying even at 4x
- This is the standard attack, always available, never consumes a Sentinel ability slot
- Scales with hero level and research

Between volleys the ship still auto-fires light point-defense weapons, so it is never fully idle. The missile volley is the punctuation.

### Hero level — cap 20

The battleship levels like a MOBA hero, with a **hard cap of level 20**.

Each level grants base stat increases: hull, shields, missile damage, movement speed, ability power.

**Milestone levels unlock capability:**
- **Level 1** — 3 Sentinel ability slots
- **Level 8** — 4th Sentinel ability slot
- **Level 14** — missile volley upgrades to a double salvo
- **Level 20** — 5th Sentinel ability slot

Hero level is **persistent across runs**, not per-match. It levels from mission XP alongside Commander level but on its own curve.

**The League-style in-match power curve comes from the card draft between waves**, not from re-leveling the hero every mission. Each draft offers ability-specific upgrades — extra Lance duration, an extra Barrage arc, a second Nova pulse — so every run builds a different version of the same hero. This gives the moment-to-moment feel of a MOBA power spike without erasing persistent progress at the start of each mission.

*(Flag for the build team: if this reads better as an in-match hero level 1–20 that resets each mission, raise it before building — but the draft approach is the recommendation, since it preserves the "everything progresses permanently" rule.)*

---

## 5. Progression — Five Layers

1. **Research tree** — permanent, account-wide, never resets. Passive boosts plus ability unlocks.
2. **Commander level** — XP from every battle, win or loss. Slow and steady. Unlocks slots, tree tiers, and content.
3. **Hero level (cap 20)** — persistent. Stats plus the milestone unlocks above.
4. **Sentinel abilities (12)** — each levels independently via Sentinel Cores. Equip limit forces real loadout choices.
5. **In-run temporary boosts** — card draft between waves plus drops during waves. Resets each run. This is the only non-permanent layer.

---

## 6. The 12 Sentinel Abilities

### Shared rules

- All 12 unlock through the research tree; each levels independently via Sentinel Cores, earned every run, win or lose
- **Equip 3–5 of 12** depending on hero level (see milestones above)
- All are **tap-to-cast or tap-then-drag-a-reticle**. Nothing needs reflexes, so all 12 remain usable at 4x. Casts queue to the next sim tick.
- Levels 1–20. Each level improves numbers.
- **Levels 5, 10, 15 and 20 grant a branching choice** between two mutually exclusive modifiers, so two players with the same maxed ability play differently
- Global soft rule: no more than one panic button off cooldown at a time. Tune cooldowns so a loadout has rhythm rather than constant spam.

### Offensive

**1. Orbital Lance** — Drag a reticle; a sustained beam burns everything under it. Primary boss-killer.
*Scales:* damage, duration, pierce. *CD ~35s*
*Branches:* longer burn / splits into two thinner beams

**2. Kinetic Barrage** — Railgun slugs saturate a chosen arc sector of the orbit. The wave-clear tool.
*Scales:* slug count, arc width, damage. *CD ~25s*
*Branches:* wider arc / armor-shredding slugs

**3. Nova Pulse** — Shockwave expands from the planet in all directions: damage plus knockback. Untargeted, instant, the "about to be overrun" button.
*Scales:* damage, radius, knockback. *CD ~50s*
*Branches:* second delayed pulse / leaves a burning ring

### Control

**4. Temporal Well** — Localized slow field. Deliberately *not* a global time-slow, which would fight the speed controls.
*Scales:* slow %, radius, duration. *CD ~40s*
*Branches:* near-stop in a tiny radius / mild slow over a huge one

**5. Gravity Snare** — Drags enemies into a single clump and holds them. Setup tool; pairs with Barrage or Nova.
*Scales:* pull strength, hold duration, radius. *CD ~35s*
*Branches:* crush damage over time / longer hold

**6. Ion Cascade** — Chain EMP. Strips enemy shields, disables enemy specials, stuns mechanical types.
*Scales:* chain jumps, stun duration, shield strip. *CD ~30s*
*Branches:* more jumps / permanent shield disable on hit

### Defensive

**7. Aegis Barrier** — Planetary shield absorbs a flat damage pool until spent or expired.
*Scales:* absorb pool, duration. *CD ~45s*
*Branches:* reflects a % of absorbed damage / overflow converts to hero shield

**8. Point Defense Grid** — Auto-intercepts incoming enemy projectiles. Hard counter to ranged and artillery waves.
*Scales:* intercept rate, duration, coverage arc. *CD ~40s*
*Branches:* also intercepts missiles and bombers / intercepts trigger small explosions

**9. Repair Swarm** — Nanite drones heal planet integrity and hero hull over time.
*Scales:* heal rate, duration, planet/hero split. *CD ~60s*
*Branches:* burst heal instead of over-time / lingering damage reduction after it ends

### Hero & Utility

**10. Overdrive Protocol** — Buffs the battleship: missile reload, damage, movement speed. Drops the missile cooldown from 15s to roughly 6s for its duration, turning the hero into the main gun.
*Scales:* buff %, duration. *CD ~45s*
*Branches:* huge damage but hero takes extra damage / moderate buff plus a damage shield

**11. Salvage Beacon** — Field where enemy kills drop bonus research currency and XP. The farming pick.
*Scales:* bonus %, radius, duration. *CD ~55s*
*Branches:* global bonus at a lower rate / stacks higher the more kills happen inside it

**12. Sentinel Deployment** — Summons autonomous drone escorts that orbit the hero and fight independently. The one ability that keeps working while the player is busy repositioning.
*Scales:* drone count, HP, damage, duration. *CD ~50s*
*Branches:* fewer but tougher drones / a swarm of fragile ones

### Unlock order

Open with three — Kinetic Barrage, Aegis Barrier, Overdrive Protocol — so the first loadout already covers offense, defense, and hero. Then roughly one new ability every 2–3 research tiers. Salvage Beacon unlocks mid-game, once farming is a real strategy rather than the only one.

---

## 7. Daily Progress & Offline

**Yes to offline collectors**, with strict rules:
- Cap at 8–12 hours
- One tap to collect
- No ad-doubling (the game has no ads)
- Supplementary only — never the main source of any currency

**Zero energy gates.** A player should be able to play for three straight hours if they want.

---

## 8. Art Direction

Realism-leaning: believable ship and planet surfaces, physically plausible lighting, with **stylized and exaggerated explosions and ability VFX**. The spectacle should be loud even when the hardware isn't.

Suggested approach: **pre-render 3D models to 2D sprite sheets** for a realistic look at 2D performance cost on mobile.

**Kenney packs: greybox placeholders only.** They're flat and cartoonish and won't survive contact with the final art bar. Fine for prototyping the loop; plan on replacing all of it.

---

## 9. Monetization: None

**The game is 100% free. There are no microtransactions, no ads, no paid currency, no paid unlocks, no season pass, no gacha, and no real-money storefront of any kind.**

This is a hard architectural rule, not a launch-window decision. Do not build payment SDK hooks, do not build a "premium currency" field into the save schema "just in case," and do not design any system with a hidden pay-gate shape that could be switched on later. Systems built to be monetized feel monetized even when the price is zero — the artificial scarcity, the drip-fed currency, the friction that exists only to be smoothed over by a purchase. None of that goes in.

Practical consequences the build team should hold onto:

- Reward curves are tuned for **player satisfaction only**, not to create a purchase impulse. When a number feels stingy in playtesting, the answer is always to raise it.
- No timers exist to be skipped. No energy. No "speed up construction."
- Offline collectors are convenience, never a bottleneck.
- The absence of ads and purchases is a genuine differentiator in this genre. **State it plainly on the store page** — it will do more for downloads than any feature bullet.

---

## 10. The Shop (in-game currency only)

A dedicated **Shop** section in the main menu. Everything in it is bought with currency earned by playing. Nothing in it can be bought with money, because there is no money in this game.

The Shop exists for three reasons: it gives late-game currency a sink, it gives star ratings and optional objectives a payoff, and it lets players express themselves.

### Shop currencies

| Currency | Earned from | Shop use |
|---|---|---|
| **Research Data** | Every run, scaled to waves cleared — paid on loss too | Consumables, loadout slots, quality-of-life |
| **Sentinel Cores** | Every run, plus guaranteed boss drops | Ability levels only — never spent in the Shop |
| **Exotic Alloy** | Boss kills, mission first-clears | Tier 5–6 research nodes and high-end Shop stock |
| **Commendations** | Mission star ratings, Ascension tiers, weekly seeded runs, Codex completion | Cosmetics only |

Commendations are the cosmetic currency and the reason to chase three stars. They buy nothing that affects combat, so a player who only wants to play well is never behind, and a player who wants a beautiful fleet has something to work toward for a very long time.

### Shop tabs

**1. Fleet Requisition** *(Commendations)*
Hero battleship hulls. Not reskins of the same silhouette — genuinely different ship designs, all mechanically identical. Target 12–15 at launch, themed to the setting: a scarred survey vessel, a pre-collapse dreadnought, a shipbreaker's improvised hull, an intact archive ship. This is the flagship cosmetic and should get the most art attention of anything in the Shop.

**2. Ordnance Palettes** *(Commendations)*
VFX color and style variants for the missile volley and for each Sentinel ability. Ion-blue, plasma-orange, void-violet, a monochrome white for players who want clarity over spectacle. Sell them per-ability and as full sets.

**3. Worlds** *(Commendations)*
Planet biome skins — ice, volcanic, oceanic, shattered, terraformed, dead. Purely visual. Bundle each with a matching skybox.

**4. Command Deck** *(Research Data)*
Quality-of-life unlocks, permanent once bought:
- Additional loadout preset slots beyond the free three
- Extra card draft reroll charge per run
- Turret blueprint presets — save a favored opening build and place it in one tap
- Codex auto-scan — new enemy entries unlock on first sighting rather than first kill
- Extended offline cap, in one-hour steps

**5. Field Supplies** *(Research Data)*
Single-use consumables applied before a run. Deliberately modest, deliberately cheap, deliberately never required:
- Reinforced Hull — start at 120% planet integrity
- Advance Scouting — see all wave compositions for the whole mission, not just the next one
- Salvage Charter — +30% Research Data from one run
- Emergency Requisition — start with extra in-run build currency

Hard rule on this tab: **no consumable may exceed roughly a 15% swing.** These are a small assist on a wall, not a substitute for building well. If playtesting shows players stockpiling and burning consumables to clear content they otherwise couldn't, the consumables are too strong — cut their power, not their price.

**6. Archive** *(Exotic Alloy)*
Late-game prestige stock for players who have finished the research tree and have nowhere else to spend Alloy:
- Ascension modifier tokens — reroll a punishing modifier once per tier
- Codex expansions — long-form lore entries, ship schematics, in-world documents
- Fleet nameplates and commander titles shown on the endless leaderboard

### Shop rules

- **No randomness.** No loot boxes, no crates, no rolls, no duplicates. Every item shows its price and is bought directly. This is non-negotiable — a random reward box is the mechanical seed of a monetized game, and this game doesn't have one.
- **No rotating limited stock and no FOMO timers.** The full catalogue is visible from day one, greyed out with its price and unlock condition shown. Players should be able to plan toward something they want, and it should still be there in six months.
- **Nothing in the Shop is required to complete any content.** A player who never opens the Shop can three-star every mission and finish every Ascension tier.
- **Nothing in the Shop is sold for real money, ever.** If this game is ever ported, licensed, or handed to a publisher, this rule travels with it.

---

## 11. Research Tree

### Structure

**Five branches, six tiers each. Roughly 90 nodes total.** Permanent and account-wide — never resets, never respecs away progress.

Branches unlock in sequence so the early game isn't paralyzing: Armaments and Logistics are open from the start, Fortification opens at Commander 5, Fleet Command at Commander 10, Sentinel Protocols at Commander 15.

**Tier gates:** a tier opens when the player has bought any 3 nodes in the tier below it *and* hit the Commander level gate. Tier 1 is open, Tier 2 at Cmdr 8, Tier 3 at Cmdr 16, Tier 4 at Cmdr 25, Tier 5 at Cmdr 35, Tier 6 at Cmdr 50.

**No dead ends.** Every node does something on the run it's bought. No pure connector nodes that exist only to cost currency.

### Currencies

| Currency | Source | Spent on |
|---|---|---|
| **Research Data** | Every run, scaled to waves cleared — paid on loss too | All standard tree nodes |
| **Sentinel Cores** | Every run, plus guaranteed boss drops | Sentinel ability levels only |
| **Exotic Alloy** | Boss kills and mission first-clears only | Tier 5–6 nodes and capstones |

Splitting Alloy out means late-tier progress requires *beating* content, not just grinding wave 8 of mission one on repeat.

### Branch A — Armaments (turrets)

*Open from start.*

- **T1** — Turret damage +5% per rank (5 ranks) · Turret range +4% (5 ranks) · Unlock second turret type
- **T2** — Fire rate +4% (5 ranks) · Projectile speed +10% · Unlock third turret type
- **T3** — Armor penetration +8% (3 ranks) · Turret crit chance 5% · Unlock fourth turret type
- **T4** — Turret upgrade costs −10% in-run (3 ranks) · Splash radius +15% · Unlock fifth turret type
- **T5** — Turrets gain +1 target · Crit damage +50% · Turrets fire during build phase
- **T6 Capstone — choose one, permanent:**
  - **Saturation Doctrine** — +25% fire rate, −10% damage per shot
  - **Precision Doctrine** — +35% damage, −10% fire rate

### Branch B — Fortification (planet)

*Opens at Commander 5.*

- **T1** — Planet integrity +8% (5 ranks) · Start each run with a small shield
- **T2** — Shield regen between waves (3 ranks) · Unlock deflector obstacles · Obstacle placement +1
- **T3** — Damage taken −5% (3 ranks) · Unlock gravity-well obstacles · Integrity restored per wave cleared
- **T4** — Leaked enemies deal −25% damage · Obstacle placement +2 · Shields absorb the first hit fully
- **T5** — Integrity below 30% grants +20% all damage (desperation) · Obstacles regenerate when destroyed
- **T6 Capstone — choose one:**
  - **Bastion** — +40% max integrity, no regeneration at all
  - **Resilience** — +15% integrity, plus 2% regeneration per wave cleared

### Branch C — Fleet Command (hero battleship)

*Opens at Commander 10.*

- **T1** — Hero hull +8% (5 ranks) · Missile volley damage +6% (5 ranks) · Hero drag speed +10%
- **T2** — Missile cooldown −0.5s (4 ranks, floor of 13s) · +1 missile per volley · Hero shield pool
- **T3** — Missiles apply a burn · Hero auto-fire point-defense damage +30% · Hull regen out of combat
- **T4** — Missile cooldown −1s (2 ranks, floor of 11s) · Missiles seek and re-target on kill · Hero takes −20% damage
- **T5** — Volley splits across two targets · Hero death respawns in 8s instead of 15s
- **T6 Capstone — choose one:**
  - **Dreadnought** — double hull, hero moves 25% slower
  - **Interceptor** — +40% drag speed and missile reload, −20% hull

*Note: this branch and the hero's own level 1–20 are separate systems. Levels give milestone unlocks; research gives the tuning.*

### Branch D — Sentinel Protocols (the 12 abilities)

*Opens at Commander 15. This is the branch that unlocks abilities.*

Structure it as a **hub**, not a line: the three starter abilities sit at T1, and each remaining ability is its own small unlock node hanging off the tier appropriate to its power.

- **T1** — Kinetic Barrage, Aegis Barrier, Overdrive Protocol (all three, free at branch open) · Ability damage +5% (5 ranks)
- **T2** — Unlock **Repair Swarm** · Unlock **Ion Cascade** · All cooldowns −4% (5 ranks)
- **T3** — Unlock **Orbital Lance** · Unlock **Gravity Snare** · Ability radius/duration +8% (4 ranks)
- **T4** — Unlock **Nova Pulse** · Unlock **Point Defense Grid** · Unlock **Salvage Beacon** · Abilities start each run off cooldown
- **T5** — Unlock **Temporal Well** · Unlock **Sentinel Deployment** · Cooldowns −10% · Sentinel Core gain +25%
- **T6 Capstone — choose one:**
  - **Rapid Protocols** — −20% all cooldowns, −15% ability effect
  - **Heavy Protocols** — +30% ability effect, +15% cooldowns

Sentinel Core spending happens on a **separate ability screen**, not in the tree. The tree unlocks and globally tunes; Cores level individual abilities 1–20 with their branch choices at 5/10/15/20.

### Branch E — Logistics (economy & meta)

*Open from start. Compounds — worth buying into early.*

- **T1** — Research Data gain +6% (5 ranks) · XP gain +6% (5 ranks) · Starting in-run currency +50
- **T2** — Card draft offers 3 options instead of 2 · Offline collector cap 8h → 10h · Sentinel Core gain +10%
- **T3** — Reroll one card draft per run · Exotic Alloy gain +15% · In-run income per wave +10% (3 ranks)
- **T4** — Card draft offers 4 options · Offline cap 10h → 12h · Second draft reroll
- **T5** — Rare cards appear twice as often · Losing a run pays 100% rewards instead of partial
- **T6 Capstone — choose one:**
  - **Prospector** — +35% Research Data, no Alloy bonus
  - **Vanguard** — +35% Exotic Alloy, no Data bonus

### Costs & rules

**Cost curve:** node cost scales with tier and rank. T1 ranks start around 50 Data; T6 capstones cost several thousand Data plus a meaningful Alloy sum. Aim so a fully invested player takes 60–100 hours to complete the tree, with something buyable roughly every 2–3 runs early on and every 6–8 runs late.

**Respec:** capstone choices only, freely and at no cost, from the tree screen out of combat. Everything else is permanent — that's the point. Free capstone respec means players experiment with builds instead of researching the "right" answer online.

**Preview everything.** Every node shows exact numbers before purchase — no "increases damage" vagueness. The tree screen shows a live total of every bonus currently active, so players can see the sum of what they've built.

### Data requirements

Every node is a config entry with: id, branch, tier, prerequisite ids, rank count, cost curve, currency type, effect type, effect value per rank, Commander level gate, and display text. Capstone pairs are flagged as mutually exclusive groups.

The tree layout itself renders from config too, so branches and tiers can be reordered or expanded without touching UI code.

---

## 12. Turret Roster

**Eight types.** Five unlock through the Armaments branch as written; three unlock from mission first-clears so that beating content — not just grinding — opens options. *(Armaments T1/T2/T3/T4 unlock turrets 2–5; turrets 6–8 come from mission milestones.)*

Every turret has **three in-run upgrade levels**, and at level 3 forks into one of two specializations — the same A/B design language as the Sentinel abilities, so the whole game teaches one grammar.

| # | Turret | Role | Level-3 fork |
|---|---|---|---|
| 1 | **Autocannon** | Cheap, fast, single-target chip damage. The one you spam early. | Shred (fire rate) / Slug (armor pierce) |
| 2 | **Flak Battery** | Wide short-range AoE. Melts swarms, near-useless vs. armor. | Wider cone / Proximity airburst |
| 3 | **Railgun** | Slow, huge single-target, pierces everything in a line. | Overcharge (damage) / Cascade (extra pierce) |
| 4 | **Tesla Node** | Chain lightning, strips shields, weak raw damage. | More jumps / Full shield removal |
| 5 | **Missile Silo** | Homing splash, slow reload. Punishes clumped enemies. | Cluster (more, smaller) / Bunker-buster (one, huge) |
| 6 | **Laser Lattice** | Continuous beam that ramps damage the longer it stays on one target. The boss answer. | Faster ramp / Ramp persists 3s after breaking contact |
| 7 | **Graviton Projector** | Zero damage. Slows and drags enemies toward it. Pure support. | Stronger pull / Larger radius |
| 8 | **Nanite Forge** | Zero damage. Buffs adjacent turrets and repairs obstacles. | +25% adjacent damage / +40% adjacent range |

**Design rule: no turret is good at everything, and no turret is ever worthless.** Every type should be the correct answer to at least one enemy and the wrong answer to at least one other. If playtesting shows a turret that's never built, buff it — don't cut it.

**Placement matters in a radial game.** Turrets have firing arcs, not 360° coverage. A turret on the north face genuinely cannot help the south face. That's what makes rotating and repositioning in the build phase a real decision rather than busywork, and it's what makes the draggable hero valuable — the hero is the mobile answer to whichever face is currently collapsing.

---

## 13. Enemy Roster

Each enemy exists to punish a specific lazy build. That's the whole point of the roster.

### Line units

**Skiff** — Fast, fragile, arrives in dozens. Punishes single-target-only builds. *Answer: Flak, Nova Pulse.*

**Hauler** — Slow, enormous HP, heavy frontal armor. Punishes chip-damage spam. *Answer: Railgun, Laser Lattice, armor pen.*

**Interceptor** — Extremely fast and evasive; slow projectiles miss it outright. Punishes all-missile builds. *Answer: hitscan — Laser, Tesla, Railgun.*

**Aegis Cruiser** — Heavy regenerating shield that must be broken before hull damage lands. Punishes builds with no shield-strip. *Answer: Tesla Node, Ion Cascade.*

### Threat units

**Bombard** — Halts at long range and shells turrets and the planet from outside most turret range. Punishes short-range builds. *Answer: Point Defense Grid, Railgun, Orbital Lance, or send the hero.*

**Carrier** — Slow, tanky, continuously spawns Skiffs until killed. Punishes players who don't prioritize targets. *Answer: focus fire, hero missile volley.*

**Phase Runner** — Periodically blinks forward and is untargetable mid-blink; ignores obstacles entirely. Punishes reliance on obstacle shaping. *Answer: AoE saturation, Gravity Snare on the landing zone.*

**Leech** — Attaches to a turret and disables it for 10 seconds. Punishes builds resting on one super-turret. *Answer: coverage overlap, Ion Cascade.*

**Warden** — Deals no damage; shields and repairs every enemy near it. Punishes players who kill front-to-back. *Answer: kill it first — this is the unit that teaches target priority.*

**Siege Crawler** — Immune to all slows and pulls. Punishes control-heavy builds. *Answer: raw damage.*

### Bosses

One per mission arc, each with a **telegraphed mechanic the player must actively respond to** — not just a big health bar.

- **The Threshing Gate** — periodically retracts into an invulnerable shell; only the seam is damageable. Rewards precise Orbital Lance placement.
- **Hexad Warmind** — six independently destroyed weapon pods; each kill enrages the remaining ones. Rewards planning the kill order.
- **The Long Hunger** — heals from every enemy that dies near it. Forces the player to fight it *away* from the swarm, which means committing the hero.
- **Null Sovereign** — projects a field that disables one random equipped Sentinel ability at a time, rotating every 20 seconds. Tests whether the loadout has redundancy.
- **The Drowned Choir** *(endless-mode superboss)* — combines three earlier mechanics and scales infinitely.

### Wave composition rules

Show the **full composition of the next wave during the build phase**, with icons and counts. The player should always be able to see a Warden or Bombard coming and prepare for it. Difficulty comes from responding to visible information, not from being ambushed by information the game hid.

Never introduce more than one new enemy type per mission, and always debut a new type in small numbers before it appears in force.

---

## 14. World, Content & Polish

### Give it a world

Sci-fi is the reason to build this rather than a generic TD, so commit to it.

**Frame:** the planet is a seed-vault world — the last archive of a civilization that already lost. The player commands a recovered pre-collapse science battleship, the **Sentinel** class, whose original crew is long dead and whose systems are only partially understood. The **Sentinel Protocols** (the 12 abilities) are recovered fragments of its original software; researching them is literally recovering lost capability. That framing makes the progression system *diegetic* — the player isn't buying upgrades, they're restoring a ship.

**The enemy** shouldn't be generic aliens. Make them the **Harvest** — self-replicating machines built by the same lost civilization, still faithfully executing a resource-extraction order nobody remembers issuing. That gives a reason for the roster to look industrial rather than organic, and it makes the Carrier and the Warden make sense.

**Ship a Codex.** Every enemy, turret, boss, and ability gets a short entry that unlocks on first encounter — 80 to 150 words of in-world text. This is the cheapest content the project will ever produce per unit of player affection, and for a sci-fi audience it's often the thing they remember.

### Mission structure & long tail

**Campaign:** 6 arcs × 8 missions = 48 missions, each with a first-clear Exotic Alloy reward. Three star ratings per mission (clear it / clear with the planet above 75% integrity / clear without losing the hero) that pay Commendations.

**Ascension:** after the campaign, replay it at escalating difficulty tiers with modifiers — *enemies gain 30% shields*, *turret range reduced 20%*, *only 3 ability slots*, *bosses spawn twice*. Ten tiers. This is infinite content for near-zero art cost.

**Endless mode** with local and global leaderboards, plus a **weekly seeded run** so everyone faces the same wave sequence. Weekly seeds create the only competitive comparison worth having and cost almost nothing to generate.

### UX details that decide whether people stay

- **Loadout presets.** Three free slots, more available in the Shop. Nobody wants to re-equip four abilities before every run.
- **Speed setting persists** between runs and between sessions. If a player has to tap up to 4x every mission, they will feel that friction 500 times.
- **Hold-to-inspect on any enemy** mid-wave shows HP, armor, shields, immunities, and the Codex line. Especially valuable at 4x.
- **Damage attribution screen** at end of run: percentage dealt by each turret, each ability, and the hero. This is what turns players into theorycrafters, and theorycrafters are the ones who stay for a year.
- **"Simulate" cleared missions** — for any mission already three-starred, offer an instant resolve at reduced reward. Respects the player's time without gutting the reason to play.
- **Haptics on the missile volley.** A short, distinct rumble on launch and a heavier one on impact. Costs nothing and is the cheapest way to make the hero feel powerful.

### Sound

Under-scoped in almost every indie TD and it does more work than any other single element. Budget real time for: a distinct launch/impact pair for the hero missiles, a unique cast sound per Sentinel ability, and an audio ducking system so that at 4x the mix doesn't collapse into undifferentiated noise. Consider **dropping the music to a low drone during boss phases** so mechanic telegraphs read clearly.

### Accessibility

Colorblind-safe enemy silhouettes — enemies distinguishable by shape, not just color. A reduce-flash toggle (this game will have a lot of explosions). One-handed portrait layout with all controls in the lower third. Adjustable font scale.

### Technical

- **Cloud save from day one.** Losing 80 hours of permanent progression is unrecoverable as a player relationship. Not a post-launch feature.
- **Object pooling for everything.** At 4x with a Carrier on screen there may be 300+ active entities. Allocating and garbage-collecting mid-wave will stutter.
- **Particle budget that scales with speed.** At 3x and 4x, automatically reduce particle counts and skip minor VFX. Nobody can see them at that speed, and it buys significant headroom on mid-range Android hardware.
- **Telemetry for balance.** Log which turrets get built, which abilities get equipped, and where players fail. Nobody can balance 8 turrets × 12 abilities × 10 enemies by intuition alone.
- **Data-drive everything from config files (JSON or similar) from day one** — turrets, enemies, abilities, research nodes, hero stat curves, ability level curves, branch modifiers, drop tables, shop stock. Balance passes must never require code changes.
- **Deterministic sim** as described in section 2 — this also gives replays and reproducible bug reports for free.
- **Test on a cheap Android device weekly**, not on a flagship. This genre lives on hardware that's four years old.

---

## 15. Scope & Build Order

This spec describes a large game. Built well, it's 8–14 months of steady work. The most common failure here is building all five progression systems before verifying the core loop is fun.

**Build in this order:**

1. **One mission. Greybox. No art.** Autocannon and Flak only. Skiffs and Haulers only. The hero with its 15-second missile volley. Three abilities. Working 1x–4x speed.
2. **Play it at 4x for a week.** If it isn't fun with grey rectangles and no progression, no amount of research tree will save it. If it *is* fun, everything after this is upside.
3. Full turret and enemy rosters, one arc of 8 missions, first boss.
4. Research tree, Commander level, hero level.
5. Card draft, offline collectors, Codex, Shop.
6. Art pass, sound pass, Ascension, endless mode, weekly seeds.

**Cut candidates if time runs short**, in this order: obstacle placement, Nanite Forge, weekly seeded runs, the Simulate feature, Archive shop tab.

**Never cut:** the speed controls, the hero, the permanent progression, the wave preview, or the no-monetization rule. Those five are the reason this game is better than the one it's replacing.
