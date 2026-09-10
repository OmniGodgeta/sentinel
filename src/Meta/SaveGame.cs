using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Sentinel.Meta;

/// <summary>
/// Account-wide persistent progress. One file at user://save.json. There is no
/// premium-currency field and there never will be (design-spec §9) — every
/// currency here is earned only by playing.
/// </summary>
public sealed class SaveGame
{
    private const string Path = "user://save.json";
    private const int Version = 1;

    public int Version_ { get; set; } = Version;

    // currencies
    public double ResearchData { get; set; }
    public double ExoticAlloy { get; set; }
    public int SentinelCores { get; set; }
    public double Xp { get; set; }

    // per-mission record: id -> best star tier (0..3), and whether ever cleared
    public Dictionary<string, MissionRecord> Missions { get; set; } = new();

    // research: node id -> ranks bought; branch -> chosen capstone id
    public Dictionary<string, int> ResearchRanks { get; set; } = new();
    public Dictionary<string, string> Capstones { get; set; } = new();

    // ability leveling: ability id -> level (1..20); "<id>:<milestone>" -> branch choice
    public Dictionary<string, int> AbilityLevels { get; set; } = new();
    public Dictionary<string, string> AbilityBranches { get; set; } = new();

    // level-up upgrade cards picked (Planet-Defense-TD style meta progression)
    public List<string> LevelCards { get; set; } = new();

    // codex entries revealed by first-hand encounter (enemies seen, kit unlocked, lore earned)
    public List<string> CodexSeen { get; set; } = new();

    /// <summary>Reveal a codex entry. Returns true if it was newly revealed.</summary>
    public bool Discover(string codexId)
    {
        if (string.IsNullOrEmpty(codexId) || CodexSeen.Contains(codexId)) return false;
        CodexSeen.Add(codexId);
        return true;
    }

    // equipped ability loadout (ids). Length tracks hero-level slot count.
    public List<string> Loadout { get; set; } = new() { "kinetic_barrage", "aegis_barrier", "overdrive" };

    public int EndlessBest { get; set; }
    public string WeeklyId { get; set; } = "";                     // ISO week the WeeklyBest belongs to ("2026-W37")
    public int WeeklyBest { get; set; }                            // deepest wave this week

    // ---- Shop (design-spec §10) — cosmetics bought with Commendations, earned by playing ----
    /// <summary>Shop item ids the player has purchased. Free items are implicitly owned.
    /// The Commendations balance is derived (total earned from progress − total spent here),
    /// so it is always self-consistent and needs no separate stored counter.</summary>
    public List<string> ShopOwned { get; set; } = new();
    public int AscensionTier { get; set; }                         // currently selected (0 = off)
    public Dictionary<string, int> MissionBestTier { get; set; } = new();   // mission id -> highest ascension tier cleared
    public Settings Options { get; set; } = new();

    [JsonIgnore] public int CommanderLevel => XpToCommanderLevel(Xp);

    public sealed class MissionRecord
    {
        public bool Cleared { get; set; }
        public int Stars { get; set; }
        public int Attempts { get; set; }
    }

    public sealed class Settings
    {
        public int Speed { get; set; } = 1;
        public bool Haptics { get; set; } = true;
        public bool ReduceFlash { get; set; }
        public bool AutoSlowOnBoss { get; set; } = true;
        public float SfxVolume { get; set; } = 0.85f;
        public float MusicVolume { get; set; } = 0.55f;
        public bool Muted { get; set; }
        public string PlanetSkin { get; set; } = "earth";
        public string HullSkin { get; set; } = "standard";
        public string OrdnancePalette { get; set; } = "ember";
    }

    // ---- commander level curve (slow, steady; XP is easy to earn, levels are not) ----
    public static int XpToCommanderLevel(double xp)
    {
        // level n needs cumulative  50 * n^1.6
        int lvl = 1;
        while (50.0 * Mathf.Pow(lvl + 1, 1.6f) <= xp) lvl++;
        return lvl;
    }

    public MissionRecord Record(string missionId)
    {
        if (!Missions.TryGetValue(missionId, out var r))
        {
            r = new MissionRecord();
            Missions[missionId] = r;
        }
        return r;
    }

    public bool IsUnlocked(string missionId, IReadOnlyList<string> arcOrder)
    {
        int idx = -1;
        for (int i = 0; i < arcOrder.Count; i++) if (arcOrder[i] == missionId) { idx = i; break; }
        if (idx <= 0) return true;                       // first mission always open
        return Record(arcOrder[idx - 1]).Cleared;         // previous mission cleared
    }

    // ---- io ----
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public static SaveGame Load()
    {
        if (!FileAccess.FileExists(Path)) return new SaveGame();
        try
        {
            using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
            var s = JsonSerializer.Deserialize<SaveGame>(f.GetAsText(), Opts);
            return s ?? new SaveGame();
        }
        catch (System.Exception ex)
        {
            GD.PushError($"SaveGame: load failed, starting fresh: {ex.Message}");
            return new SaveGame();
        }
    }

    public void Save()
    {
        try
        {
            using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
            f.StoreString(JsonSerializer.Serialize(this, Opts));
        }
        catch (System.Exception ex)
        {
            GD.PushError($"SaveGame: save failed: {ex.Message}");
        }
    }
}
