# 🌌 Stellar Frontiers: Expansion Blueprint

This document outlines the visual and auditory identities for the new mission stages (m09-m15).
These descriptions are designed to guide manual asset creation and visual design in the Godot Editor.

## Stage m09: Helios Forge
- **Visual Aesthetic**: Blinding golden light, solar flares, high-contrast amber and white tones. Particles like floating sparks/embers.
- **Audio/Music Identity**: High-tempo, bright electronic percussion with intense, shimmering pad textures.
- **Asset Mapping Plan**: `Backdrop: solar_flares`, `Music: helios_theme`
- **PDTD Asset Integration**: ✅ Recommended

## Stage m10: Nebula Mists
- **Visual Aesthetic**: Deep purples and indigoes. Silhouettes of massive gas clouds. Low-visibility, hazy atmosphere.
- **Audio/Music Identity**: Low-frequency, swelling ambient pads. Slow, heavy, atmospheric textures.
- **Asset Mapping Plan**: `Backdrop: nebula_purple`, `Music: mist_ambient`
- **PDTD Asset Integration**: ❌ Use Bespoke

## Stage m11: Nova Residue
- **Visual Aesthetic**: Electric blues and neon pinks. Erratic, flickering debris. High-energy radio-wave-like particle effects.
- **Audio/Music Identity**: Glitchy, high-speed breakbeats with sharp, sudden synth stabs.
- **Asset Mapping Plan**: `Backdrop: nova_debris`, `Music: glitch_active`
- **PDTD Asset Integration**: ✅ Recommended

## Stage m12: Void Edge
- **Visual Aesthetic**: Pitch black space with high-contrast white starfield. Distorted lens flares and gravity warping effects.
- **Audio/Music Identity**: Deep, subsonic bass and echoing, cavernous reverb. Minimalist and unsettling.
- **Asset Mapping Plan**: `Backdrop: void_black`, `Music: singularity_theme`
- **PDTD Asset Integration**: ❌ Use Bespoke

## Stage m13: Crystal Expanse
- **Visual Aesthetic**: Cold blues and bright whites. Jagged, refracting light beams. Sharp angular debris.
- **Audio/Music Identity**: High-register, crystalline bell sounds and shimmering, cold synth arpeggios.
- **Asset Mapping Plan**: `Backdrop: ice_crystal`, `Music: frozen_pulse`
- **PDTD Asset Integration**: ❌ Use Bespoke

## Stage m14: Pulsar Core
- **Visual Aesthetic**: Fast, rhythmic flashing lights (blue/white). Pulsating circular glows around the center.
- **Audio/Music Identity**: Heavy, driving percussion with a constant, rhythmic 'heartbeat' effect.
- **Asset Mapping Plan**: `Backdrop: pulsar_rhythm`, `Music: core_beat`
- **PDTD Asset Integration**: ✅ Recommended

## Stage m15: The Event Horizon
- **Visual Aesthetic**: Cinematic epicness. Swirling light-distortion rings, extreme cosmic scale, intense color grading.
- **Audio/Music Identity**: Full-scale orchestral crescendos with heavy brass and epic cinematic percussion.
- **Asset Mapping Plan**: `Backdrop: event_horizon_final`, `Music: final_battle`
- **PDTD Asset Integration**: ✅ Recommended

---
## 🚀 Next Steps Assessment
The expansion of the mission roster is complete. To continue building toward a 'full' game experience, I recommend:

1.  **Enemy Capability Upgrade**: Implement 'Abilities' (e.g., enemy shields, speed bursts) so that higher levels don't just scale HP/Speed, but also add tactical complexity.
2.  **Global Meta-Progression**: Link `Progression.cs` to the mission completion system. Defeating `m08` should unlock `m09`, and players could earn 'Star Dust' to spend on permanent turret buffs.
3.  **Asset Implementation**: Convert the 'Visual Identity' descriptions into actual `.tscn` backgrounds and `.ogg` music tracks.
4.  **Themed UI**: Update the `ArenaScreen` to change its color theme based on the current `backdrop` identifier (e.g., blue/cold for 'Crystal Expanse').
