using System.Collections.Generic;
using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// Menu: progress totals, a simple ability loadout picker, and the arc-1 mission
/// list (locked until the previous mission is cleared).
/// </summary>
public sealed partial class MenuScreen : CanvasLayer
{
    public AppRoot App = null!;

    private VBoxContainer _loadoutRow = null!;
    private VBoxContainer _missionList = null!;
    private Label _totals = null!;

    public override void _Ready()
    {
        Layer = 5;

        var bg = new ColorRect { Color = new Color(0.03f, 0.03f, 0.05f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var root = new VBoxContainer { CustomMinimumSize = new Vector2(440, 0) };
        root.AddThemeConstantOverride("separation", 9);
        center.AddChild(root);

        var title = new Label { Text = "SENTINEL", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 40);
        root.AddChild(title);
        var sub = new Label { Text = "no ads · no purchases · ever", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.5f) };
        root.AddChild(sub);

        _totals = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _totals.AddThemeFontSizeOverride("font_size", 13);
        root.AddChild(_totals);

        root.AddChild(new HSeparator());
        var lh = new Label { Text = "LOADOUT  (tap a slot to change)" };
        lh.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(lh);
        _loadoutRow = new VBoxContainer();
        _loadoutRow.AddThemeConstantOverride("separation", 5);
        root.AddChild(_loadoutRow);

        root.AddChild(new HSeparator());
        var mh = new Label { Text = $"ARC 1 — {App.Cfg.Arc.Name}" };
        mh.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(mh);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 300) };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        root.AddChild(scroll);
        _missionList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _missionList.AddThemeConstantOverride("separation", 6);
        _missionList.CustomMinimumSize = new Vector2(440, 0);
        scroll.AddChild(_missionList);

        var wipe = new Button { Text = "reset progress", Modulate = new Color(1, 1, 1, 0.4f), CustomMinimumSize = new Vector2(0, 30) };
        wipe.Pressed += () =>
        {
            App.Save.ResearchData = App.Save.Xp = App.Save.ExoticAlloy = 0;
            App.Save.SentinelCores = 0;
            App.Save.Missions.Clear();
            App.Save.Save();
            Rebuild();
        };
        root.AddChild(wipe);

        Rebuild();
    }

    private void Rebuild()
    {
        var s = App.Save;
        _totals.Text = $"Commander {s.CommanderLevel}   ·   RD {Mathf.FloorToInt((float)s.ResearchData)}   ·   Alloy {Mathf.FloorToInt((float)s.ExoticAlloy)}   ·   Cores {s.SentinelCores}";

        foreach (Node c in _loadoutRow.GetChildren()) c.QueueFree();
        int slots = App.Cfg.Hero.AbilitySlots;
        while (s.Loadout.Count < slots) s.Loadout.Add(App.Cfg.AbilityOrder[0]);
        for (int i = 0; i < slots; i++)
        {
            int slot = i;
            var cur = App.Cfg.Ability(s.Loadout[i]);
            var btn = new Button { Text = $"{i + 1}.  {cur.Name}   ({cur.Role})", CustomMinimumSize = new Vector2(440, 36) };
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.Pressed += () => CycleLoadout(slot);
            _loadoutRow.AddChild(btn);
        }

        foreach (Node c in _missionList.GetChildren()) c.QueueFree();
        var arcOrder = new List<string>();
        foreach (var m in App.Cfg.Arc.Missions) arcOrder.Add(m.Id);
        foreach (var m in App.Cfg.Arc.Missions)
        {
            var rec = s.Record(m.Id);
            bool open = s.IsUnlocked(m.Id, arcOrder);
            string stars = rec.Stars > 0 ? new string('●', rec.Stars) + new string('○', 3 - rec.Stars) : "○○○";
            var btn = new Button
            {
                Text = open ? $"{m.Name}      {(rec.Cleared ? stars : "· not cleared")}" : $"🔒   {m.Name}",
                Disabled = !open,
                CustomMinimumSize = new Vector2(440, 46),
            };
            btn.AddThemeFontSizeOverride("font_size", 13);
            string file = m.File, id = m.Id;
            btn.Pressed += () => App.StartMission(file, id);
            _missionList.AddChild(btn);
        }
    }

    private void CycleLoadout(int slot)
    {
        var order = App.Cfg.AbilityOrder;
        int cur = 0;
        for (int i = 0; i < order.Count; i++) if (order[i] == App.Save.Loadout[slot]) { cur = i; break; }
        for (int step = 1; step <= order.Count; step++)
        {
            string cand = order[(cur + step) % order.Count];
            if (!App.Save.Loadout.Contains(cand)) { App.Save.Loadout[slot] = cand; break; }
        }
        App.Save.Save();
        Rebuild();
    }
}
