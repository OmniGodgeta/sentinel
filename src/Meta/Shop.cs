using System.Collections.Generic;
using Sentinel.Config;

namespace Sentinel.Meta;

/// <summary>
/// The Shop (design-spec §10). Everything here is a cosmetic bought with
/// Commendations, which are earned only by playing — star ratings, Ascension
/// tiers, the weekly run, Codex progress, endless depth. The balance is derived
/// (total earned − total spent), so it is always self-consistent.
/// Nothing here touches combat.
/// </summary>
public sealed class Shop
{
    private readonly SaveGame _save;
    private readonly ConfigDb _cfg;
    private ShopDef D => _cfg.Shop;

    public Shop(SaveGame save, ConfigDb cfg) { _save = save; _cfg = cfg; }

    public static readonly string[] Tabs = { "fleet", "worlds", "ordnance" };
    public static string TabTitle(string tab) => tab switch
    {
        "fleet" => "Fleet Requisition",
        "worlds" => "Worlds",
        "ordnance" => "Ordnance Palettes",
        _ => tab,
    };

    public IEnumerable<ShopItemDef> ItemsIn(string tab)
    {
        foreach (var it in D.Items) if (it.Tab == tab) yield return it;
    }

    // ---- Commendations ----
    public int TotalEarned()
    {
        var c = D.Commendations;
        int total = 0;
        foreach (var rec in _save.Missions.Values) total += rec.Stars * c.PerStar;
        foreach (var tier in _save.MissionBestTier.Values) total += tier * c.PerAscensionTier;
        if (_save.WeeklyBest > 0) total += c.WeeklyComplete;
        total += _save.CodexSeen.Count * c.PerCodexEntry;
        total += (_save.EndlessBest / 120) * c.EndlessPerTwoMinutes;
        return total;
    }

    public int Spent()
    {
        int s = _save.CommendationsSpent;   // sentinel upgrades, planet shield, etc.
        foreach (var id in _save.ShopOwned)
            if (Item(id) is { } it) s += it.Cost;
        return s;
    }

    /// <summary>Never negative — if earned later drops (e.g. the weekly week rolls over)
    /// owned items are kept and the player simply re-earns headroom to buy more.</summary>
    public int Balance => System.Math.Max(0, TotalEarned() - Spent());

    // ---- items ----
    public ShopItemDef? Item(string id) => D.Items.Find(i => i.Id == id);

    public bool Owned(string id)
    {
        var it = Item(id);
        if (it == null) return false;
        return it.Cost == 0 || _save.ShopOwned.Contains(id);
    }

    public bool CanBuy(ShopItemDef it) =>
        it != null && it.Cost > 0 && !_save.ShopOwned.Contains(it.Id) && Balance >= it.Cost;

    public bool Buy(ShopItemDef it)
    {
        if (!CanBuy(it)) return false;
        _save.ShopOwned.Add(it.Id);
        Equip(it);                     // buying equips it
        _save.Save();
        return true;
    }

    // ---- equip ----
    private (string cat, string val) ParseApply(ShopItemDef it)
    {
        int i = it.Apply.IndexOf(':');
        return i < 0 ? (it.Apply, "") : (it.Apply[..i], it.Apply[(i + 1)..]);
    }

    public bool Equipped(ShopItemDef it)
    {
        var (cat, val) = ParseApply(it);
        return cat switch
        {
            "hull" => _save.Options.HullSkin == val,
            "planet" => _save.Options.PlanetSkin == val,
            "ordnance" => _save.Options.OrdnancePalette == val,
            _ => false,
        };
    }

    public void Equip(ShopItemDef it)
    {
        if (!Owned(it.Id)) return;
        var (cat, val) = ParseApply(it);
        switch (cat)
        {
            case "hull": _save.Options.HullSkin = val; break;
            case "planet": _save.Options.PlanetSkin = val; break;
            case "ordnance": _save.Options.OrdnancePalette = val; break;
        }
        _save.Save();
    }
}
