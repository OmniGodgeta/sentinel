using System.Collections.Generic;
using Godot;

namespace Sentinel.Sim;

/// <summary>
/// The in-fight upgrade draft. Each commander level-up offers a choice of ship
/// weapon cards (Laser Volley, Missile Barrage, Ion Cannon, Yamato Cannon,
/// Plasma Field, Shields Boost); picking one raises that weapon a level.
/// Deterministic — options come off <see cref="_draftRng"/>.
/// </summary>
public sealed partial class SimWorld
{
    private int _pendingDrafts;
    private readonly List<string> _runCards = new();   // weapon ids picked, in order
    private readonly List<int> _draftOptions = new();  // weapon indices on offer
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
        var weapons = Cfg.HeroWeapons;
        if (weapons.Count == 0) { _pendingDrafts = 0; return; }

        var pool = new List<int>();
        var weights = new List<int>();
        for (int i = 0; i < weapons.Count; i++)
        {
            int lvl = _hwLevel[i];
            int max = Mathf.Max(1, weapons[i].MaxLevel);
            if (lvl >= max) continue;
            pool.Add(i);
            // unlock a new system first, then favour the ones lagging behind
            int w = 10 + (lvl == 0 ? 16 : 0) + (max - lvl) * 2;
            weights.Add(Mathf.Max(1, w));
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

    private void PickCard(int weaponIndex)
    {
        if (!_draftOptions.Contains(weaponIndex)) return;

        LevelUpHeroWeapon(weaponIndex);
        _runCards.Add(Cfg.HeroWeapons[weaponIndex].Id);

        _pendingDrafts = Mathf.Max(0, _pendingDrafts - 1);
        _draftOptions.Clear();
        if (_pendingDrafts > 0) GenerateDraftOptions();
        Events.Push(SimEventKind.AbilityCast, Vector2.Zero, 0f, -3);
    }
}
