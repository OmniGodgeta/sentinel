using System.Collections.Generic;
using Godot;
using Sentinel.Config;

namespace Sentinel.Meta;

/// <summary>
/// The Armory chest/chip system (data/chips.json, design-spec's PDTD-style gear layer).
/// Chests are opened with Silver/Gold Keys — earned only by playing (mission/star/endless
/// rewards, see GameRoot's reward hookup), never purchasable. CLAUDE.md's 2026-09-16
/// amendment: no real money in the loop, RNG itself is fine. This class is the only place
/// that rolls chip rewards or merges chips; `Hud`/`ShopScreen`/a future chip screen call
/// into it rather than touching `SaveGame.ChipInventory` directly.
/// </summary>
public sealed class ChipVault
{
    private readonly SaveGame _save;
    private readonly ConfigDb _cfg;
    public ChipVault(SaveGame save, ConfigDb cfg) { _save = save; _cfg = cfg; }

    private ChipsDb D => _cfg.Chips;

    public static string Key(string chipId, int tier) => $"{chipId}:{tier}";

    public int Count(string chipId, int tier) => _save.ChipInventory.GetValueOrDefault(Key(chipId, tier));

    public IEnumerable<ChipDef> Archetypes => D.Chips;
    public IReadOnlyList<ChipTierDef> Tiers => D.Tiers;
    public ChipTierDef? TierDef(int tier) => D.Tiers.Find(t => t.Tier == tier);
    public int MaxTier => D.Tiers.Count == 0 ? 1 : D.Tiers[^1].Tier;

    // ---- chests ----

    /// <summary>Silver chests roll T1/T2 only; Gold chests roll T2/T3, with a small
    /// chance at T4 — mirrors the reference "N more to guarantee Rare/Epic" chest
    /// pattern loosely (a real pity-timer isn't implemented, just weighted odds).</summary>
    private static readonly (int tier, float weight)[] SilverOdds = { (1, 0.70f), (2, 0.30f) };
    private static readonly (int tier, float weight)[] GoldOdds = { (2, 0.55f), (3, 0.38f), (4, 0.07f) };

    private int RollTier((int tier, float weight)[] odds)
    {
        float total = 0f;
        foreach (var (_, w) in odds) total += w;
        float r = GD.Randf() * total;
        foreach (var (tier, w) in odds) { if (r < w) return tier; r -= w; }
        return odds[^1].tier;
    }

    /// <summary>Opens <paramref name="count"/> chests of <paramref name="kind"/>
    /// ("silver"/"gold"), 1 key each. Returns what dropped (chip id, tier) for the UI to
    /// show; does nothing and returns empty if there aren't enough keys.</summary>
    public List<(string chipId, int tier)> OpenChests(string kind, int count)
    {
        var results = new List<(string, int)>();
        int have = kind == "gold" ? _save.GoldKeys : _save.SilverKeys;
        count = Mathf.Min(count, have);
        if (count <= 0 || D.Chips.Count == 0) return results;

        if (kind == "gold") _save.GoldKeys -= count; else _save.SilverKeys -= count;
        var odds = kind == "gold" ? GoldOdds : SilverOdds;
        for (int i = 0; i < count; i++)
        {
            var chip = D.Chips[GD.RandRange(0, D.Chips.Count - 1)];
            int tier = RollTier(odds);
            string k = Key(chip.Id, tier);
            _save.ChipInventory[k] = _save.ChipInventory.GetValueOrDefault(k) + 1;
            results.Add((chip.Id, tier));
        }
        _save.Save();
        return results;
    }

    // ---- merging ----

    public bool CanMerge(string chipId, int tier)
    {
        var td = TierDef(tier);
        if (td == null || td.MergeCost <= 0) return false;   // already max tier
        return Count(chipId, tier) >= td.MergeCost;
    }

    /// <summary>Consumes MergeCost copies of (chipId, tier) and adds 1 of (chipId, tier+1).</summary>
    public bool Merge(string chipId, int tier)
    {
        if (!CanMerge(chipId, tier)) return false;
        var td = TierDef(tier)!;
        string lo = Key(chipId, tier), hi = Key(chipId, tier + 1);
        _save.ChipInventory[lo] -= td.MergeCost;
        if (_save.ChipInventory[lo] <= 0) _save.ChipInventory.Remove(lo);
        _save.ChipInventory[hi] = _save.ChipInventory.GetValueOrDefault(hi) + 1;
        PruneInvalidEquips();
        return true;
    }

    /// <summary>Merges every chip archetype as far up as it'll go, lowest tier first so a
    /// big pile of T1s cascades up through T2/T3 in one tap ("Quick Merge").</summary>
    public int QuickMergeAll()
    {
        int merges = 0;
        foreach (var chip in D.Chips)
            foreach (var td in D.Tiers)
                while (Merge(chip.Id, td.Tier)) merges++;
        if (merges > 0) _save.Save();
        return merges;
    }

    // ---- spending chips on module upgrades (PDTD's module/chip loop) ----

    /// <summary>What one chip of a tier is worth when spent. A tier-N chip counts for as
    /// much as the chips it would take to merge up to it, so spending a T3 is never worse
    /// than merging down.</summary>
    public int ChipValue(int tier)
    {
        int v = 1;
        foreach (var td in D.Tiers)
        {
            if (td.Tier >= tier) break;
            v *= Mathf.Max(1, td.MergeCost);
        }
        return v;
    }

    /// <summary>Total spendable chip value held, counting every archetype and tier.</summary>
    public int ChipPoints()
    {
        int total = 0;
        foreach (var (key, count) in _save.ChipInventory)
        {
            var parts = key.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out int tier)) total += ChipValue(tier) * count;
        }
        return total;
    }

    /// <summary>Spend <paramref name="points"/> worth of chips, lowest tier first so the
    /// good ones are kept back. Returns false (spending nothing) if you can't cover it.</summary>
    public bool SpendChips(int points)
    {
        if (points <= 0) return true;
        if (ChipPoints() < points) return false;

        foreach (var td in D.Tiers)   // tiers are listed low -> high
        {
            foreach (var chip in D.Chips)
            {
                string k = Key(chip.Id, td.Tier);
                int have = _save.ChipInventory.GetValueOrDefault(k);
                if (have <= 0) continue;
                int worth = ChipValue(td.Tier);
                while (have > 0 && points > 0)
                {
                    have--; points -= worth;
                    _save.ChipInventory[k] = have;
                }
                if (_save.ChipInventory[k] <= 0) _save.ChipInventory.Remove(k);
                if (points <= 0) break;
            }
            if (points <= 0) break;
        }
        PruneInvalidEquips();
        _save.Save();
        return true;
    }

    // ---- equip ----

    public bool IsEquipped(string chipId, int tier) => _save.EquippedChips.Contains(Key(chipId, tier));

    public bool Equip(string chipId, int tier)
    {
        string k = Key(chipId, tier);
        if (_save.EquippedChips.Contains(k)) return true;
        if (_save.EquippedChips.Count >= D.EquipSlots) return false;
        _save.EquippedChips.Add(k);
        _save.Save();
        return true;
    }

    public void Unequip(string chipId, int tier)
    {
        _save.EquippedChips.Remove(Key(chipId, tier));
        _save.Save();
    }

    /// <summary>Drop any equipped chip the player no longer owns enough of (e.g. after a
    /// merge consumed the equipped tier) — call after any inventory-changing op that
    /// isn't itself Equip/Unequip.</summary>
    public void PruneInvalidEquips()
    {
        _save.EquippedChips.RemoveAll(k =>
        {
            var parts = k.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int tier)) return true;
            return Count(parts[0], tier) <= 0;
        });
    }
}
