using System.Collections.Generic;
using Godot;

namespace Sentinel.Sim;

/// <summary>
/// Survival spawn director. Instead of discrete waves, enemies arrive continuously
/// on an escalating curve for the length of the hold. Fully deterministic — every
/// roll comes off <see cref="SimWorld.Rng"/>.
/// </summary>
public sealed partial class SimWorld
{
    // ---- per-run director state (reset in Load / BeginWave) ----
    private int _survRewardMark;        // whole minutes already paid out
    private float _survSpawnAccum;      // fractional enemies owed
    private bool _survBossSpawned;

    // in-run "commander level" — climbs from kill XP; each level pops an upgrade card
    private float _runXp;
    private int _runLevel = 1;
    public int RunLevel => _runLevel;
    public float RunXp => _runXp;
    /// <summary>cumulative XP for the start of a level — L1 = 0.</summary>
    private static float XpForRunLevel(int n) => n <= 1 ? 0f : 120f * Mathf.Pow(n - 1, 1.5f);
    public float RunXpFloor => XpForRunLevel(_runLevel);
    public float RunXpCeil => XpForRunLevel(_runLevel + 1);

    internal void GainRunXp(float amount)
    {
        _runXp += amount;
        while (_runXp >= XpForRunLevel(_runLevel + 1) && _runLevel < 99)
        {
            _runLevel++;
            OfferDraftAfterWave();   // "commander promotion" — pick an upgrade
        }
    }

    private struct SurvRosterEntry
    {
        public int DefIndex;
        public int Weight;
        public float UnlockFrac;   // fraction of the hold before this type appears
        public float Cost;         // spawn "budget" cost — heavier ships come slower
    }
    private readonly List<SurvRosterEntry> _survRoster = new();

    // rough threat weight / spawn cost by id — heavies are rarer and pricier
    private static (int weight, float cost) SurvThreat(string id) => id switch
    {
        "skiff" => (60, 1f),
        "interceptor" => (26, 1.6f),
        "leech" => (14, 2.2f),
        "phase_runner" => (14, 2.6f),
        "hauler" => (22, 4.5f),
        "aegis_cruiser" => (20, 4.5f),
        "bombard" => (18, 4f),
        "warden" => (12, 5f),
        "carrier" => (9, 8f),
        "siege_crawler" => (12, 6f),
        _ => (12, 3f),
    };

    private void BuildSurvivalRoster()
    {
        _survRoster.Clear();
        if (!Mission.Survival) return;

        // explicit roster wins; otherwise fall back to the endless roster, then to
        // "everything this mission resolved".
        IEnumerable<KeyValuePair<string, float>> src;
        if (Mission.Roster.Count > 0) src = Mission.Roster;
        else
        {
            var d = new Dictionary<string, float>();
            foreach (var id in Mission.EndlessRoster) d[id] = 0f;
            if (d.Count == 0)
                foreach (var kv in _enemyDefIndex) d[kv.Key] = 0f;
            src = d;
        }

        foreach (var (id, frac) in src)
        {
            if (!_enemyDefIndex.TryGetValue(id, out int di)) continue;
            if (_missionEnemyDefs[di].Class == "boss") continue;   // boss is handled separately
            var (w, c) = SurvThreat(id);
            _survRoster.Add(new SurvRosterEntry { DefIndex = di, Weight = w, UnlockFrac = frac, Cost = c });
        }
    }

    /// <summary>0..1 across a timed hold; for endless, an open-ended ramp.</summary>
    private float SurvRamp
    {
        get
        {
            if (Mission.Duration > 0f) return Mathf.Clamp(PhaseTimer / Mission.Duration, 0f, 1f);
            return Mathf.Min(2.5f, PhaseTimer / Mathf.Max(30f, Cfg.Survival.EndlessRampSeconds));
        }
    }

    /// <summary>Shaped 0..1 escalation across a timed hold: rises with <paramref name="curve"/>
    /// to a peak at <c>LateEaseFrac</c>, then eases back off by <c>LateEaseAmount</c> so the
    /// last stretch of a mission is a hard fight, not an impossible one.</summary>
    internal float SurvEscalation(float frac, float curve)
    {
        var S = Cfg.Survival;
        float peak = Mathf.Clamp(S.LateEaseFrac, 0.4f, 0.95f);
        if (frac <= peak)
            return Mathf.Pow(Mathf.Clamp(frac / peak, 0f, 1f), curve);
        float over = (frac - peak) / Mathf.Max(0.01f, 1f - peak);   // 0..1 across the tail
        return 1f - Mathf.Clamp(S.LateEaseAmount, 0f, 0.9f) * Mathf.SmoothStep(0f, 1f, over);
    }

    private void StepSurvivalDirector()
    {
        // stop feeding new enemies once the timer is up — the player just clears the field
        if (Mission.Duration > 0f && PhaseTimer >= Mission.Duration) return;
        if (_survRoster.Count == 0) return;

        var S = Cfg.Survival;
        float t = PhaseTimer;
        float ramp = SurvRamp;
        float lvl = 1f + Mission.Level * S.LevelSpawnFactor;

        // slow, overlapping surges so it breathes instead of a flat stream
        float surge = 1f
            + S.SurgeA * Mathf.Sin(t * 0.130f)
            + S.SurgeB * Mathf.Sin(t * 0.370f + 1.3f);

        // enemies-per-second target: gentle open, eased so the first ~90s stay
        // light, then climbs and keeps going in endless
        float rampCurve = Mission.Duration > 0f ? SurvEscalation(ramp, S.EpsRampCurve) : ramp;
        float eps = (S.EpsBase + S.EpsRamp * rampCurve) * lvl * Mathf.Max(0.3f, surge)
                    * Mathf.Max(0.25f, _ascCountMult);
        _survSpawnAccum += eps * SimClock.TickDelta;

        // concurrency soft-cap so a stall doesn't turn into a slideshow
        float capRamp = Mission.Duration > 0f ? SurvEscalation(ramp, 1f) : ramp;
        int softCap = S.SoftCapBase + Mathf.RoundToInt(S.SoftCapRamp * capRamp) + Mission.Level * S.SoftCapPerLevel;
        int guard = 0;
        while (_survSpawnAccum >= 1f && _aliveThisWave < softCap && guard++ < 12)
        {
            _survSpawnAccum -= 1f;
            SpawnSurvivalEnemy(ramp);
        }
        if (_aliveThisWave >= softCap) _survSpawnAccum = Mathf.Min(_survSpawnAccum, 4f);

        // one boss, once, in the last stretch of a timed hold
        if (!_survBossSpawned && Mission.Boss.Length > 0 && Mission.Duration > 0f
            && PhaseTimer >= Mission.Duration * Cfg.Survival.BossTimeFrac
            && _enemyDefIndex.TryGetValue(Mission.Boss, out int bdi))
        {
            _survBossSpawned = true;
            float a = Rng.NextAngle();
            Vector2 bp = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * B.SpawnRadius;
            int bi = SpawnEnemy(bdi, bp);
            if (bi >= 0)
            {
                Enemies[bi].Vel = (-bp).Normalized() * EnemyDefAt(bdi).Speed;
                _aliveThisWave++;
            }
        }
    }

    private void SpawnSurvivalEnemy(float ramp)
    {
        // weighted pick among types whose unlock fraction has been reached
        int total = 0;
        for (int i = 0; i < _survRoster.Count; i++)
            if (ramp >= _survRoster[i].UnlockFrac) total += _survRoster[i].Weight;
        if (total <= 0) { total = _survRoster[0].Weight; }

        int roll = Rng.NextInt(total);
        int edi = _survRoster[0].DefIndex;
        for (int i = 0; i < _survRoster.Count; i++)
        {
            if (ramp < _survRoster[i].UnlockFrac) continue;
            if (roll < _survRoster[i].Weight) { edi = _survRoster[i].DefIndex; break; }
            roll -= _survRoster[i].Weight;
        }

        // mostly scattered; sometimes a tight pincer from one bearing
        float ang = Rng.Chance(Cfg.Survival.PincerChance)
            ? Rng.NextFloat(0f, Mathf.Tau) + Rng.NextFloat(-0.25f, 0.25f)
            : Rng.NextAngle();
        Vector2 pos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * B.SpawnRadius;
        int idx = SpawnEnemy(edi, pos);
        if (idx < 0) return;
        Vector2 toCenter = (-pos).Normalized().Rotated(Rng.NextFloat(-0.05f, 0.05f));
        Enemies[idx].Vel = toCenter * EnemyDefAt(edi).Speed;
        _aliveThisWave++;
    }

    /// <summary>Survival end-of-tick: pay out per-minute rewards, offer draft picks,
    /// and win once the timer is up and the field is clear.</summary>
    private void CheckSurvivalEnd()
    {
        // slow self-repair so steady chip damage doesn't inevitably grind the planet
        // down — a real breach still outpaces it
        if (PlanetIntegrity > 0f && PlanetIntegrity < PlanetIntegrityMax)
            PlanetIntegrity = Mathf.Min(PlanetIntegrityMax,
                PlanetIntegrity + PlanetIntegrityMax * Cfg.Survival.SelfRepairFracPerSec * SimClock.TickDelta);

        // per-minute payout (behaves like a "wave cleared" for rewards + cores + draft)
        int minutes = Mathf.FloorToInt(PhaseTimer / 60f);
        int maxMinutes = Mission.Duration > 0f ? Mathf.FloorToInt(Mission.Duration / 60f) : int.MaxValue;
        while (_survRewardMark < minutes && _survRewardMark < maxMinutes)
        {
            _survRewardMark++;
            WavesCleared = _survRewardMark;
            ResearchDataEarned += B.ResearchDataPerWave * Cfg.Survival.RewardRdMult * Mods.ResearchDataGainMult * _ascRewardMult;
            XpEarned += B.XpPerWave * Cfg.Survival.RewardXpMult * Mods.XpGainMult * _ascRewardMult;
            Credits += Mathf.RoundToInt(B.CreditsPerWave * Cfg.Survival.RewardCreditsMult * Mods.WaveIncomeMult);
            if (Cfg.Survival.CoreEveryNMinutes > 0 && _survRewardMark % Cfg.Survival.CoreEveryNMinutes == 0) CoresEarned += 1;
            if (Mods.PlanetRegenPerWaveFrac > 0f)
                PlanetIntegrity = Mathf.Min(PlanetIntegrityMax,
                    PlanetIntegrity + PlanetIntegrityMax * Mods.PlanetRegenPerWaveFrac);
            // cards come from level-ups now (GainRunXp), not the minute mark
        }

        if (Mission.Duration > 0f && PhaseTimer >= Mission.Duration && _aliveThisWave <= 0)
        {
            Phase = SimPhase.Won;
            AccrueRewards(missionClear: true);
            Events.Push(SimEventKind.MissionWon, Vector2.Zero);
        }
    }
}
