using System.Collections.Generic;
using Godot;

namespace Sentinel.Sim;

public sealed partial class SimWorld
{
    private int _pendingDrafts;
    private readonly List<string> _runCards = new();
    private readonly List<int> _draftOptions = new();
    private DetRandom _draftRng;

    public bool HasPendingDraft => _pendingDrafts > 0 && _draftOptions.Count > 0;
    public IReadOnlyList<int> DraftOptionIndices => _draftOptions;
    public IReadOnlyList<string> RunCards => _runCards;

    private void OfferDraftAfterWave()
    {
        _pendingDrafts++;
        if (_draftOptions.Count == 0) GenerateDraftOptions();
    }

    private void GenerateDraftOptions()
    {
        _draftOptions.Clear();
        var cards = Cfg.Cards;
        if (cards.Count == 0) { _pendingDrafts = 0; return; }

        int want = Mathf.Clamp(Mods.CardDraftOptions, 2, 4);
        // weighted pick without replacement; already-picked cards excluded
        var pool = new List<int>();
        var weights = new List<int>();
        for (int i = 0; i < cards.Count; i++)
        {
            if (_runCards.Contains(cards[i].Id)) continue;
            pool.Add(i);
            weights.Add(Mathf.Max(1, cards[i].Weight));
        }
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

    private void PickCard(int cardConfigIndex)
    {
        if (!_draftOptions.Contains(cardConfigIndex)) return;
        var card = Cfg.Cards[cardConfigIndex];

        foreach (var kv in card.Effects) Mods.ApplyEffect(kv.Key, kv.Value);
        if (card.IntegrityBonus != 0f)
        {
            PlanetIntegrityMax += card.IntegrityBonus;
            PlanetIntegrity = Mathf.Min(PlanetIntegrityMax, PlanetIntegrity + card.IntegrityBonus);
        }
        if (card.HullBonus != 0f)
        {
            Hero.MaxHull += card.HullBonus;
            Hero.Hull = Mathf.Min(Hero.MaxHull, Hero.Hull + card.HullBonus);
        }

        _runCards.Add(card.Id);
        _pendingDrafts = Mathf.Max(0, _pendingDrafts - 1);
        _draftOptions.Clear();
        if (_pendingDrafts > 0) GenerateDraftOptions();
        Events.Push(SimEventKind.AbilityCast, Vector2.Zero, 0f, -3);
    }
}
