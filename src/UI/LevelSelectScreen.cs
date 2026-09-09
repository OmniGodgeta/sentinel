using System.Collections.Generic;
using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>Arc-1 mission list with unlock gating, star ratings, and the
/// Ascension tier stepper.</summary>
public sealed partial class LevelSelectScreen : CanvasLayer
{
    public AppRoot App = null!;
    private VBoxContainer _list = null!;
    private HBoxContainer _ascRow = null!;
    private Label _hdr = null!;

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground());

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -235, OffsetRight = 235, OffsetTop = 16, OffsetBottom = -14,
        };
        root.AddThemeConstantOverride("separation", 8);
        root.Theme = UiTheme.Instance;
        AddChild(root);

        var head = new HBoxContainer();
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(90, 34) };
        back.Pressed += () => App.ShowMenu();
        head.AddChild(back);
        _hdr = new Label { Text = $"  ARC 1 — {App.Cfg.Arc.Name}", VerticalAlignment = VerticalAlignment.Center };
        _hdr.AddThemeFontSizeOverride("font_size", 18);
        head.AddChild(_hdr);

        _ascRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        root.AddChild(_ascRow);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        root.AddChild(scroll);
        _list = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);

        var wipe = new Button { Text = "reset all progress", Modulate = new Color(1, 1, 1, 0.35f), CustomMinimumSize = new Vector2(0, 28) };
        wipe.Pressed += () =>
        {
            var s = App.Save;
            s.ResearchData = s.Xp = s.ExoticAlloy = 0; s.SentinelCores = 0; s.EndlessBest = 0; s.AscensionTier = 0;
            s.Missions.Clear(); s.ResearchRanks.Clear(); s.Capstones.Clear();
            s.AbilityLevels.Clear(); s.AbilityBranches.Clear(); s.MissionBestTier.Clear();
            s.Loadout = new List<string> { "kinetic_barrage", "aegis_barrier", "overdrive" };
            s.Save(); Rebuild();
        };
        root.AddChild(wipe);

        Rebuild();
    }

    private void Rebuild()
    {
        App.RefreshProgression();
        var s = App.Save;
        var p = App.Prog;

        foreach (Node c in _ascRow.GetChildren()) c.QueueFree();
        int ascMax = p.AscensionMax;
        if (ascMax > 0)
        {
            if (s.AscensionTier > ascMax) s.AscensionTier = ascMax;
            var minus = new Button { Text = "−", CustomMinimumSize = new Vector2(36, 30) };
            minus.Pressed += () => { s.AscensionTier = Mathf.Max(0, s.AscensionTier - 1); s.Save(); Rebuild(); };
            _ascRow.AddChild(minus);
            string nm = s.AscensionTier == 0 ? "off" : App.Cfg.Ascension.Find(a => a.Tier == s.AscensionTier)?.Name ?? "";
            var lbl = new Label { Text = $"  Ascension {s.AscensionTier}/{ascMax} · {nm}  " };
            lbl.AddThemeFontSizeOverride("font_size", 11);
            _ascRow.AddChild(lbl);
            var plus = new Button { Text = "+", CustomMinimumSize = new Vector2(36, 30) };
            plus.Pressed += () => { s.AscensionTier = Mathf.Min(ascMax, s.AscensionTier + 1); s.Save(); Rebuild(); };
            _ascRow.AddChild(plus);
        }

        foreach (Node c in _list.GetChildren()) c.QueueFree();
        var ids = new List<string>();
        foreach (var m in App.Cfg.Arc.Missions) ids.Add(m.Id);
        foreach (var m in App.Cfg.Arc.Missions)
        {
            var rec = s.Record(m.Id);
            bool open = s.IsUnlocked(m.Id, ids);
            string stars = rec.Stars > 0 ? new string('★', rec.Stars) + new string('☆', 3 - rec.Stars) : "☆☆☆";
            var btn = new Button
            {
                Text = open ? $"{m.Name}      {(rec.Cleared ? stars : "· not cleared")}" : $"🔒   {m.Name}",
                Disabled = !open,
                CustomMinimumSize = new Vector2(460, 48),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            btn.AddThemeFontSizeOverride("font_size", 13);
            string file = m.File, id = m.Id;
            btn.Pressed += () => App.StartMission(file, id);
            _list.AddChild(btn);
        }
    }
}
