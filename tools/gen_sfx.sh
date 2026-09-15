#!/usr/bin/env bash
# Procedural game SFX — layered filtered noise + sub-bass, enveloped.
# Original content (no third-party samples). Output: 44.1k mono OGG.
set -e
mkdir -p "$(dirname "$0")/sfx"
cd "$(dirname "$0")/sfx"
Q="-c:a libvorbis -q:a 4 -ar 44100 -ac 1"

# ---------- LASER — turret_shot (bright/quick) ----------
ffmpeg -y -v error -filter_complex "
 aevalsrc=exprs='sin(2*PI*(2400*t-5937.5*t*t))*exp(-31.25*t)':s=44100:d=0.16[tone];
 anoisesrc=d=0.03:c=white:a=1:s=21[n];
 [n]highpass=f=3200,volume=2.0,afade=t=out:st=0.004:d=0.026[crack];
 sine=frequency=150:duration=0.1[sb];
 [sb]volume=1.1,afade=t=out:st=0.006:d=0.09[thump];
 [tone][crack][thump]amix=inputs=3:normalize=0:weights=1 0.4 0.35,
   acompressor=threshold=-14dB:ratio=4:attack=1:release=55,
   alimiter=limit=0.92,volume=1.15,aformat=channel_layouts=mono
" $Q turret_shot.ogg

# ---------- LASER — turret_shot_b (variant) ----------
ffmpeg -y -v error -filter_complex "
 aevalsrc=exprs='sin(2*PI*(1900*t-4078.9*t*t))*exp(-26.3*t)':s=44100:d=0.19[tone];
 anoisesrc=d=0.035:c=white:a=1:s=33[n];
 [n]highpass=f=3000,volume=2.0,afade=t=out:st=0.004:d=0.03[crack];
 sine=frequency=130:duration=0.12[sb];
 [sb]volume=1.15,afade=t=out:st=0.006:d=0.11[thump];
 [tone][crack][thump]amix=inputs=3:normalize=0:weights=1 0.42 0.4,
   acompressor=threshold=-14dB:ratio=4:attack=1:release=55,
   alimiter=limit=0.92,volume=1.15,aformat=channel_layouts=mono
" $Q turret_shot_b.ogg

# ---------- LASER — sentinel_shot (heavier orbital weapon) ----------
ffmpeg -y -v error -filter_complex "
 aevalsrc=exprs='sin(2*PI*(1500*t-2666.7*t*t))*exp(-20.8*t)':s=44100:d=0.24[tone];
 anoisesrc=d=0.045:c=white:a=1:s=47[n];
 [n]highpass=f=2600,volume=2.2,afade=t=out:st=0.005:d=0.04[crack];
 sine=frequency=100:duration=0.16[sb];
 [sb]volume=1.4,afade=t=out:st=0.01:d=0.14[thump];
 [tone][crack][thump]amix=inputs=3:normalize=0:weights=1 0.4 0.5,
   acompressor=threshold=-15dB:ratio=4.5:attack=1:release=70,
   alimiter=limit=0.93,volume=1.2,aformat=channel_layouts=mono
" $Q sentinel_shot.ogg

# ---------- EXPLOSION (standard, enemy death) ----------
ffmpeg -y -v error -filter_complex "
 anoisesrc=d=0.75:c=brown:a=1:s=101[n];
 [n]lowpass=f=900,lowpass=f=520,volume=7,
    afade=t=in:st=0:d=0.004,afade=t=out:st=0.06:d=0.6,
    aeval='val(0)*(0.35+0.65*exp(-7*t))':c=same[body];
 sine=frequency=70:duration=0.35[s];
 [s]asetrate=44100*0.75,aresample=44100,volume=2.6,afade=t=out:st=0.01:d=0.32[sub];
 anoisesrc=d=0.06:c=white:a=1:s=3[c];
 [c]highpass=f=2400,volume=2.0,afade=t=out:st=0.005:d=0.055[crk];
 [body][sub][crk]amix=inputs=3:normalize=0:weights=1 0.85 0.45,
   acompressor=threshold=-16dB:ratio=5:attack=1:release=120,
   alimiter=limit=0.95,volume=1.3,aformat=channel_layouts=mono
" $Q explosion.ogg

# ---------- EXPLOSION B (variant) ----------
ffmpeg -y -v error -filter_complex "
 anoisesrc=d=0.9:c=pink:a=1:s=202[n];
 [n]lowpass=f=1100,lowpass=f=440,volume=7,
    afade=t=in:st=0:d=0.006,afade=t=out:st=0.08:d=0.72,
    aeval='val(0)*(0.3+0.7*exp(-6*t))':c=same[body];
 sine=frequency=58:duration=0.45[s];
 [s]asetrate=44100*0.7,aresample=44100,volume=2.8,afade=t=out:st=0.02:d=0.4[sub];
 anoisesrc=d=0.05:c=white:a=1:s=9[c];
 [c]highpass=f=3000,volume=1.8,afade=t=out:st=0.004:d=0.045[crk];
 [body][sub][crk]amix=inputs=3:normalize=0:weights=1 0.9 0.4,
   acompressor=threshold=-16dB:ratio=5:attack=1:release=140,
   alimiter=limit=0.95,volume=1.3,aformat=channel_layouts=mono
" $Q explosion_b.ogg

# ---------- EXPLOSION BIG (missile impact / heavy kill) ----------
ffmpeg -y -v error -filter_complex "
 anoisesrc=d=1.7:c=brown:a=1:s=303[n];
 [n]lowpass=f=380,lowpass=f=300,volume=6.5,
    afade=t=in:st=0:d=0.005,afade=t=out:st=0.25:d=1.42,
    aeval='val(0)*(0.4+0.6*exp(-3.2*t))':c=same[rumble];
 sine=frequency=52:duration=0.7[s1];
 [s1]afade=t=out:st=0.03:d=0.65,volume=3.2,asetrate=44100*0.68,aresample=44100[sub];
 anoisesrc=d=0.11:c=white:a=1:s=7[c];
 [c]highpass=f=1700,volume=2.6,afade=t=out:st=0.012:d=0.095[crack];
 [rumble][sub][crack]amix=inputs=3:normalize=0:weights=1 0.95 0.55,
   acompressor=threshold=-14dB:ratio=6:attack=1:release=180,
   alimiter=limit=0.96,volume=1.45,aformat=channel_layouts=mono
" $Q explosion_big.ogg

# ---------- MISSILE LAUNCH (hero volley) — ignition crack + chugging rocket-motor
# roar (tremolo-modulated noise) + sub thump + receding whoosh ----------
ffmpeg -y -v error -filter_complex "
 anoisesrc=d=0.05:c=white:a=1:s=111[c];
 [c]highpass=f=2000,volume=2.6,afade=t=out:st=0.006:d=0.044[crack];
 anoisesrc=d=0.5:c=brown:a=1:s=222[n];
 [n]bandpass=f=900:width_type=h:w=1400,volume=3.0,tremolo=f=38:d=0.35,
    aeval='val(0)*(0.25+0.75*exp(-3.6*t))':c=same[roar];
 sine=frequency=68:duration=0.32[s1];
 [s1]volume=2.4,afade=t=out:st=0.02:d=0.3,asetrate=44100*0.72,aresample=44100[sub];
 anoisesrc=d=0.5:c=white:a=1:s=333[w];
 [w]highpass=f=600,aeval='val(0)*exp(-3.0*t)':c=same,volume=2.2[whoosh];
 [crack][roar][sub][whoosh]amix=inputs=4:normalize=0:weights=0.7 1 0.9 0.6,
   acompressor=threshold=-16dB:ratio=4:attack=2:release=110,
   alimiter=limit=0.94,volume=1.25,aformat=channel_layouts=mono
" $Q missile_launch.ogg

# ---------- BATTERY LAUNCH (planet missile silo) — heavier mechanical thump +
# rocket-motor roar + receding whoosh ----------
ffmpeg -y -v error -filter_complex "
 sine=frequency=85:duration=0.34[s];
 [s]asetrate=44100*0.62,aresample=44100,volume=3.2,afade=t=out:st=0.015:d=0.3[thump];
 anoisesrc=d=0.05:c=white:a=1:s=444[c];
 [c]highpass=f=2200,volume=2.4,afade=t=out:st=0.006:d=0.044[crack];
 anoisesrc=d=0.45:c=brown:a=1:s=555[n];
 [n]bandpass=f=750:width_type=h:w=1100,tremolo=f=32:d=0.3,
    aeval='val(0)*(0.3+0.7*exp(-4.2*t))':c=same,volume=3.0[roar];
 anoisesrc=d=0.4:c=white:a=1:s=666[w];
 [w]highpass=f=550,aeval='val(0)*exp(-3.6*t)':c=same,volume=2.0[whoosh];
 [thump][crack][roar][whoosh]amix=inputs=4:normalize=0:weights=1 0.6 0.9 0.55,
   acompressor=threshold=-15dB:ratio=5:attack=1:release=90,
   alimiter=limit=0.94,volume=1.3,aformat=channel_layouts=mono
" $Q battery_launch.ogg

# ---------- PLANET HIT (deep impact on the world) ----------
ffmpeg -y -v error -filter_complex "
 sine=frequency=44:duration=0.55[s];
 [s]volume=3.6,afade=t=out:st=0.04:d=0.5[sub];
 anoisesrc=d=0.5:c=brown:a=1:s=707[n];
 [n]lowpass=f=320,aeval='val(0)*exp(-5*t)':c=same,volume=5[body];
 anoisesrc=d=0.05:c=white:a=1:s=2[c];
 [c]bandpass=f=900:width_type=q:w=1.2,volume=1.4,afade=t=out:st=0.005:d=0.045[tick];
 [sub][body][tick]amix=inputs=3:normalize=0:weights=1 0.8 0.3,
   acompressor=threshold=-15dB:ratio=5:attack=1:release=150,
   alimiter=limit=0.95,volume=1.35,aformat=channel_layouts=mono
" $Q planet_hit.ogg

echo "--- generated ---"
for f in turret_shot turret_shot_b sentinel_shot explosion explosion_b explosion_big missile_launch battery_launch planet_hit; do
  d=$(ffprobe -v error -show_entries format=duration -of csv=p=0 $f.ogg)
  v=$(ffmpeg -i $f.ogg -af volumedetect -f null /dev/null 2>&1 | grep -oE 'mean_volume: [-0-9.]+|max_volume: [-0-9.]+' | tr '\n' ' ')
  printf "  %-16s %.2fs  %s\n" "$f" "$d" "$v"
done
