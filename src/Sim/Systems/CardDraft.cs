using System.Collections.Generic;
using Godot;

namespace Sentinel.Sim;

/// <summary>
/// The in-fight upgrade draft. Each commander level-up offers a choice of 4 cards
/// drawn from one shared pool of ship weapons (Laser Volley, Missile Barrage, …)
/// and planet orbital weapons (Orbital Cannon, Radiation Zone, …). Picking one
/// raises that weapon a level. Deterministic — options come off <see cref="_draftRng"/>.
/// </summary>
public sealed partial class SimWorld
{
    /// <summary>Draft option indices ≥ this are orbital weapons (index − this);
    /// below it they are hero weapons.</summary>
    public const int OrbitalCardBase = 100;

    /// <summary>How many orbital sentinels can be active at once. PDTD runs the planet's
    /// missile battery plus five others; Beyond matches that, so the draft stops offering
    /// new sentinels once five are live (it keeps offering upgrades to those five).</summary>
    public const int MaxActiveSentinels = 5;

    /// <summary>Draft option indices at/above this are boost cards (data/runcards.json) —
    /// PDTD-style percentage buffs and trade-offs rather than a weapon level.</summary>
    public const int BoostCardBase = 200;

    /// <summary>Draft option indices at/above this are PDTD skill cards
    /// (data/skillcards.json), index − this. These replaced the old
    /// one-card-per-orbital-weapon "LV 1 → 2" options: a weapon now has a whole menu of
    /// named upgrades, exactly as in PDTD, and taking one lights a star rather than
    /// bumping a level directly.</summary>
    public const int SkillCardBase = 1000;

    /// <summary>Cards taken this run, by skill-card id, so MaxPicks can be enforced.</summary>
    private readonly Dictionary<string, int> _skillPicks = new();

    /// <summary>Battery upgrade cards taken. The planet's missile battery has no level of
    /// its own, so its cards' NeedLevel gates read off this instead.</summary>
    private int _batteryCardsTaken;

    public bool IsSkillCard(int idx) => idx >= SkillCardBase;
    public Config.SkillCardDef? SkillCard(int idx) =>
        idx >= SkillCardBase && idx - SkillCardBase < Cfg.SkillCards.Cards.Count
            ? Cfg.SkillCards.Cards[idx - SkillCardBase] : null;
    public int SkillCardPicks(string id) => _skillPicks.GetValueOrDefault(id);

    private int _pendingDrafts;
    private readonly List<string> _runCards = new();
    private readonly List<int> _draftOptions = new();
    private DetRandom _draftRng;

    // ---- mini-boss payout ----
    // A mini-boss deals a whole hand face-up instead of the usual pick-one-of-four. You
    // take one, and each card taken rolls for the hand to stay open so you can end up
    // with anywhere from one to the whole hand.
    private int _bossPicksLeft;
    private float _bossCascadeChance;

    /// <summary>The draft on screen is a mini-boss payout — every card is face-up and you
    /// may get to take more than one.</summary>
    public bool IsBossReward => _bossPicksLeft > 0;
    /// <summary>How many more cards this payout will let you take (≥1 while open).</summary>
    public int BossRewardPicksLeft => _bossPicksLeft;

    public bool HasPendingDraft => _pendingDrafts > 0 && _draftOptions.Count > 0;
    public IReadOnlyList<int> DraftOptionIndices => _draftOptions;
    public IReadOnlyList<string> RunCards => _runCards;

    public bool IsBoostCard(int idx) => idx >= BoostCardBase && idx < SkillCardBase;
    public bool IsOrbitalCard(int idx) => idx >= OrbitalCardBase && idx < BoostCardBase;
    public int CardWeaponIndex(int idx) => idx >= OrbitalCardBase ? idx - OrbitalCardBase : idx;
    public Config.RunCardDef? BoostCard(int idx) =>
        idx >= BoostCardBase && idx - BoostCardBase < Cfg.RunCards.Cards.Count ? Cfg.RunCards.Cards[idx - BoostCardBase] : null;

    /// <summary>Is the weapon a boost card needs actually in play? `requires` names either an
    /// orbital weapon Kind or a hero weapon id; blank means always offerable.</summary>
    private bool BoostRequirementMet(string requires)
    {
        if (string.IsNullOrEmpty(requires)) return true;
        for (int i = 0; i < Cfg.OrbitalWeapons.Count; i++)
            if (Cfg.OrbitalWeapons[i].Kind == requires) return _owLevel[i] > 0;
        for (int i = 0; i < Cfg.HeroWeapons.Count; i++)
            if (Cfg.HeroWeapons[i].Id == requires) return _hwLevel[i] > 0;
        return false;
    }

    /// <summary>How many times a boost card has already been taken this run. Picks are
    /// recorded in <see cref="_runCards"/> as "boost:&lt;id&gt;".</summary>
    private int BoostPickCount(string id)
    {
        int n = 0;
        string key = "boost:" + id;
        for (int i = 0; i < _runCards.Count; i++) if (_runCards[i] == key) n++;
        return n;
    }

    private void OfferDraftAfterWave()
    {
        _pendingDrafts++;
        if (_draftOptions.Count == 0) GenerateDraftOptions();
    }

    /// <summary>Deal a mini-boss's payout: <paramref name="cards"/> options face-up, the
    /// first pick guaranteed and each one after that gated on a
    /// <paramref name="cascadeChance"/> roll.</summary>
    private void OpenBossReward(int cards, float cascadeChance)
    {
        if (cards <= 0) return;
        // Don't stomp a payout that's already on screen — stack the picks onto it instead,
        // which is what happens if two mini-bosses die within a frame of each other.
        _bossCascadeChance = Mathf.Clamp(cascadeChance, 0f, 0.95f);
        if (_bossPicksLeft > 0) { _bossPicksLeft++; return; }

        _bossPicksLeft = 1;
        _pendingDrafts++;
        _draftOptions.Clear();
        GenerateDraftOptions(Mathf.Clamp(cards, 1, 6));
    }

    /// <summary>Default hand size. PDTD deals THREE — which is what lets its cards be
    /// big enough to read the art and the effect text at a glance. Four cards on a phone
    /// forced each one narrow enough that the text had to shrink to fit.</summary>
    private const int DefaultDraftOptions = 3;

    private void GenerateDraftOptions(int want = DefaultDraftOptions)
    {
        _draftOptions.Clear();

        var pool = new List<int>();
        var weights = new List<int>();

        void Consider(int cardIdx, int lvl, int max)
        {
            if (lvl >= Mathf.Max(1, max)) return;
            pool.Add(cardIdx);
            weights.Add(Mathf.Max(1, 10 + (lvl == 0 ? 14 : 0) + (max - lvl) * 2));
        }

        var hw = Cfg.HeroWeapons;
        for (int i = 0; i < hw.Count; i++) Consider(i, _hwLevel[i], hw[i].MaxLevel);

        // PDTD's rule: the planet's missile battery is always slot 1, and you may run at
        // most MaxActiveSentinels others. Once that many are live, the draft only offers
        // upgrades to the ones you already have — never a brand-new sentinel.
        var ow = Cfg.OrbitalWeapons;
        int active = 0;
        for (int i = 0; i < ow.Count; i++) if (_owLevel[i] > 0) active++;
        bool roomForNew = active < MaxActiveSentinels;

        // ---- PDTD skill cards ----
        var sc = Cfg.SkillCards.Cards;
        for (int i = 0; i < sc.Count; i++)
        {
            var c = sc[i];
            if (_skillPicks.GetValueOrDefault(c.Id) >= Mathf.Max(1, c.MaxPicks)) continue;

            // "battery" is the planet's own missile battery — always in play, never
            // unlocked or levelled, so only its upgrade cards are ever offerable.
            if (c.Kind == "battery")
            {
                if (c.Unlock) continue;
                if (_batteryCardsTaken < c.NeedLevel) continue;
                pool.Add(SkillCardBase + i);
                weights.Add(Mathf.Max(1, c.Weight / 100));
                continue;
            }

            int w = OrbitalIndexOfKind(c.Kind);
            if (w < 0) continue;
            int lvl = _owLevel[w];

            if (c.Unlock)
            {
                // only offerable while the weapon is out of play and there's a free slot
                if (lvl > 0 || !roomForNew) continue;
                pool.Add(SkillCardBase + i);
                // PDTD weights unlocks enormously (6000 vs 1000) so a new sentinel is the
                // obvious early pick; scaled down here to sit alongside Beyond's own
                // weights rather than swamping them.
                weights.Add(60);
                continue;
            }

            if (lvl <= 0) continue;                              // weapon not in play
            if (lvl < c.NeedLevel) continue;                     // level gate
            if (lvl - 1 < c.NeedStar) continue;                  // promotions gate
            if (lvl >= Mathf.Max(1, ow[w].MaxLevel)) continue;   // maxed
            pool.Add(SkillCardBase + i);
            weights.Add(Mathf.Max(1, c.Weight / 100));
        }

        // boost cards — offered from the second draft on, so the opening picks still
        // hand you actual weapons rather than buffs for things you don't own yet
        if (_runCards.Count >= 1)
        {
            var rc = Cfg.RunCards.Cards;
            for (int i = 0; i < rc.Count; i++)
            {
                if (!BoostRequirementMet(rc[i].Requires)) continue;
                if (rc[i].MaxPicks > 0 && BoostPickCount(rc[i].Id) >= rc[i].MaxPicks) continue;
                pool.Add(BoostCardBase + i);
                weights.Add(Mathf.Max(1, rc[i].Weight));
            }
        }

        if (pool.Count == 0) { _pendingDrafts = 0; return; }

        want = Mathf.Min(want, pool.Count);
        for (int n = 0; n < want && pool.Count > 0; n++)
        {
            int total = 0;
            foreach (int w in weights) total += w;
            int roll = _draftRng.NextInt(total);
            int idx = 0;
            while (roll >= weights[idx]) { roll -= weights[idx]; idx++; }
            _draftOptions.Add(pool[idx]);
            pool.RemoveAt(idx);
            weights.RemoveAt(idx);
        }
    }

    private void PickCard(int cardIdx)
    {
        if (!_draftOptions.Contains(cardIdx)) return;

        if (IsSkillCard(cardIdx))
        {
            var c = SkillCard(cardIdx);
            if (c != null)
            {
                foreach (var (key, v) in c.Effects) Mods.ApplyEffect(key, v);
                _skillPicks[c.Id] = _skillPicks.GetValueOrDefault(c.Id) + 1;
                _runCards.Add("skill:" + c.Id);

                if (c.Kind == "battery") _batteryCardsTaken++;
                else
                {
                    int w = OrbitalIndexOfKind(c.Kind);
                    if (w >= 0)
                    {
                        if (c.Unlock) UnlockOrbitalWeapon(w);
                        else AddOrbitalStar(w);
                    }
                }
            }
        }
        else if (IsBoostCard(cardIdx))
        {
            var bc = BoostCard(cardIdx);
            if (bc != null)
            {
                foreach (var (key, v) in bc.Effects) Mods.ApplyEffect(key, v);
                _runCards.Add("boost:" + bc.Id);
            }
        }
        else if (IsOrbitalCard(cardIdx))
        {
            int w = cardIdx - OrbitalCardBase;
            LevelUpOrbitalWeapon(w);
            _runCards.Add("orbital:" + Cfg.OrbitalWeapons[w].Id);
        }
        else
        {
            LevelUpHeroWeapon(cardIdx);
            _runCards.Add(Cfg.HeroWeapons[cardIdx].Id);
        }

        if (_bossPicksLeft > 0)
        {
            // Taking a card from the payout burns a pick, then rolls to add another.
            // The remaining cards stay on the table, so a cascade is drawn from the same
            // hand rather than a freshly generated one.
            _bossPicksLeft--;
            if (_draftRng.NextFloat() < _bossCascadeChance) _bossPicksLeft++;

            _draftOptions.Remove(cardIdx);
            if (_bossPicksLeft > 0 && _draftOptions.Count > 0)
            {
                Events.Push(SimEventKind.AbilityCast, Vector2.Zero, 0f, -3);
                return;                       // hand stays up for the next pick
            }
            _bossPicksLeft = 0;
        }

        _pendingDrafts = Mathf.Max(0, _pendingDrafts - 1);
        _draftOptions.Clear();
        if (_pendingDrafts > 0) GenerateDraftOptions();
        Events.Push(SimEventKind.AbilityCast, Vector2.Zero, 0f, -3);
    }
}
