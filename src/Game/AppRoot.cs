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

    private Node? _current;

    public override void _Ready()
    {
        Instance = this;
        Cfg = ConfigDb.Load();
        Research = ResearchDb.Load();
        Save = SaveGame.Load();
        Prog = new Progression(Save, Research, Cfg);
        ShowMenu();
    }

    public void RefreshProgression() => Prog = new Progression(Save, Research, Cfg);

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut || what == NotificationWMGoBackRequest)
            Save.Save();
    }

    public void ShowMenu()
    {
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
    public void ShowLevels() => SwapTo(new LevelSelectScreen { App = this });
    public void ShowResearch() => SwapTo(new ResearchScreen { App = this });
    public void ShowAbilities() => SwapTo(new AbilityScreen { App = this });
    public void ShowCodex() => SwapTo(new CodexScreen { App = this });

    public void StartMission(string missionFile, string missionId)
    {
        RefreshProgression();
        int slots = Prog.AbilitySlots;
        // keep only unlocked abilities, top up from the base kit, then clamp to slots
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
