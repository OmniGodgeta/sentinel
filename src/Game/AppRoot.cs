using System.Collections.Generic;
using Godot;
using Sentinel.Config;
using Sentinel.Meta;
using Sentinel.UI;

namespace Sentinel.Game;

/// <summary>
/// Application shell. Owns the save file and the config, and swaps between the
/// menu and a running mission. Banks mission rewards into the save.
/// </summary>
public sealed partial class AppRoot : Node
{
    public static AppRoot Instance { get; private set; } = null!;

    public ConfigDb Cfg { get; private set; } = null!;
    public ResearchDb Research { get; private set; } = null!;
    public SaveGame Save { get; private set; } = null!;
    public Progression Prog { get; private set; } = null!;
    public Shop Shop { get; private set; } = null!;

    private Node? _current;

    public override void _Ready()
    {
        Instance = this;
        Cfg = ConfigDb.Load();
        Research = ResearchDb.Load();
        Save = SaveGame.Load();
        Prog = new Progression(Save, Research, Cfg);
        Shop = new Shop(Save, Cfg);
        Sentinel.Audio.AudioManager.Instance?.SetVolume(Save.Options.SfxVolume, Save.Options.Muted);
        Sentinel.Audio.MusicPlayer.Instance?.SetVolume(Save.Options.MusicVolume, Save.Options.Muted);
        ShowMenu();
    }

    public void RefreshProgression()
    {
        Prog = new Progression(Save, Research, Cfg);
        Shop = new Shop(Save, Cfg);
        SyncCodex();
    }

    /// <summary>Reveal codex entries for the premise, and for whatever kit is now unlocked.</summary>
    private void SyncCodex()
    {
        bool any = false;
        foreach (var e in Cfg.AllCodex)
            if (e.Category == "world") any |= Save.Discover(e.Id);
        foreach (var id in Cfg.TurretOrder)
            if (Prog.IsTurretUnlocked(id)) any |= Save.Discover(Cfg.Turret(id).CodexId);
        foreach (var id in Cfg.AbilityOrder)
            if (Prog.IsAbilityUnlocked(id)) any |= Save.Discover(Cfg.Ability(id).CodexId);
        if (any) Save.Save();
    }

    /// <summary>Reveal codex entries for every enemy that can appear in a mission (incl. carrier broods).</summary>
    private void DiscoverMissionCodex(string missionFile)
    {
        try { DiscoverMissionCodex(Cfg.LoadMission(missionFile)); }
        catch { }
    }

    private void DiscoverMissionCodex(MissionDef m)
    {

        var ids = new HashSet<string>(m.EndlessRoster);
        foreach (var w in m.Waves)
            foreach (var g in w.Groups)
                ids.Add(g.Enemy);
        foreach (var id in m.Roster.Keys) ids.Add(id);
        if (m.Boss.Length > 0) ids.Add(m.Boss);
        foreach (var id in new List<string>(ids))
            if (Cfg.HasEnemy(id) && Cfg.Enemy(id).SpawnEnemy is { Length: > 0 } brood)
                ids.Add(brood);

        bool any = false;
        foreach (var id in ids)
            if (Cfg.HasEnemy(id)) any |= Save.Discover(Cfg.Enemy(id).CodexId);
        if (any) Save.Save();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut || what == NotificationWMGoBackRequest)
            Save.Save();
    }

    public void ShowMenu()
    {
        Sentinel.Audio.MusicPlayer.Instance?.PlayMenu();
        RefreshProgression();
        if (Prog.PendingLevelUps > 0)
        {
            var lv = new LevelUpScreen { App = this };
            lv.Done += ShowMenu;
            SwapTo(lv);
            return;
        }
        SwapTo(new MenuScreen { App = this });
    }
    public void ShowLevels() => SwapTo(new StarMapScreen { App = this });
    public void ShowResearch() => SwapTo(new ResearchScreen { App = this });
    public void ShowAbilities() => SwapTo(new AbilityScreen { App = this });
    public void ShowCodex() => SwapTo(new CodexScreen { App = this });
    public void ShowSettings() => SwapTo(new SettingsScreen { App = this });
    public void ShowShop() => SwapTo(new ShopScreen { App = this });

    /// <summary>Resolve the equipped ability loadout to unlocked ids + their per-ability effect/cd multipliers.</summary>
    private (string[] loadout, float[] eff, float[] cd) ResolveLoadout()
    {
        int slots = Prog.AbilitySlots;
        Save.Loadout.RemoveAll(a => !Prog.IsAbilityUnlocked(a));
        foreach (var a in Progression.BaseAbilities)
            if (Save.Loadout.Count < slots && !Save.Loadout.Contains(a)) Save.Loadout.Add(a);
        if (Save.Loadout.Count == 0) Save.Loadout.Add(Progression.BaseAbilities[0]);
        var loadout = Save.Loadout.GetRange(0, System.Math.Min(slots, Save.Loadout.Count)).ToArray();
        var eff = new float[loadout.Length];
        var cd = new float[loadout.Length];
        for (int i = 0; i < loadout.Length; i++)
        {
            eff[i] = Prog.AbilityEffectMult(loadout[i]);
            cd[i] = Prog.AbilityCdMult(loadout[i]);
        }
        return (loadout, eff, cd);
    }

    /// <summary>This week's shared endless run with its rotating twist.</summary>
    public void StartWeekly()
    {
        RefreshProgression();
        var wk = Sentinel.Meta.WeeklyChallenge.Current();
        if (Save.WeeklyId != wk.Id) { Save.WeeklyId = wk.Id; Save.WeeklyBest = 0; Save.Save(); }

        var m = Cfg.LoadMission("res://data/missions/endless.json") with
        {
            Id = "weekly", Name = wk.Title, Intro = wk.MutatorBlurb, Seed = wk.Seed,
        };
        DiscoverMissionCodex(m);

        var (loadout, eff, cd) = ResolveLoadout();
        var mods = Prog.BuildModifiers();
        foreach (var kv in wk.Twist.PlayerEffects) mods.ApplyEffect(kv.Key, kv.Value);

        var g = new GameRoot
        {
            MissionOverride = m,
            MissionPath = "res://data/missions/endless.json",
            EquippedAbilities = loadout,
            AbilityEffect = eff,
            AbilityCd = cd,
            Mods = mods,
            Ascension = wk.Twist,
            StartSpeed = Save.Options.Speed,
        };
        g.MissionEnded += o => OnMissionEnded(o, 0);
        g.ExitToMenu += ShowMenu;
        Save.Record("weekly").Attempts++;
        Sentinel.Audio.MusicPlayer.Instance?.PlayGame();
        SwapTo(g);
    }

    public void StartMission(string missionFile, string missionId)
    {
        RefreshProgression();
        DiscoverMissionCodex(missionFile);
        var (loadout, eff, cd) = ResolveLoadout();

        // ascension only applies to arc missions, and only up to what's unlocked
        int tier = 0;
        Config.AscensionTierDef? asc = null;
        if (missionId != "endless" && Save.AscensionTier > 0 && Save.AscensionTier <= Prog.AscensionMax)
        {
            tier = Save.AscensionTier;
            asc = Cfg.Ascension.Find(a => a.Tier == tier);
        }
        var mods = Prog.BuildModifiers();
        if (asc != null)
            foreach (var kv in asc.PlayerEffects) mods.ApplyEffect(kv.Key, kv.Value);

        var g = new GameRoot
        {
            MissionPath = missionFile,
            EquippedAbilities = loadout,
            AbilityEffect = eff,
            AbilityCd = cd,
            Mods = mods,
            Ascension = asc,
            StartSpeed = Save.Options.Speed,
        };
        g.MissionEnded += o => OnMissionEnded(o, tier);
        g.ExitToMenu += ShowMenu;
        Save.Record(missionId).Attempts++;
        Sentinel.Audio.MusicPlayer.Instance?.PlayGame();
        SwapTo(g);
    }

    private void OnMissionEnded(MissionOutcome o, int ascensionTier)
    {
        var rec = Save.Record(o.MissionId);
        bool firstClear = o.Won && !rec.Cleared;

        Save.ResearchData += o.ResearchData;
        Save.Xp += o.Xp;
        Save.SentinelCores += o.Cores;

        if (o.MissionId == "endless")
        {
            if (o.WavesCleared > Save.EndlessBest) Save.EndlessBest = o.WavesCleared;
            Save.Save();
            return;
        }

        if (o.MissionId == "weekly")
        {
            var wk = Sentinel.Meta.WeeklyChallenge.Current();
            if (Save.WeeklyId != wk.Id) { Save.WeeklyId = wk.Id; Save.WeeklyBest = 0; }
            if (o.WavesCleared > Save.WeeklyBest) Save.WeeklyBest = o.WavesCleared;
            Save.Save();
            return;
        }

        if (o.Won)
        {
            rec.Cleared = true;
            int stars = 1 + (o.PlanetIntegrityPct >= 0.75f ? 1 : 0) + (o.HeroSurvived ? 1 : 0);
            if (stars > rec.Stars) rec.Stars = stars;
            if (firstClear) Save.ExoticAlloy += 5;
            if (ascensionTier > Save.MissionBestTier.GetValueOrDefault(o.MissionId, 0))
                Save.MissionBestTier[o.MissionId] = ascensionTier;
        }
        Save.Save();
    }

    // remember the current speed choice whenever it changes mid-mission
    public void RememberSpeed(int s) { Save.Options.Speed = s; }

    private void SwapTo(Node next)
    {
        _current?.QueueFree();
        _current = next;
        AddChild(next);
    }
}
