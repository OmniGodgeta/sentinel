# Deviations from the design spec

Decisions the user made after the spec was written. The spec stays as-is;
this is the running list of where the build intentionally differs.

| Spec says | Build does | Why |
|---|---|---|
| §7 Offline collectors (8–12h cap, one-tap collect) | **Cut entirely.** No offline earning of any kind. | User: "no offline collectors." Clashes with the "always feel like you can progress, no daily gates" model. |
| §1 Target session "30+ min once a day, 3–5 runs" | Sessions are **5–30 sittings a day**, any length. Progression tuned so *every* run and *every* sitting advances something — never a per-day cap. | User: "It can even be 5 to 30 sittings. Feeling like you can always progress." |
| §4 Hero level 1–20 with milestone ability-slot unlocks | Deferred to the progression phase. Loadout is a fixed 3 slots for now; the 4th/5th come with hero level later. | Phase ordering — progression systems are spec build-step 4. |
| §11 Research tree (90 nodes) | Not built yet. Currencies (RD / XP / Cores / Alloy) accrue to the save file and show on the menu, but there is nothing to spend them on yet. | Spec build-step 4. |
| §10 Shop | Not built yet. | Spec build-step 5. |
| Sentinel ability branch modifiers (levels 5/10/15/20) | Abilities have their base effect only; no per-level branches yet. | Spec build-step 4 (ability leveling via Cores). |
| Turret level-3 A/B fork | **Implemented** (in-run, both forks per turret). | Core to build-phase decisions; pulled forward. |
| Art: pre-rendered 3D→2D realism | Greybox shapes. AI-sprite pipeline (ComfyUI on the RTX 5070) not set up yet. | Spec build-step 6; user chose AI sprite sheets. |

Also settled: Godot 4.7 / C# / net9.0, Android-only personal sideload, 100% free
(no payment SDK, no premium-currency field in the save schema — ever).
