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
    /// <summary>The previous good save, rolled on every write so a corrupt or truncated
    /// save.json costs one session instead of the whole profile.</summary>
    private const string BackupPath = "user://save.json.bak";
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

    /// <summary>Persistent per-orbital-weapon meta level (bought on the Sentinels screen).</summary>
    public Dictionary<string, int> OrbitalMeta { get; set; } = new();
    /// <summary>Persistent Planet Shield level (bought outside battles).</summary>
    public int PlanetShieldLevel { get; set; } = 0;
    /// <summary>Commendations spent outside the cosmetic Shop (sentinels, planet shield).</summary>
    public int CommendationsSpent { get; set; } = 0;

    /// <summary>Persistent per-module level (bought with Research Data on the Planet
    /// Modules screen). Levelling a module doesn't require it to be equipped.</summary>
    public Dictionary<string, int> ModuleLevels { get; set; } = new();
    /// <summary>Which modules are actively equipped (capped at <c>ModulesDb.Slots</c>) —
    /// only equipped modules contribute their effect in a run.</summary>
    public List<string> EquippedModules { get; set; } = new();

    /// <summary>Chip inventory (data/chips.json) — key is <c>"{chip_id}:{tier}"</c> (e.g.
    /// "chip_damage:2"), value is how many of that chip+tier are owned. Chests (Meta/
    /// ChipVault.cs) add to this; merging N of one tier removes them and adds 1 of the
    /// next tier up.</summary>
    public Dictionary<string, int> ChipInventory { get; set; } = new();
    /// <summary>Which chip+tier keys (same "{chip_id}:{tier}" shape as ChipInventory) are
    /// equipped right now — capped at <c>ChipsDb.EquipSlots</c>, only equipped chips
    /// contribute their effect in a run.</summary>
    public List<string> EquippedChips { get; set; } = new();
    /// <summary>Lifetime career totals, shown on the Commander screen. Banked in
    /// AppRoot.OnMissionEnded from each run's RunStats — win or lose.</summary>
    public long LifetimeKills { get; set; }
    public double LifetimeDamage { get; set; }
    public int LifetimeBossKills { get; set; }
    public int StageClears { get; set; }          // every successful clear, re-runs included
    public int MissionsPlayed { get; set; }

    /// <summary>Armory chest keys — earned by playing (mission/star/endless rewards),
    /// never purchasable with real money. See CLAUDE.md's 2026-09-16 amendment.</summary>
    public int SilverKeys { get; set; } = 0;
    public int GoldKeys { get; set; } = 0;

    /// <summary>Ultimate Alloy — spent on the per-sentinel ultimate upgrade tracks
    /// (Upgrades -> Planet -> Ultimate). Earned from Gold chests.</summary>
    public int UltimateAlloy { get; set; } = 0;
    /// <summary>Unobtainium Alloy — the rare one. Same tracks, far scarcer: it only
    /// appears on the deepest levels of each track.</summary>
    public int UnobtainiumAlloy { get; set; } = 0;
    /// <summary>Per-sentinel ultimate upgrade levels, key "{weapon_id}:{track_id}".</summary>
    public Dictionary<string, int> UltimateLevels { get; set; } = new();

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

    // equipped ability loadout (ids) — the ACTIVE preset. Length tracks hero-level slot count.
    // Starts empty: abilities are recovered from Commander level-ups, then equipped in Protocols.
    public List<string> Loadout { get; set; } = new();

    // three saved loadout presets (design-spec §14) so a balance pass isn't re-equipping
    // four abilities before every one of fifty test runs. Slot ActivePreset mirrors Loadout.
    public const int PresetCount = 3;
    public List<List<string>> LoadoutPresets { get; set; } = new();
    public int ActivePreset { get; set; }

    /// <summary>One-time fixups for ids removed/renamed in later versions of the game, so an
    /// existing save doesn't end up pointing at content that no longer exists. Idempotent —
    /// safe to call on every load. Add an entry here (not a full migration framework) whenever
    /// a weapon/ability id is retired or renamed; this is a personal single-save build, not a
    /// live service, so this stays intentionally light.</summary>
    private void MigrateRemovedIds()
    {
        // v0.26.x: Kinetic Barrage (commander ability) retired outright.
        AbilityLevels.Remove("kinetic_barrage");
        AbilityBranches.Remove("kinetic_barrage");
        Loadout.RemoveAll(id => id == "kinetic_barrage");
        foreach (var preset in LoadoutPresets) preset.RemoveAll(id => id == "kinetic_barrage");

        // v0.31.2: the Shop's invented worlds became the real bodies they were always
        // standing in for, and the skin ids moved with the names so the equirectangular
        // map files line up. Without this an existing save's PlanetSkin points at an id
        // with no map and silently falls back to the flat sprite.
        if (Options.PlanetSkin is "ice") Options.PlanetSkin = "europa";
        else if (Options.PlanetSkin is "volcanic") Options.PlanetSkin = "titan";
        else if (Options.PlanetSkin is "gas") Options.PlanetSkin = "triton";
        else if (Options.PlanetSkin is "shattered") Options.PlanetSkin = "pluto";

        // v0.31.2: Salvage Beacon retired — a utility ability whose effect never read in
        // play, and it was taking a bottom-bar slot the sentinel cards want.
        AbilityLevels.Remove("salvage_beacon");
        AbilityBranches.Remove("salvage_beacon");
        Loadout.RemoveAll(id => id == "salvage_beacon");
        foreach (var preset in LoadoutPresets) preset.RemoveAll(id => id == "salvage_beacon");
        LevelCards.RemoveAll(id => id == "pro_salvage");

        // v0.26.x: Orbital Cannon (Railgun analog) retired; Orbital Laser renamed to Beam.
        OrbitalMeta.Remove("orbital_cannon");
        if (OrbitalMeta.Remove("orbital_laser", out int beamLevel))
            OrbitalMeta["beam"] = System.Math.Max(beamLevel, OrbitalMeta.GetValueOrDefault("beam"));
    }

    /// <summary>Ensure three presets exist and the active one matches Loadout.</summary>
    public void NormalizePresets()
    {
        MigrateRemovedIds();
        while (LoadoutPresets.Count < PresetCount) LoadoutPresets.Add(new List<string>());
        if (LoadoutPresets.Count > PresetCount) LoadoutPresets.RemoveRange(PresetCount, LoadoutPresets.Count - PresetCount);
        ActivePreset = System.Math.Clamp(ActivePreset, 0, PresetCount - 1);
        bool allEmpty = LoadoutPresets.TrueForAll(p => p.Count == 0);
        if (allEmpty && Loadout.Count > 0) LoadoutPresets[ActivePreset] = new List<string>(Loadout);
        if (LoadoutPresets[ActivePreset].Count > 0) Loadout = new List<string>(LoadoutPresets[ActivePreset]);
    }

    /// <summary>Write the current Loadout back into the active preset slot.</summary>
    public void CommitLoadout()
    {
        NormalizePresets();
        LoadoutPresets[ActivePreset] = new List<string>(Loadout);
        Save();
    }

    /// <summary>Make preset <paramref name="i"/> active and load it into Loadout.</summary>
    public void SwitchPreset(int i)
    {
        NormalizePresets();
        ActivePreset = System.Math.Clamp(i, 0, PresetCount - 1);
        Loadout = new List<string>(LoadoutPresets[ActivePreset]);
        Save();
    }

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
        if (!FileAccess.FileExists(Path)) { var fresh = new SaveGame(); fresh.NormalizePresets(); return fresh; }
        try
        {
            using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
            var s = JsonSerializer.Deserialize<SaveGame>(f.GetAsText(), Opts);
            s ??= new SaveGame();
            s.NormalizePresets();
            return s;
        }
        catch (System.Exception ex)
        {
            // Never silently hand back an empty save on top of a file that still has the
            // player's progress in it — the next Save() would overwrite it for good.
            // Keep the unreadable file aside, try the last known-good backup, and only
            // start fresh if that fails too.
            GD.PushError($"SaveGame: load failed ({ex.Message}); keeping a copy and trying the backup");
            try { DirAccess.CopyAbsolute(Path, Path + ".corrupt"); } catch { /* best effort */ }
            if (FileAccess.FileExists(BackupPath))
            {
                try
                {
                    using var bf = FileAccess.Open(BackupPath, FileAccess.ModeFlags.Read);
                    var b = JsonSerializer.Deserialize<SaveGame>(bf.GetAsText(), Opts);
                    if (b != null)
                    {
                        GD.Print("SaveGame: recovered from backup");
                        b.NormalizePresets();
                        return b;
                    }
                }
                catch (System.Exception bex) { GD.PushError($"SaveGame: backup unreadable too: {bex.Message}"); }
            }
            var fresh2 = new SaveGame();
            fresh2.NormalizePresets();
            return fresh2;
        }
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, Opts);
            // Roll the previous good file to .bak first. A half-written save (app killed
            // mid-write, storage full) then costs one session rather than the whole
            // profile — Load() falls back to this.
            if (FileAccess.FileExists(Path))
            {
                try { DirAccess.CopyAbsolute(Path, BackupPath); } catch { /* best effort */ }
            }
            using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
            f.StoreString(json);
        }
        catch (System.Exception ex)
        {
            GD.PushError($"SaveGame: save failed: {ex.Message}");
        }
        CloudSave.Instance?.PushSave(JsonSerializer.Serialize(this, Opts));
    }

    /// <summary>For CloudSave: serialize/deserialize without touching user://save.json.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Opts);
    public static SaveGame? FromJson(string json)
    {
        try { var s = JsonSerializer.Deserialize<SaveGame>(json, Opts); s?.NormalizePresets(); return s; }
        catch { return null; }
    }
}
