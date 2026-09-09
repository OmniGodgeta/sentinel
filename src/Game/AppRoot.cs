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
    public SaveGame Save { get; private set; } = null!;

    private Node? _current;

    public override void _Ready()
    {
        Instance = this;
        Cfg = ConfigDb.Load();
        Save = SaveGame.Load();
        ShowMenu();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut || what == NotificationWMGoBackRequest)
            Save.Save();
    }

    public void ShowMenu()
    {
        SwapTo(new MenuScreen { App = this });
    }

    public void StartMission(string missionFile, string missionId)
    {
        var g = new GameRoot
        {
            MissionPath = missionFile,
            EquippedAbilities = Save.Loadout.ToArray(),
            StartSpeed = Save.Options.Speed,
        };
        g.MissionEnded += o => OnMissionEnded(o);
        g.ExitToMenu += ShowMenu;
        Save.Record(missionId).Attempts++;
        SwapTo(g);
    }

    private void OnMissionEnded(MissionOutcome o)
    {
        var rec = Save.Record(o.MissionId);
        bool firstClear = o.Won && !rec.Cleared;

        Save.ResearchData += o.ResearchData;
        Save.Xp += o.Xp;
        Save.SentinelCores += o.Cores;

        if (o.Won)
        {
            rec.Cleared = true;
            int stars = 1 + (o.PlanetIntegrityPct >= 0.75f ? 1 : 0) + (o.HeroSurvived ? 1 : 0);
            if (stars > rec.Stars) rec.Stars = stars;
            if (firstClear) Save.ExoticAlloy += 5;   // mission first-clear alloy (spec §11)
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
