using System.Collections.Generic;
using Godot;
using Sentinel.Config;

namespace Sentinel.Meta;

/// <summary>
/// Turns a <see cref="SaveGame"/> plus the research/ability data into the derived
/// numbers the rest of the game needs: level curves, tier/branch gating, the
/// equipped ability slot count, and the aggregated <see cref="ModifierSet"/>.
/// </summary>
public sealed class Progression
{
    private readonly SaveGame _save;
    private readonly ResearchDb _research;
    private readonly ConfigDb _cfg;

    public Progression(SaveGame save, ResearchDb research, ConfigDb cfg)
    {
        _save = save;
        _research = research;
        _cfg = cfg;
    }

    // ---- level curves (both fed by the single Xp pool, different curves) ----
    public static int CommanderLevel(double xp)
    {
        int n = 1;
        while (CmdrXpForLevel(n + 1) <= xp && n < 999) n++;
        return n;
    }
    public static double CmdrXpForLevel(int n) => n <= 1 ? 0 : 120.0 * Mathf.Pow(n - 1, 1.75f);

    public const int HeroLevelCap = 20;
    public static int HeroLevel(double xp)
    {
        int n = 1;
        while (n < HeroLevelCap && HeroXpForLevel(n + 1) <= xp) n++;
        return n;
    }
    public static double HeroXpForLevel(int n) => n <= 1 ? 0 : 260.0 * Mathf.Pow(n - 1, 1.55f);

    public int Commander => CommanderLevel(_save.Xp);
    public int Hero => HeroLevel(_save.Xp);

    public static readonly string[] BaseTurrets = { "autocannon", "flak" };
    public static readonly string[] BaseAbilities = { "kinetic_barrage", "aegis_barrier", "overdrive" };

    /// <summary>Equipped ability slots: 3 base, +1 at hero 8, +1 at hero 20, + level-card bonuses.</summary>
    public int AbilitySlots
    {
        get
        {
            int h = Hero;
            int fromHero = h >= 20 ? 5 : h >= 8 ? 4 : 3;
            int fromCards = 0;
            foreach (var id in _save.LevelCards)
                if (_cfg.LevelCard(id) is { } c && c.Effects.TryGetValue("ability_slot", out float v))
                    fromCards += (int)v;
            return System.Math.Min(6, fromHero + fromCards);
        }
    }

    // ---- level-up cards ----
    /// <summary>Card picks owed: one per Commander level past 1, minus what's been picked.</summary>
    public int PendingLevelUps => System.Math.Max(0, Commander - 1 - _save.LevelCards.Count);

    private int Picks(string id)
    {
        int n = 0;
        foreach (var x in _save.LevelCards) if (x == id) n++;
        return n;
    }

    public bool CardAvailable(LevelCardDef c)
    {
        int max = c.Repeatable ? System.Math.Max(1, c.MaxPicks) : 1;
        if (Picks(c.Id) >= max) return false;
        if (c.UnlockTurret != "" && (Picks(c.Id) > 0)) return false;
        if (c.UnlockAbility != "" && (Picks(c.Id) > 0)) return false;
        return true;
    }

    /// <summary>Deterministic 3-card offer for the current pick, from the mission-agnostic
    /// pool, seeded by how many cards have been picked so it's stable while shown.</summary>
    public System.Collections.Generic.List<LevelCardDef> LevelCardOffer(int count = 3)
    {
        var pool = new System.Collections.Generic.List<LevelCardDef>();
        foreach (var c in _cfg.LevelCards) if (CardAvailable(c)) pool.Add(c);
        var rng = new Sim.DetRandom((ulong)(0xC0FFEE + _save.LevelCards.Count * 2654435761u));
        var pick = new System.Collections.Generic.List<LevelCardDef>();
        while (pick.Count < count && pool.Count > 0)
        {
            int i = rng.NextInt(pool.Count);
            pick.Add(pool[i]);
            pool.RemoveAt(i);
        }
        return pick;
    }

    public void PickLevelCard(string id)
    {
        _save.LevelCards.Add(id);
        _save.Save();
    }

    public bool IsTurretUnlocked(string id)
    {
        foreach (var t in BaseTurrets) if (t == id) return true;
        foreach (var cardId in _save.LevelCards)
            if (_cfg.LevelCard(cardId)?.UnlockTurret == id) return true;
        return false;
    }

    public bool IsAbilityUnlocked(string id)
    {
        foreach (var a in BaseAbilities) if (a == id) return true;
        foreach (var cardId in _save.LevelCards)
            if (_cfg.LevelCard(cardId)?.UnlockAbility == id) return true;
        return false;
    }

    public System.Collections.Generic.List<string> UnlockedTurrets()
    {
        var l = new System.Collections.Generic.List<string>();
        foreach (var id in _cfg.TurretOrder) if (IsTurretUnlocked(id)) l.Add(id);
        return l;
    }

    // ---- research gating ----
    public bool BranchOpen(string branch)
    {
        int c = Commander;
        return branch switch
        {
            "armaments" => true,
            "logistics" => true,
            "fortification" => c >= 5,
            "fleet" => c >= 10,
            "sentinel" => c >= 15,
            _ => false,
        };
    }

    public static int TierCommanderGate(int tier) => tier switch
    {
        1 => 1, 2 => 8, 3 => 16, 4 => 25, 5 => 35, 6 => 50, _ => 999,
    };

    public bool TierOpen(string branch, int tier)
    {
        if (!BranchOpen(branch)) return false;
        if (Commander < TierCommanderGate(tier)) return false;
        if (tier == 1) return true;
        // need any 3 ranks bought in the tier below
        int bought = 0;
        foreach (var node in _research.NodesIn(branch, tier - 1))
            bought += Ranks(node.Id);
        return bought >= 3;
    }

    public int Ranks(string nodeId) => _save.ResearchRanks.TryGetValue(nodeId, out int r) ? r : 0;

    /// <summary>Highest ascension tier the player may select: 0 until the whole arc is
    /// cleared at tier 0, then one above the lowest per-mission cleared tier (cap 10).</summary>
    public int AscensionMax
    {
        get
        {
            int lowest = int.MaxValue;
            foreach (var m in _cfg.Arc.Missions)
            {
                if (!_save.Record(m.Id).Cleared) return 0;
                lowest = System.Math.Min(lowest, _save.MissionBestTier.GetValueOrDefault(m.Id, 0));
            }
            return System.Math.Min(10, lowest + 1);
        }
    }

    public bool CanBuy(ResearchNode node, out string reason)
    {
        reason = "";
        if (!node.IsCapstone && Ranks(node.Id) >= node.RankCount) { reason = "maxed"; return false; }
        if (!TierOpen(node.Branch, node.Tier)) { reason = $"tier locked (Cmdr {TierCommanderGate(node.Tier)})"; return false; }
        if (Commander < node.CommanderGate) { reason = $"needs Commander {node.CommanderGate}"; return false; }
        (double rd, double alloy) = NodeCost(node);
        if (_save.ResearchData < rd) { reason = "not enough Research Data"; return false; }
        if (_save.ExoticAlloy < alloy) { reason = "not enough Exotic Alloy"; return false; }
        if (node.IsCapstone && _save.Capstones.ContainsKey(node.Branch) && _save.Capstones[node.Branch] == node.Id)
        { reason = "active"; return false; }
        return true;
    }

    public (double rd, double alloy) NodeCost(ResearchNode node)
    {
        if (node.IsCapstone) return (node.CostBase, node.AlloyCost);
        int rank = Ranks(node.Id);                       // cost of the NEXT rank
        // gentle curve — the player should be buying something every few runs, not
        // grinding 60 missions for one node (session model: always feel progress)
        double mult = Mathf.Pow(1.35f, rank) * Mathf.Pow(1.7f, node.Tier - 1);
        double rd = node.Currency == "alloy" ? 0 : node.CostBase * mult;
        double alloy = node.Currency == "alloy" ? node.AlloyCost * Mathf.Pow(1.35f, rank) * (node.Tier - 4) : 0;
        return (System.Math.Round(rd), System.Math.Round(System.Math.Max(0, alloy)));
    }

    public bool Buy(ResearchNode node)
    {
        if (!CanBuy(node, out _)) return false;
        (double rd, double alloy) = NodeCost(node);
        _save.ResearchData -= rd;
        _save.ExoticAlloy -= alloy;
        if (node.IsCapstone) _save.Capstones[node.Branch] = node.Id;    // free swap: just overwrite
        else _save.ResearchRanks[node.Id] = Ranks(node.Id) + 1;
        _save.Save();
        return true;
    }

    // ---- ability leveling (Sentinel Cores) ----
    public const int AbilityLevelCap = 20;
    public int AbilityLevel(string id) => System.Math.Max(1, _save.AbilityLevels.TryGetValue(id, out int l) ? l : 1);

    public int AbilityLevelCost(string id)
    {
        int lvl = AbilityLevel(id);
        return lvl >= AbilityLevelCap ? -1 : 1 + lvl / 4;      // 1..6 cores as it climbs
    }

    public bool LevelAbility(string id)
    {
        int cost = AbilityLevelCost(id);
        if (cost < 0 || _save.SentinelCores < cost) return false;
        _save.SentinelCores -= cost;
        _save.AbilityLevels[id] = AbilityLevel(id) + 1;
        _save.Save();
        return true;
    }

    public static bool IsMilestone(int level) => level is 5 or 10 or 15 or 20;
    public string AbilityBranchChoice(string id, int level)
        => _save.AbilityBranches.TryGetValue($"{id}:{level}", out var c) ? c : "";
    public void SetAbilityBranch(string id, int level, string choice)
    {
        _save.AbilityBranches[$"{id}:{level}"] = choice;      // free to change
        _save.Save();
    }

    /// <summary>Ability effect multiplier from its level + "Potency" branch picks.</summary>
    public float AbilityEffectMult(string id)
    {
        int lvl = AbilityLevel(id);
        float m = 1f + (lvl - 1) * 0.035f;
        for (int ms = 5; ms <= 20; ms += 5)
            if (lvl >= ms && AbilityBranchChoice(id, ms) == "Potency") m += 0.10f;
        return m;
    }

    /// <summary>Ability cooldown multiplier from its "Tempo" branch picks.</summary>
    public float AbilityCdMult(string id)
    {
        int lvl = AbilityLevel(id);
        float m = 1f;
        for (int ms = 5; ms <= 20; ms += 5)
            if (lvl >= ms && AbilityBranchChoice(id, ms) == "Tempo") m -= 0.09f;
        return Mathf.Max(0.4f, m);
    }

    // ---- the aggregate ----
    public ModifierSet BuildModifiers()
    {
        var m = new ModifierSet { HeroLevel = Hero, AbilitySlots = AbilitySlots };

        // base kit
        foreach (var t in BaseTurrets) m.UnlockedTurrets.Add(t);
        foreach (var a in BaseAbilities) m.UnlockedAbilities.Add(a);

        // level-up cards
        foreach (var id in _save.LevelCards)
        {
            var c = _cfg.LevelCard(id);
            if (c == null) continue;
            if (c.UnlockTurret != "") m.UnlockedTurrets.Add(c.UnlockTurret);
            if (c.UnlockAbility != "") m.UnlockedAbilities.Add(c.UnlockAbility);
            foreach (var (key, v) in c.Effects) m.ApplyEffect(key, v);
        }

        foreach (var node in _research.AllNodes)
        {
            if (node.IsCapstone) continue;
            int r = Ranks(node.Id);
            if (r <= 0) continue;
            foreach (var (key, per) in node.Effects)
                m.ApplyEffect(key, per * r);
        }

        // capstones
        foreach (var (branch, capId) in _save.Capstones)
        {
            var cap = _research.Node(capId);
            if (cap == null) continue;
            foreach (var (key, v) in cap.Effects) m.ApplyEffect(key, v);
            switch (branch)
            {
                case "armaments": m.CapArmaments = capId; break;
                case "fortification": m.CapFortification = capId; break;
                case "fleet": m.CapFleet = capId; break;
                case "sentinel": m.CapSentinel = capId; break;
                case "logistics": m.CapLogistics = capId; break;
            }
        }

        // hero level milestones
        if (Hero >= 14) m.HeroDoubleSalvo = true;
        // small per-level stat bump (spec: each level grants base stat increases)
        float hb = (Hero - 1) * 0.03f;
        m.HeroHullMult += hb;
        m.HeroMissileDamageMult += hb;
        m.HeroMoveSpeedMult += (Hero - 1) * 0.015f;

        return m;
    }
}
