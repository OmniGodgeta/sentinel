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

    // equipped ability loadout (ids); research/unlocks come later
    public List<string> Loadout { get; set; } = new() { "kinetic_barrage", "aegis_barrier", "overdrive" };

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
