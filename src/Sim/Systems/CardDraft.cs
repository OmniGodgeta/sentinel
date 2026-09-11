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

    private int _pendingDrafts;
    private readonly List<string> _runCards = new();
    private readonly List<int> _draftOptions = new();
    private DetRandom _draftRng;

    public bool HasPendingDraft => _pendingDrafts > 0 && _draftOptions.Count > 0;
    public IReadOnlyList<int> DraftOptionIndices => _draftOptions;
    public IReadOnlyList<string> RunCards => _runCards;

    public bool IsOrbitalCard(int idx) => idx >= OrbitalCardBase;
    public int CardWeaponIndex(int idx) => idx >= OrbitalCardBase ? idx - OrbitalCardBase : idx;

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
        var ow = Cfg.OrbitalWeapons;
        for (int i = 0; i < ow.Count; i++) Consider(OrbitalCardBase + i, _owLevel[i], ow[i].MaxLevel);

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

        if (IsOrbitalCard(cardIdx))
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
