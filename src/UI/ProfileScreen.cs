using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// COMMANDER — the profile screen, opened by tapping the rank badge on the menu.
/// Rank, hero level, campaign progress and lifetime stats. Read-only for now.
/// </summary>
public sealed partial class ProfileScreen : CanvasLayer
{
    public AppRoot App = null!;

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.5f, PlanetScale = 0.55f, NebulaAlpha = 0.14f });

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -500, OffsetRight = 500, OffsetTop = 34, OffsetBottom = -12,
        };
        root.AddThemeConstantOverride("separation", 12);
        root.Theme = UiTheme.Instance;
        AddChild(MenuFrame.Around(root));
        AddChild(root);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(210, 84) };
        back.Pressed += () => App.ShowMenu();
        head.AddChild(back);
        var title = new Label { Text = "  COMMANDER", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontOverride("font", UiTheme.Display);
        title.AddThemeFontSizeOverride("font_size", 34);
        head.AddChild(title);

        App.RefreshProgression();
        var p = App.Prog;
        var s = App.Save;

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 14) };
        root.AddChild(spacer);

        // rank ring
        var ring = new PanelContainer { CustomMinimumSize = new Vector2(168, 168), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        ring.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.10f, 0.18f, 0.92f),
            BorderColor = UiTheme.Accent, BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3, BorderWidthBottom = 3,
            CornerRadiusTopLeft = 60, CornerRadiusTopRight = 60, CornerRadiusBottomLeft = 60, CornerRadiusBottomRight = 60,
        });
        var rl = new Label { Text = $"{p.Commander}", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        rl.AddThemeFontOverride("font", UiTheme.Display);
        rl.AddThemeFontSizeOverride("font_size", 73);
        rl.AddThemeColorOverride("font_color", UiTheme.Accent);
        ring.AddChild(rl);
        root.AddChild(ring);

        var sub = new Label { Text = $"COMMANDER LEVEL {p.Commander}    ·    HERO {p.Hero}/20", HorizontalAlignment = HorizontalAlignment.Center };
        sub.AddThemeFontSizeOverride("font_size", 21);
        sub.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
        root.AddChild(sub);

        var spacer2 = new Control { CustomMinimumSize = new Vector2(0, 20) };
        root.AddChild(spacer2);

        // ---- lifetime stats ----
        int cleared = 0, totalStars = 0, missions = App.Cfg.Arc.Missions.Count;
        foreach (var m in App.Cfg.Arc.Missions)
        {
            var rec = s.Record(m.Id);
            if (rec.Cleared) cleared++;
            totalStars += rec.Stars;
        }

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(list);

        int perfect = 0;
        foreach (var m in App.Cfg.Arc.Missions) if (s.Record(m.Id).Stars >= 3) perfect++;

        Header(list, "CAREER");
        Row(list, "⚔  Enemies killed", $"{s.LifetimeKills:N0}");
        Row(list, "💥  Total damage dealt", $"{F(s.LifetimeDamage)}");
        Row(list, "☠  Bosses killed", $"{s.LifetimeBossKills:N0}");
        Row(list, "▶  Missions played", $"{s.MissionsPlayed:N0}");
        Row(list, "✔  Stages cleared", $"{s.StageClears:N0}");

        Header(list, "CAMPAIGN");
        Row(list, "★  Stages beaten", $"{cleared}/{missions}");
        Row(list, "★  Stars earned", $"{totalStars}/{missions * 3}");
        Row(list, "🏆  100% stages (3★)", $"{perfect}/{missions}");

        Header(list, "ARSENAL");
        Row(list, "🛡  Planet Shield", $"LV {s.PlanetShieldLevel}/12");
        int owLvl = 0; foreach (var v in s.OrbitalMeta.Values) owLvl += v;
        Row(list, "✷  Sentinels total level", $"{owLvl}");
        Row(list, "◆  Chips owned", $"{ChipCount(s)}");

        Header(list, "BANK");
        Row(list, "◇  Research Data", $"{F(s.ResearchData)}");
        Row(list, "✷  Sentinel Cores", $"{s.SentinelCores}");
        Row(list, "❖  Exotic Alloy", $"{F(s.ExoticAlloy)}");
        Row(list, "✦  Commendations", $"{App.Shop.Balance}");
        Row(list, "🔑  Keys", $"{s.SilverKeys} silver · {s.GoldKeys} gold");
    }

    private static int ChipCount(Sentinel.Meta.SaveGame s)
    {
        int n = 0;
        foreach (var v in s.ChipInventory.Values) n += v;
        return n;
    }

    /// <summary>Section divider so the career/campaign/arsenal/bank blocks read apart.</summary>
    private static void Header(Control parent, string text)
    {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", UiTheme.Display);
        l.AddThemeFontSizeOverride("font_size", 22);
        l.AddThemeColorOverride("font_color", UiTheme.Accent);
        l.Modulate = new Color(1, 1, 1, 0.85f);
        parent.AddChild(l);
    }

    private static void Row(Control parent, string label, string val)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(1, 1, 1, 0.04f),
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10,
        });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        p.AddChild(row);
        var a = new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        a.AddThemeFontSizeOverride("font_size", 21);
        var b = new Label { Text = val, HorizontalAlignment = HorizontalAlignment.Right };
        b.AddThemeFontOverride("font", UiTheme.Display);
        b.AddThemeFontSizeOverride("font_size", 22);
        b.AddThemeColorOverride("font_color", UiTheme.Accent);
        row.AddChild(a); row.AddChild(b);
        parent.AddChild(p);
    }

    private static string Mmss(int secs) => secs <= 0 ? "—" : $"{secs / 60}:{secs % 60:00}";
    private static string F(double v) => Mathf.FloorToInt((float)v).ToString();
}
