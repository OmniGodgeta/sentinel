using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>Landing screen — the "Beyond" key-art look: nebula + rotating Earth,
/// the wordmark up top, an animated control stack below.</summary>
public sealed partial class MenuScreen : CanvasLayer
{
    public AppRoot App = null!;

    public override void _Ready()
    {
        Layer = 5;
        AddChild(new MenuBackground { ShowPlanet = true, PlanetY = 0.36f, PlanetScale = 1f });
        AddChild(new Sentinel.Meta.UpdateChecker());

        var s = App.Save;
        App.RefreshProgression();
        var p = App.Prog;

        // ---- wordmark, pinned near the top ----
        var head = new VBoxContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0f, OffsetTop = 46, OffsetLeft = 20, OffsetRight = -20,
        };
        AddChild(head);
        var title = new Label { Text = "B E Y O N D", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 68);
        title.AddThemeColorOverride("font_color", UiTheme.Accent);
        title.AddThemeConstantOverride("outline_size", 6);
        title.AddThemeColorOverride("font_outline_color", new Color(0.01f, 0.03f, 0.06f, 0.9f));
        head.AddChild(title);
        var tag = new Label { Text = "no ads   ·   no purchases   ·   ever", HorizontalAlignment = HorizontalAlignment.Center };
        tag.AddThemeFontSizeOverride("font_size", 16);
        tag.AddThemeColorOverride("font_color", new Color(UiTheme.Accent2, 0.85f));
        head.AddChild(tag);

        // ---- control stack, lower half, on a legibility panel ----
        var panel = new PanelContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.47f, AnchorBottom = 1f,
            OffsetLeft = 10, OffsetRight = -10, OffsetTop = 0, OffsetBottom = -14,
        };
        var psb = new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.03f, 0.06f, 0.62f),
            BorderColor = new Color(UiTheme.Accent, 0.14f),
            BorderWidthTop = 1,
            CornerRadiusTopLeft = 20, CornerRadiusTopRight = 20, CornerRadiusBottomLeft = 20, CornerRadiusBottomRight = 20,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 14, ContentMarginBottom = 14,
        };
        panel.AddThemeStyleboxOverride("panel", psb);
        panel.Theme = UiTheme.Instance;
        AddChild(panel);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        panel.AddChild(scroll);

        var stack = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        stack.AddThemeConstantOverride("separation", 13);
        scroll.AddChild(stack);

        var totals = new Label
        {
            Text = $"Commander {p.Commander}      Hero {p.Hero}/20      ✦ {App.Shop.Balance}\nRD {F(s.ResearchData)}     Alloy {F(s.ExoticAlloy)}     Cores {s.SentinelCores}",
            HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.82f),
        };
        totals.AddThemeFontSizeOverride("font_size", 17);
        stack.AddChild(totals);

        var play = new GlowButton { Text = "▶   PLAY", FontSize = 44, Primary = true, CustomMinimumSize = new Vector2(0, 132) };
        play.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Confirm(); PlayPressed(); };
        stack.AddChild(play);

        var wk = Sentinel.Meta.WeeklyChallenge.Current();
        string wkText = $"★   WEEKLY · {wk.Title}";
        if (s.WeeklyId == wk.Id && s.WeeklyBest > 0) wkText += $"   ·   best {Mmss(s.WeeklyBest)}";
        var weekly = new GlowButton { Text = wkText, FontSize = 19, Alt = true, CustomMinimumSize = new Vector2(0, 74) };
        weekly.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Click(); App.StartWeekly(); };
        stack.AddChild(weekly);

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 14);
        stack.AddChild(grid);
        grid.AddChild(Nav("◈   Star Map", App.ShowLevels, false));
        grid.AddChild(Nav("∞   Endless", () => App.StartMission("res://data/missions/endless.json", "endless"), false));
        grid.AddChild(Nav("⬡   Research", App.ShowResearch, false));
        grid.AddChild(Nav("◆   Protocols", App.ShowAbilities, true));
        grid.AddChild(Nav("✦   Shop", App.ShowShop, true));
        grid.AddChild(Nav("☰   Codex", App.ShowCodex, false));
        grid.AddChild(Nav("⚙   Settings", App.ShowSettings, true));
        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddChild(spacer);

        if (s.EndlessBest > 0)
        {
            var eb = new Label { Text = $"Endless best · survived {Mmss(s.EndlessBest)}", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.45f) };
            eb.AddThemeFontSizeOverride("font_size", 14);
            stack.AddChild(eb);
        }
    }

    private static string Mmss(int secs) => $"{secs / 60}:{secs % 60:00}";

    private GlowButton Nav(string text, System.Action onPress, bool alt)
    {
        var b = new GlowButton { Text = text, FontSize = 24, Alt = alt, CustomMinimumSize = new Vector2(0, 118) };
        b.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Click(); onPress(); };
        return b;
    }

    private void PlayPressed()
    {
        var ids = new System.Collections.Generic.List<string>();
        foreach (var m in App.Cfg.Arc.Missions) ids.Add(m.Id);
        foreach (var m in App.Cfg.Arc.Missions)
            if (!App.Save.Record(m.Id).Cleared && App.Save.IsUnlocked(m.Id, ids))
            {
                App.StartMission(m.File, m.Id);
                return;
            }
        App.ShowLevels();
    }

    private static string F(double v) => Mathf.FloorToInt((float)v).ToString();
}
