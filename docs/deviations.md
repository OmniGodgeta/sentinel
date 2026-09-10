# Deviations from the design spec

Decisions the user made after the spec was written. The spec stays as-is;
this is the running list of where the build intentionally differs.

| Spec says | Build does | Why |
|---|---|---|
| §7 Offline collectors (8–12h cap, one-tap collect) | **Cut entirely.** No offline earning of any kind. | User: "no offline collectors." Clashes with the "always feel like you can progress, no daily gates" model. |
| §1 Target session "30+ min once a day, 3–5 runs" | Sessions are **5–30 sittings a day**, any length. Progression tuned so *every* run and *every* sitting advances something — never a per-day cap. | User: "It can even be 5 to 30 sittings. Feeling like you can always progress." |
| §4 Hero level 1–20 with milestone ability-slot unlocks | Deferred to the progression phase. Loadout is a fixed 3 slots for now; the 4th/5th come with hero level later. | Phase ordering — progression systems are spec build-step 4. |
| §11 Research tree (90 nodes) | **Built** — 5 branches × 6 tiers, data-driven (`data/research.json`). | — |
| Sentinel ability branch modifiers (levels 5/10/15/20) | **Built** — Potency / Tempo picks at each milestone. | — |
| Turret level-3 A/B fork | **Implemented** (in-run, both forks per turret). | Core to build-phase decisions; pulled forward. |
| Art: pre-rendered 3D→2D realism | Kenney CC0 sprites (ships, turrets, enemies, planets, FX) + a shader Earth. AI-sprite pipeline not set up. | User chose sprite sheets; Kenney covers it for now. |
| Waves | **Replaced with 5-minute survival everywhere** (v0.9.0) — continuous escalating spawn director, real-time base management, per-minute card drafts. Endless/Weekly are open-ended. | User: "instead of multiple waves… spawns a good amount of enemies for like 5 minutes… stronger and tougher for higher levels." |
| §10 Shop | **Built (v0.10.0)** — Commendations currency (earned from stars / Ascension / weekly / Codex / endless depth) + a cosmetic-only Shop: Fleet Requisition (7 hero hulls), Worlds (6 planet skins), Ordnance Palettes (5 VFX colour sets). **Not yet built:** Command Deck (QoL unlocks), Field Supplies (consumables), Archive (Alloy prestige) — deferred to a later pass. | Spec build-step 5. |
| §14 Loadout presets, hold-to-inspect, Simulate, cloud save | Not built. | Later polish pass. |

Also settled: Godot 4.7 / C# / net9.0, Android-only personal sideload, 100% free
(no payment SDK, no premium-currency field in the save schema — ever).
