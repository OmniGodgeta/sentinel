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

    /// <summary>How many chips of <paramref name="tier"/> are held across every archetype.
    /// Merging pools the whole tier rather than one archetype at a time — PDTD lets you
    /// fuse any three commons, not three of the *same* common.</summary>
    public int TierCount(int tier)
    {
        int n = 0;
        foreach (var chip in D.Chips) n += Count(chip.Id, tier);
        return n;
    }

    public bool CanMergeTier(int tier)
    {
        var td = TierDef(tier);
        if (td == null || td.MergeCount <= 0) return false;   // already top tier
        return TierCount(tier) >= td.MergeCount;
    }

    /// <summary>Fuse <c>MergeCount</c> chips of <paramref name="tier"/> — any archetypes,
    /// cheapest-stack-first — into ONE chip of the next tier with a RANDOM archetype.
    /// Returns the chip id produced, or null if there weren't enough.
    ///
    /// The random result is deliberate and is PDTD's behaviour: it's what makes merging a
    /// decision (spend three commons for a coin-flip at the rare you want) instead of
    /// bookkeeping. The old build fused three of one archetype into the same archetype,
    /// which meant a pile of the wrong chip stayed the wrong chip forever.</summary>
    public string? MergeTier(int tier)
    {
        if (!CanMergeTier(tier)) return null;
        var td = TierDef(tier)!;

        int need = td.MergeCount;
        // Spend from the largest stacks first so small odd stacks survive as long as
        // possible — a player holding 4x A and 1x B keeps the B.
        var stacks = new List<(string id, int count)>();
        foreach (var chip in D.Chips)
        {
            int c = Count(chip.Id, tier);
            if (c > 0) stacks.Add((chip.Id, c));
        }
        stacks.Sort((a, b) => b.count.CompareTo(a.count));

        foreach (var (id, count) in stacks)
        {
            if (need <= 0) break;
            int take = Mathf.Min(need, count);
            string k = Key(id, tier);
            int left = _save.ChipInventory.GetValueOrDefault(k) - take;
            if (left > 0) _save.ChipInventory[k] = left; else _save.ChipInventory.Remove(k);
            need -= take;
        }

        var got = D.Chips[GD.RandRange(0, D.Chips.Count - 1)];
        string hi = Key(got.Id, tier + 1);
        _save.ChipInventory[hi] = _save.ChipInventory.GetValueOrDefault(hi) + 1;
        PruneInvalidEquips();
        return got.Id;
    }

    /// <summary>Merge everything that can be merged, lowest tier first so a big pile of
    /// commons cascades all the way up in one tap ("Quick Merge"). Returns how many
    /// fusions happened.</summary>
    public int QuickMergeAll()
    {
        int merges = 0;
        foreach (var td in D.Tiers)                 // tiers are listed low -> high
            while (MergeTier(td.Tier) != null) merges++;
        if (merges > 0) _save.Save();
        return merges;
    }

    // ---- legacy migration ----

    /// <summary>Carry a pre-v0.31 save's chips onto the current table.
    ///
    /// v0.29/v0.30 shipped six archetypes over four tiers. The PDTD-accurate rebuild uses
    /// eighteen archetypes over seven, and every old id (<c>chip_damage</c>, …) is gone —
    /// so without this an existing inventory still sits in save.json but matches no
    /// archetype, and the Armory renders nothing. That reads exactly like "I lost all my
    /// chips", which is what prompted this. Mapping is in data/chips.json's
    /// `legacy_tier_map`; old T1/T2/T3/T4 land on Common/Rare/Legendary/Ultimate so a
    /// maxed old chip stays a maxed new one.
    ///
    /// Idempotent: entries already valid on the new table are left alone, so running it
    /// on every load is safe.</summary>
    public bool MigrateLegacyChips()
    {
        var map = D.LegacyTierMap;
        if (map.Chips.Count == 0 || _save.ChipInventory.Count == 0) return false;

        var valid = new HashSet<string>();
        foreach (var c in D.Chips) valid.Add(c.Id);
        var validTier = new HashSet<int>();
        foreach (var t in D.Tiers) validTier.Add(t.Tier);

        var moved = new Dictionary<string, int>();
        var drop = new List<string>();
        foreach (var (key, count) in _save.ChipInventory)
        {
            var parts = key.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int tier)) { drop.Add(key); continue; }
            if (valid.Contains(parts[0]) && validTier.Contains(tier)) continue;   // already current

            if (!map.Chips.TryGetValue(parts[0], out var newId)) { drop.Add(key); continue; }
            int newTier = map.Tiers.TryGetValue(parts[1], out int nt) ? nt : Mathf.Clamp(tier, 1, MaxTier);
            drop.Add(key);
            string nk = Key(newId, newTier);
            moved[nk] = moved.GetValueOrDefault(nk) + count;
        }
        if (drop.Count == 0 && moved.Count == 0) return false;

        foreach (var k in drop) _save.ChipInventory.Remove(k);
        foreach (var (k, n) in moved)
            _save.ChipInventory[k] = _save.ChipInventory.GetValueOrDefault(k) + n;

        // equips point at the old keys too
        for (int i = _save.EquippedChips.Count - 1; i >= 0; i--)
        {
            var parts = _save.EquippedChips[i].Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out int tier)
                && map.Chips.TryGetValue(parts[0], out var newId))
            {
                int newTier = map.Tiers.TryGetValue(parts[1], out int nt) ? nt : tier;
                _save.EquippedChips[i] = Key(newId, newTier);
            }
        }
        PruneInvalidEquips();
        _save.Save();
        GD.Print($"ChipVault: migrated {moved.Count} legacy chip stack(s) onto the 7-tier table");
        return true;
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
            v *= Mathf.Max(1, td.MergeCount);
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

    /// <summary>Is there enough stock strictly below <paramref name="tier"/> to cover
    /// <paramref name="points"/>? Used to avoid smashing an expensive chip for a small
    /// bill while cheap ones are still on the shelf.</summary>
    private bool CheaperStockCovers(int points, int tier)
    {
        int total = 0;
        foreach (var td in D.Tiers)
        {
            if (td.Tier >= tier) break;
            int worth = ChipValue(td.Tier);
            foreach (var chip in D.Chips) total += Count(chip.Id, td.Tier) * worth;
            if (total >= points) return true;
        }
        return total >= points;
    }

    /// <summary>Spend <paramref name="points"/> worth of chips, lowest tier first so the
    /// good ones are kept back. Returns false (spending nothing) if you can't cover it.</summary>
    public bool SpendChips(int points)
    {
        if (points <= 0) return true;
        if (ChipPoints() < points) return false;

        // Lowest tier first so the good chips are kept back, and — importantly — never
        // break into a tier that overshoots while a cheaper one could still cover the
        // bill. The old loop spent whatever stack it reached next, so paying 1 point
        // with only Ultimates in hand burned a 729-value chip for it and returned no
        // change. Now a tier is only touched once everything cheaper is exhausted.
        foreach (var td in D.Tiers)   // tiers are listed low -> high
        {
            int worth = ChipValue(td.Tier);
            foreach (var chip in D.Chips)
            {
                if (points <= 0) break;
                string k = Key(chip.Id, td.Tier);
                int have = _save.ChipInventory.GetValueOrDefault(k);
                if (have <= 0) continue;

                // How many of this tier are actually needed, and no more.
                int want = Mathf.Min(have, (points + worth - 1) / worth);
                // Don't overshoot with an expensive chip while cheaper stock remains:
                // if a smaller tier can still cover what's left, leave this one alone.
                if (worth > points && CheaperStockCovers(points, td.Tier)) continue;

                int left = have - want;
                if (left > 0) _save.ChipInventory[k] = left; else _save.ChipInventory.Remove(k);
                points -= want * worth;
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
