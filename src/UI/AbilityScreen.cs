using Godot;
using Sentinel.Game;
using Sentinel.Meta;

namespace Sentinel.UI;

/// <summary>
/// The 12 Sentinel abilities: level each 1–20 with Sentinel Cores, pick a branch
/// modifier at 5/10/15/20, and equip up to the hero-level slot count.
/// </summary>
public sealed partial class AbilityScreen : CanvasLayer
{
    public AppRoot App = null!;

    private Label _wallet = null!;
    private VBoxContainer _list = null!;

    public override void _Ready()
    {
        Layer = 6;
        var bg = new ColorRect { Color = new Color(0.03f, 0.03f, 0.05f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(bg);

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -235, OffsetRight = 235, OffsetTop = 14, OffsetBottom = -12,
        };
        root.AddThemeConstantOverride("separation", 8);
        root.Theme = UiTheme.Instance;
        AddChild(root);

        var head = new HBoxContainer();
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(90, 34) };
        back.Pressed += () => App.ShowMenu();
        head.AddChild(back);
        var title = new Label { Text = "  SENTINEL PROTOCOLS" };
        title.AddThemeFontSizeOverride("font_size", 20);
        head.AddChild(title);

        _wallet = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _wallet.AddThemeFontSizeOverride("font_size", 13);
        root.AddChild(_wallet);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        root.AddChild(scroll);
        _list = new VBoxContainer { CustomMinimumSize = new Vector2(465, 0) };
        _list.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(_list);

        Rebuild();
    }

    private void Rebuild()
    {
        App.RefreshProgression();
        var p = App.Prog;
        var s = App.Save;
        int slots = p.AbilitySlots;
        int equipped = System.Math.Min(s.Loadout.Count, slots);
        _wallet.Text = $"Sentinel Cores {s.SentinelCores}   ·   Hero L{p.Hero}   ·   {equipped}/{slots} slots equipped";

        foreach (Node c in _list.GetChildren()) c.QueueFree();

        foreach (var id in App.Cfg.AbilityOrder)
        {
            var def = App.Cfg.Ability(id);
            int lvl = p.AbilityLevel(id);
            bool isEquipped = s.Loadout.Contains(id);

            var panel = new PanelContainer();
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 3);
            panel.AddChild(col);

            var top = new HBoxContainer();
            col.AddChild(top);
            var nm = new Label { Text = $"{def.Name}   ·  L{lvl}/20   ({def.Role})", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            nm.AddThemeFontSizeOverride("font_size", 13);
            top.AddChild(nm);

            var eq = new Button
            {
                Text = isEquipped ? "equipped" : "equip",
                ToggleMode = true, ButtonPressed = isEquipped,
                CustomMinimumSize = new Vector2(88, 30),
                Disabled = !isEquipped && s.Loadout.Count >= slots,
            };
            eq.AddThemeFontSizeOverride("font_size", 11);
            eq.Pressed += () => ToggleEquip(id, slots);
            top.AddChild(eq);

            int cost = p.AbilityLevelCost(id);
            var lvlBtn = new Button
            {
                Text = cost < 0 ? "max level" : $"level up → L{lvl + 1}   ({cost} Cores)",
                Disabled = cost < 0 || s.SentinelCores < cost,
                CustomMinimumSize = new Vector2(0, 30),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            lvlBtn.AddThemeFontSizeOverride("font_size", 11);
            lvlBtn.Pressed += () => { if (p.LevelAbility(id)) Rebuild(); };
            col.AddChild(lvlBtn);

            // branch pick at the highest milestone reached without a choice
            for (int ms = 5; ms <= 20; ms += 5)
            {
                if (lvl < ms) break;
                string choice = p.AbilityBranchChoice(id, ms);
                var br = new HBoxContainer();
                col.AddChild(br);
                var lbl = new Label { Text = $"  L{ms}:", VerticalAlignment = VerticalAlignment.Center };
                lbl.AddThemeFontSizeOverride("font_size", 10);
                br.AddChild(lbl);
                foreach (var opt in new[] { "Potency", "Tempo" })
                {
                    string o = opt;
                    var ob = new Button
                    {
                        Text = opt + (opt == "Potency" ? " +effect" : " −cooldown"),
                        ToggleMode = true, ButtonPressed = choice == o,
                        CustomMinimumSize = new Vector2(150, 26),
                    };
                    ob.AddThemeFontSizeOverride("font_size", 10);
                    ob.Pressed += () => { p.SetAbilityBranch(id, ms, o); Rebuild(); };
                    br.AddChild(ob);
                }
            }
            _list.AddChild(panel);
        }
    }

    private void ToggleEquip(string id, int slots)
    {
        var lo = App.Save.Loadout;
        if (lo.Contains(id)) { if (lo.Count > 1) lo.Remove(id); }
        else if (lo.Count < slots) lo.Add(id);
        App.Save.Save();
        Rebuild();
    }
}
