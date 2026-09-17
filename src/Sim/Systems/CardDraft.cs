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

    private int _pendingDrafts;
    private readonly List<string> _runCards = new();
    private readonly List<int> _draftOptions = new();
    private DetRandom _draftRng;

    public bool HasPendingDraft => _pendingDrafts > 0 && _draftOptions.Count > 0;
    public IReadOnlyList<int> DraftOptionIndices => _draftOptions;
    public IReadOnlyList<string> RunCards => _runCards;

    public bool IsBoostCard(int idx) => idx >= BoostCardBase;
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

    private void OfferDraftAfterWave()
    {
        _pendingDrafts++;
        if (_draftOptions.Count == 0) GenerateDraftOptions();
    }

    private void GenerateDraftOptions()
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
        for (int i = 0; i < ow.Count; i++)
        {
            if (_owLevel[i] == 0 && !roomForNew) continue;
            Consider(OrbitalCardBase + i, _owLevel[i], ow[i].MaxLevel);
        }

        // boost cards — offered from the second draft on, so the opening picks still
        // hand you actual weapons rather than buffs for things you don't own yet
        if (_runCards.Count >= 1)
        {
            var rc = Cfg.RunCards.Cards;
            for (int i = 0; i < rc.Count; i++)
            {
                if (!BoostRequirementMet(rc[i].Requires)) continue;
                pool.Add(BoostCardBase + i);
                weights.Add(Mathf.Max(1, rc[i].Weight));
            }
        }

        if (pool.Count == 0) { _pendingDrafts = 0; return; }

        int want = Mathf.Min(4, pool.Count);
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

        if (IsBoostCard(cardIdx))
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

        _pendingDrafts = Mathf.Max(0, _pendingDrafts - 1);
        _draftOptions.Clear();
        if (_pendingDrafts > 0) GenerateDraftOptions();
        Events.Push(SimEventKind.AbilityCast, Vector2.Zero, 0f, -3);
    }
}
