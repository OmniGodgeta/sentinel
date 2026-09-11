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
    private Button[] _presetBtns = System.Array.Empty<Button>();

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.5f, PlanetScale = 0.55f, NebulaAlpha = 0.12f });

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -270, OffsetRight = 270, OffsetTop = 34, OffsetBottom = -12,
        };
        root.AddThemeConstantOverride("separation", 10);
        root.Theme = UiTheme.Instance;
        AddChild(root);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(150, 60) };
        back.Pressed += () => App.ShowMenu();
        head.AddChild(back);
        var title = new Label { Text = "  PROTOCOLS", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        head.AddChild(title);

        _wallet = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _wallet.AddThemeFontSizeOverride("font_size", 15);
        root.AddChild(_wallet);

        var pr = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        pr.AddThemeConstantOverride("separation", 8);
        root.AddChild(pr);
        var plbl = new Label { Text = "Preset ", VerticalAlignment = VerticalAlignment.Center };
        plbl.AddThemeFontSizeOverride("font_size", 16);
        pr.AddChild(plbl);
        _presetBtns = new Button[Meta.SaveGame.PresetCount];
        for (int i = 0; i < _presetBtns.Length; i++)
        {
            int pi = i;
            var b = new Button { Text = $"{i + 1}", ToggleMode = true, CustomMinimumSize = new Vector2(66, 52) };
            b.AddThemeFontSizeOverride("font_size", 18);
            b.Pressed += () => { App.Save.SwitchPreset(pi); Sentinel.Audio.AudioManager.Instance?.Click(); Rebuild(); };
            pr.AddChild(b);
            _presetBtns[i] = b;
        }

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        root.AddChild(scroll);
        _list = new VBoxContainer { CustomMinimumSize = new Vector2(530, 0) };
        _list.AddThemeConstantOverride("separation", 8);
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
        for (int i = 0; i < _presetBtns.Length; i++) _presetBtns[i].ButtonPressed = i == s.ActivePreset;

        foreach (Node c in _list.GetChildren()) c.QueueFree();

        foreach (var id in App.Cfg.AbilityOrder)
        {
            if (!p.IsAbilityUnlocked(id)) continue;   // unlocked via level-up cards
            var def = App.Cfg.Ability(id);
            int lvl = p.AbilityLevel(id);
            bool isEquipped = s.Loadout.Contains(id);

            var panel = new PanelContainer();
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 3);
            panel.AddChild(col);

            var top = new HBoxContainer();
            col.AddChild(top);
            var nm = new Label { Text = $"{def.Name}   ·  L{lvl}/20   ({def.Role})", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
            nm.AddThemeFontSizeOverride("font_size", 16);
            top.AddChild(nm);

            var eq = new Button
            {
                Text = isEquipped ? "equipped" : "equip",
                ToggleMode = true, ButtonPressed = isEquipped,
                CustomMinimumSize = new Vector2(128, 52),
                Disabled = !isEquipped && s.Loadout.Count >= slots,
            };
            eq.AddThemeFontSizeOverride("font_size", 16);
            eq.Pressed += () => ToggleEquip(id, slots);
            top.AddChild(eq);

            int cost = p.AbilityLevelCost(id);
            var lvlBtn = new Button
            {
                Text = cost < 0 ? "max level" : $"level up → L{lvl + 1}   ({cost} Cores)",
                Disabled = cost < 0 || s.SentinelCores < cost,
                CustomMinimumSize = new Vector2(0, 52),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            lvlBtn.AddThemeFontSizeOverride("font_size", 16);
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
                lbl.AddThemeFontSizeOverride("font_size", 16);
                br.AddChild(lbl);
                foreach (var opt in new[] { "Potency", "Tempo" })
                {
                    string o = opt;
                    var ob = new Button
                    {
                        Text = opt + (opt == "Potency" ? " +effect" : " −cooldown"),
                        ToggleMode = true, ButtonPressed = choice == o,
                        CustomMinimumSize = new Vector2(230, 48),
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    };
                    ob.AddThemeFontSizeOverride("font_size", 16);
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
        App.Save.CommitLoadout();
        Rebuild();
    }
}
