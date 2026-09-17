using System.Collections.Generic;
using Godot;

namespace Sentinel.Sim;

/// <summary>
/// In-run item drops (data/items.json). Killing anything rolls for a drop; a hit picks a
/// rarity by weight and then an item of that rarity, applies its effects to the run's
/// <see cref="Meta.ModifierSet"/>, and records it so the HUD can show what fell.
///
/// Deterministic like everything else in src/Sim/ — every roll comes off
/// <see cref="SimWorld.Rng"/>, never wall-clock or <c>GD.Randf</c>. (The Armory's chest
/// rolls in <c>src/Meta/ChipVault.cs</c> DO use GD.Randf, which is fine: that's the meta
/// layer, outside the determinism rule.)
/// </summary>
public sealed partial class SimWorld
{
    private readonly List<int> _runItems = new();       // indices into Cfg.Items.Items

    /// <summary>Items picked up this run, in the order they dropped.</summary>
    public IReadOnlyList<int> RunItems => _runItems;

    /// <summary>The most recent drop, for the HUD toast: item index and how long the
    /// toast still has to live. −1 = nothing to show.</summary>
    public int LastItemDrop { get; private set; } = -1;
    public float LastItemDropAge { get; private set; }

    private void ResetItemDrops()
    {
        _runItems.Clear();
        LastItemDrop = -1;
        LastItemDropAge = 999f;
    }

    private void StepItemDrops(float dt) => LastItemDropAge += dt;

    /// <summary>Roll a drop for an enemy that just died. <paramref name="cls"/> is the
    /// enemy's class, which selects the drop chance — ordinary kills use the small base
    /// rate, elites and bosses always drop.</summary>
    private void RollItemDrop(string cls, Vector2 pos)
    {
        var db = Cfg.Items;
        if (db.Items.Count == 0 || db.Rarities.Count == 0) return;

        float chance = cls switch
        {
            "miniboss" => db.MinibossDropChance,
            "boss" => db.BossDropChance,
            _ => db.DropChance,
        };
        if (chance <= 0f || Rng.NextFloat() >= chance) return;

        // rarity first (weights are the published drop percentages), then an item of it
        int total = 0;
        foreach (var r in db.Rarities) total += Mathf.Max(0, r.Weight);
        if (total <= 0) return;
        int roll = Rng.NextInt(total);
        string rarity = db.Rarities[^1].Id;
        foreach (var r in db.Rarities)
        {
            int w = Mathf.Max(0, r.Weight);
            if (roll < w) { rarity = r.Id; break; }
            roll -= w;
        }

        // Collect the candidates for that rarity. If a rarity has no items authored,
        // fall back to the whole table rather than silently dropping nothing.
        var pool = new List<int>();
        for (int i = 0; i < db.Items.Count; i++)
            if (db.Items[i].Rarity == rarity) pool.Add(i);
        if (pool.Count == 0)
            for (int i = 0; i < db.Items.Count; i++) pool.Add(i);

        int pick = pool[Rng.NextInt(pool.Count)];
        var item = db.Items[pick];
        foreach (var (key, v) in item.Effects) Mods.ApplyEffect(key, v);
        _runItems.Add(pick);
        LastItemDrop = pick;
        LastItemDropAge = 0f;
        Events.Push(SimEventKind.ItemDropped, pos, 0f, pick);
    }
}
