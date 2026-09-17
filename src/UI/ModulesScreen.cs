using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// PLANET MODULES — permanent, Research-Data-funded upgrades (extra integrity,
/// faster repair, softer spawns, bigger rewards, a stronger shield), PDTD-style:
/// levelling a module doesn't require it to be equipped, but only up to
/// <see cref="Meta.Progression.ModuleSlots"/> equipped modules contribute their
/// effect in a run. Reached from the Upgrades hub, not the main menu.
/// </summary>
public sealed partial class ModulesScreen : CanvasLayer
{
    public AppRoot App = null!;

    private Label _wallet = null!;
    private VBoxContainer _list = null!;

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.5f, PlanetScale = 0.55f, NebulaAlpha = 0.10f });

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -320, OffsetRight = 320, OffsetTop = 34, OffsetBottom = -12,
        };
        root.AddThemeConstantOverride("separation", 10);
        root.Theme = UiTheme.Instance;
        AddChild(root);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(210, 84) };
        back.Pressed += () => App.ShowUpgrades();
        head.AddChild(back);
        var title = new Label { Text = "  PLANET MODULES", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontOverride("font", UiTheme.Display);
        title.AddThemeFontSizeOverride("font_size", 34);
        head.AddChild(title);

        _wallet = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _wallet.AddThemeFontSizeOverride("font_size", 21);
        _wallet.AddThemeColorOverride("font_color", UiTheme.Accent2);
        root.AddChild(_wallet);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_list);

        Rebuild();
    }

    private void Rebuild()
    {
        var p = App.Prog;
        int equipped = App.Save.EquippedModules.Count;
        _wallet.Text = $"◇ {F(App.Save.ResearchData)}  Research Data      —      {equipped}/{p.ModuleSlots}  slots equipped";
        foreach (Node c in _list.GetChildren()) c.QueueFree();

        foreach (var def in App.Cfg.Modules.Modules)
        {
            int lvl = p.ModuleLevel(def.Id);
            bool eq = p.IsModuleEquipped(def.Id);
            _list.AddChild(MakeCard(def, lvl, eq));
        }
    }

    private Control MakeCard(Config.ModuleDef def, int lvl, bool equipped)
    {
        var accent = equipped ? new Color(0.4f, 0.95f, 0.6f) : UiTheme.Accent;
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, equipped ? 0.12f : 0.07f),
            BorderColor = new Color(accent, equipped ? 0.85f : 0.5f),
            BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 12, ContentMarginBottom = 12,
        });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        p.AddChild(row);

        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 3);
        row.AddChild(col);
        var nm = new Label { Text = $"{def.Name.ToUpperInvariant()}    ·    LV {lvl}/{def.MaxLevel}" + (equipped ? "   ✓ EQUIPPED" : "") };
        nm.AddThemeFontOverride("font", UiTheme.Display);
        nm.AddThemeFontSizeOverride("font_size", 24);
        nm.AddThemeColorOverride("font_color", accent.Lightened(0.25f));
        col.AddChild(nm);
        var tx = new Label { Text = def.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.7f) };
        tx.AddThemeFontSizeOverride("font_size", 18);
        col.AddChild(tx);

        var btnCol = new VBoxContainer();
        btnCol.AddThemeConstantOverride("separation", 6);
        row.AddChild(btnCol);

        double cost = App.Prog.ModuleCost(def);
        if (cost >= 0)
        {
            var buy = new Button { Text = $"Upgrade\n◇ {cost:0}", CustomMinimumSize = new Vector2(196, 81) };
            buy.AddThemeFontSizeOverride("font_size", 20);
            buy.Disabled = App.Save.ResearchData < cost;
            buy.Pressed += () => { if (App.Prog.BuyModule(def)) { Click(); Rebuild(); } };
            btnCol.AddChild(buy);
        }
        else
        {
            btnCol.AddChild(new Label { Text = "MAX LEVEL", HorizontalAlignment = HorizontalAlignment.Center });
        }

        var equipBtn = new Button
        {
            Text = equipped ? "Unequip" : "Equip",
            CustomMinimumSize = new Vector2(196, 70),
            Disabled = lvl <= 0 || (!equipped && App.Save.EquippedModules.Count >= App.Prog.ModuleSlots),
        };
        equipBtn.AddThemeFontSizeOverride("font_size", 20);
        equipBtn.Pressed += () => { if (App.Prog.ToggleEquipModule(def.Id)) { Click(); Rebuild(); } };
        btnCol.AddChild(equipBtn);

        return p;
    }

    private static void Click() => Sentinel.Audio.AudioManager.Instance?.Click();
    private static string F(double v) => Mathf.FloorToInt((float)v).ToString();
}
