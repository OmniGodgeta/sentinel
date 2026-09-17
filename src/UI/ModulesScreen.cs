using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// PLANET MODULES — PDTD's module grid: six slots, each module carrying a tier (T1-T3,
/// derived from its level) and a level, upgraded by spending chips from the Armory. The
/// layout follows PDTD's module screen (a grid of tier-badged slot cards over the chip
/// pool) in this project's own visual language rather than PDTD's chrome.
/// Only equipped modules contribute their effect in a run.
/// </summary>
public sealed partial class ModulesScreen : CanvasLayer
{
    public AppRoot App = null!;

    private Label _wallet = null!;
    private GridContainer _grid = null!;
    private VBoxContainer _chipRow = null!;

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.5f, PlanetScale = 0.55f, NebulaAlpha = 0.10f });

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -500, OffsetRight = 500, OffsetTop = 34, OffsetBottom = -12,
        };
        root.AddThemeConstantOverride("separation", 10);
        root.Theme = UiTheme.Instance;
        AddChild(MenuFrame.Around(root));
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
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        root.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(body);

        _grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 12);
        _grid.AddThemeConstantOverride("v_separation", 12);
        body.AddChild(_grid);

        _chipRow = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _chipRow.AddThemeConstantOverride("separation", 8);
        body.AddChild(_chipRow);

        Rebuild();
    }

    private void Rebuild()
    {
        App.RefreshProgression();
        var p = App.Prog;
        int equipped = App.Save.EquippedModules.Count;
        _wallet.Text = $"◆ {App.Chips.ChipPoints()} chip points      —      {equipped}/{p.ModuleSlots} slots equipped";

        foreach (Node c in _grid.GetChildren()) c.QueueFree();
        foreach (var def in App.Cfg.Modules.Modules)
            _grid.AddChild(SlotCard(def, p.ModuleLevel(def.Id), p.IsModuleEquipped(def.Id)));

        foreach (Node c in _chipRow.GetChildren()) c.QueueFree();
        var hdr = new Label { Text = "CHIPS" };
        hdr.AddThemeFontOverride("font", UiTheme.Display);
        hdr.AddThemeFontSizeOverride("font_size", 22);
        hdr.AddThemeColorOverride("font_color", UiTheme.Accent);
        _chipRow.AddChild(hdr);

        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 8);
        flow.AddThemeConstantOverride("v_separation", 8);
        _chipRow.AddChild(flow);

        bool any = false;
        foreach (var chip in App.Chips.Archetypes)
            foreach (var td in App.Chips.Tiers)
            {
                int n = App.Chips.Count(chip.Id, td.Tier);
                if (n <= 0) continue;
                any = true;
                flow.AddChild(ChipPip(chip.Name, td.Name, td.Color, n));
            }
        if (!any)
        {
            var none = new Label { Text = "No chips — open chests in the Armory.", Modulate = new Color(1, 1, 1, 0.5f) };
            none.AddThemeFontSizeOverride("font_size", 19);
            flow.AddChild(none);
        }

        var toArmory = new Button { Text = "◆  Go to Armory (chests / merge)", CustomMinimumSize = new Vector2(0, 74) };
        toArmory.AddThemeFontSizeOverride("font_size", 22);
        toArmory.Pressed += () => { Click(); App.ShowChips(); };
        _chipRow.AddChild(toArmory);
    }

    /// <summary>One module slot, PDTD-style: tier badge, level, a pip strip for progress
    /// through the current tier, and the chip cost of the next level.</summary>
    private Control SlotCard(Config.ModuleDef def, int lvl, bool equipped)
    {
        int tier = Sentinel.Meta.Progression.ModuleTier(lvl);
        var accent = tier switch
        {
            0 => new Color(0.45f, 0.48f, 0.56f),
            1 => new Color(0.42f, 0.78f, 0.95f),
            2 => new Color(0.72f, 0.45f, 0.95f),
            _ => new Color(0.98f, 0.78f, 0.30f),
        };

        var p = new PanelContainer { CustomMinimumSize = new Vector2(0, 210) };
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, equipped ? 0.13f : 0.06f),
            BorderColor = new Color(accent, equipped ? 0.9f : 0.45f),
            BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 12,
        });

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 4);
        p.AddChild(col);

        var top = new HBoxContainer();
        col.AddChild(top);
        var badge = new Label { Text = tier == 0 ? "—" : $"◈ T{tier}" };
        badge.AddThemeFontOverride("font", UiTheme.Display);
        badge.AddThemeFontSizeOverride("font_size", 22);
        badge.AddThemeColorOverride("font_color", accent.Lightened(0.25f));
        top.AddChild(badge);
        top.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var lvLbl = new Label { Text = lvl <= 0 ? "not installed" : $"Lv.{lvl}" };
        lvLbl.AddThemeFontSizeOverride("font_size", 21);
        lvLbl.Modulate = new Color(1, 1, 1, 0.85f);
        top.AddChild(lvLbl);

        var nm = new Label { Text = def.Name.ToUpperInvariant() };
        nm.AddThemeFontOverride("font", UiTheme.Display);
        nm.AddThemeFontSizeOverride("font_size", 24);
        nm.AddThemeColorOverride("font_color", accent.Lightened(0.3f));
        col.AddChild(nm);

        var tx = new Label { Text = def.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.65f) };
        tx.AddThemeFontSizeOverride("font_size", 17);
        col.AddChild(tx);

        // pip strip: progress through the current tier's ten levels
        var pips = new HBoxContainer();
        pips.AddThemeConstantOverride("separation", 3);
        int inTier = lvl <= 0 ? 0 : (lvl - 1) % Sentinel.Meta.Progression.ModuleLevelsPerTier + 1;
        for (int i = 0; i < Sentinel.Meta.Progression.ModuleLevelsPerTier; i++)
            pips.AddChild(new ColorRect
            {
                CustomMinimumSize = new Vector2(14, 8),
                Color = i < inTier ? accent : new Color(1, 1, 1, 0.12f),
            });
        col.AddChild(pips);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        col.AddChild(row);

        int cost = App.Prog.ModuleChipCost(def);
        var up = new Button
        {
            Text = cost < 0 ? "MAX" : $"Upgrade  ◆ {cost}",
            CustomMinimumSize = new Vector2(0, 66),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Disabled = cost < 0 || App.Chips.ChipPoints() < cost,
        };
        up.AddThemeFontSizeOverride("font_size", 20);
        up.Pressed += () => { if (App.Prog.UpgradeModuleWithChips(def, App.Chips)) { Click(); Rebuild(); } };
        row.AddChild(up);

        var eq = new Button
        {
            Text = equipped ? "Unequip" : "Equip",
            CustomMinimumSize = new Vector2(150, 66),
            Disabled = lvl <= 0 || (!equipped && App.Save.EquippedModules.Count >= App.Prog.ModuleSlots),
        };
        eq.AddThemeFontSizeOverride("font_size", 20);
        eq.Pressed += () => { if (App.Prog.ToggleEquipModule(def.Id)) { Click(); Rebuild(); } };
        row.AddChild(eq);

        return p;
    }

    private static Control ChipPip(string name, string tierName, string hex, int count)
    {
        var accent = Color.FromHtml(hex);
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, 0.12f), BorderColor = new Color(accent, 0.55f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 6,
        });
        var l = new Label { Text = $"{tierName} {name}  ×{count}" };
        l.AddThemeFontSizeOverride("font_size", 17);
        l.AddThemeColorOverride("font_color", accent.Lightened(0.3f));
        p.AddChild(l);
        return p;
    }

    private static void Click() => Sentinel.Audio.AudioManager.Instance?.Click();
}
