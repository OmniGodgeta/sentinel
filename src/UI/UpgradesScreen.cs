using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// UPGRADES — the hub for everything that improves the planet and the commander
/// outside of battle: the Research tree, battle Protocols (abilities), and Planet
/// Modules. (Sentinels and the cosmetic Shop have their own buttons.)
/// </summary>
public sealed partial class UpgradesScreen : CanvasLayer
{
    public AppRoot App = null!;

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.5f, PlanetScale = 0.55f, NebulaAlpha = 0.12f });

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -300, OffsetRight = 300, OffsetTop = 34, OffsetBottom = -12,
        };
        root.AddThemeConstantOverride("separation", 14);
        root.Theme = UiTheme.Instance;
        AddChild(root);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(150, 60) };
        back.Pressed += () => App.ShowMenu();
        head.AddChild(back);
        var title = new Label { Text = "  UPGRADES", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontOverride("font", UiTheme.Display);
        title.AddThemeFontSizeOverride("font_size", 24);
        head.AddChild(title);

        var s = App.Save;
        var wallet = new Label
        {
            Text = $"◇ {F(s.ResearchData)} RD      ✷ {s.SentinelCores} Cores      ❖ {F(s.ExoticAlloy)} Alloy",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        wallet.AddThemeFontSizeOverride("font_size", 15);
        wallet.AddThemeColorOverride("font_color", UiTheme.Accent);
        root.AddChild(wallet);

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 20) };
        root.AddChild(spacer);

        root.AddChild(HubButton("⬡  RESEARCH TREE",
            "Deep upgrades to turrets, the battery, sentinels and the planet. Spend Research Data.",
            App.ShowResearch));
        root.AddChild(HubButton("◆  PROTOCOLS",
            "Equip the battle abilities you've recovered from Commander level-ups (Kinetic Barrage, Aegis Barrier…).",
            App.ShowAbilities));
        root.AddChild(HubButton("⬢  PLANET MODULES",
            "Slot-in modules with varied effects — extra integrity, faster repair, spawn dampeners, reward boosters.  (coming soon)",
            null));
        root.AddChild(HubButton("☰  CODEX",
            "Intel on every enemy, weapon and protocol you've encountered.",
            App.ShowCodex));
    }

    private Control HubButton(string title, string desc, System.Action? onPress)
    {
        var p = new PanelContainer();
        var accent = onPress == null ? new Color(1, 1, 1, 0.25f) : UiTheme.Accent;
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, 0.07f),
            BorderColor = new Color(accent, 0.55f),
            BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 14, ContentMarginBottom = 14,
        });
        var btn = new Button { Flat = true, CustomMinimumSize = new Vector2(0, 96) };
        btn.Disabled = onPress == null;
        btn.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Click(); onPress?.Invoke(); };
        p.AddChild(btn);

        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        col.OffsetLeft = 14; col.OffsetRight = -14; col.OffsetTop = 8;
        col.AddThemeConstantOverride("separation", 4);
        btn.AddChild(col);
        var t = new Label { Text = title, MouseFilter = Control.MouseFilterEnum.Ignore };
        t.AddThemeFontOverride("font", UiTheme.Display);
        t.AddThemeFontSizeOverride("font_size", 18);
        t.AddThemeColorOverride("font_color", accent.Lightened(0.3f));
        col.AddChild(t);
        var d = new Label { Text = desc, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0.65f) };
        d.AddThemeFontSizeOverride("font_size", 12);
        col.AddChild(d);
        return p;
    }

    private static string F(double v) => Mathf.FloorToInt((float)v).ToString();
}
