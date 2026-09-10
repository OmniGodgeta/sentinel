using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>Landing screen — Earth backdrop, the BEYOND wordmark, a big PLAY button, a nav grid.</summary>
public sealed partial class MenuScreen : CanvasLayer
{
    public AppRoot App = null!;

    public override void _Ready()
    {
        Layer = 5;
        AddChild(new MenuBackground { PlanetY = 0.20f });
        AddChild(new Sentinel.Meta.UpdateChecker());

        var s = App.Save;
        App.RefreshProgression();
        var p = App.Prog;

        // one centred column holding wordmark + controls, in the lower ~58% of the screen
        var wrap = new CenterContainer { AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.30f, AnchorBottom = 1f, OffsetBottom = -28 };
        wrap.Theme = UiTheme.Instance;
        AddChild(wrap);

        var stack = new VBoxContainer { CustomMinimumSize = new Vector2(680, 0) };
        stack.AddThemeConstantOverride("separation", 16);
        wrap.AddChild(stack);

        var title = new Label { Text = "BEYOND", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 78);
        title.AddThemeColorOverride("font_color", UiTheme.Accent);
        stack.AddChild(title);

        var tag = new Label { Text = "no ads   ·   no purchases   ·   ever", HorizontalAlignment = HorizontalAlignment.Center };
        tag.AddThemeFontSizeOverride("font_size", 16);
        tag.AddThemeColorOverride("font_color", new Color(UiTheme.Accent2, 0.75f));
        stack.AddChild(tag);

        var totals = new Label
        {
            Text = $"Commander {p.Commander}     Hero {p.Hero}/20\nRD {F(s.ResearchData)}     Alloy {F(s.ExoticAlloy)}     Cores {s.SentinelCores}",
            HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.72f),
        };
        totals.AddThemeFontSizeOverride("font_size", 17);
        stack.AddChild(totals);

        stack.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });

        var play = new Button { Text = "▶   PLAY", CustomMinimumSize = new Vector2(0, 156), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        play.AddThemeFontSizeOverride("font_size", 50);
        UiTheme.StylePrimary(play);
        play.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Confirm(); PlayPressed(); };
        stack.AddChild(play);

        var wk = Sentinel.Meta.WeeklyChallenge.Current();
        string wkText = $"★   WEEKLY   ·   {wk.Title}";
        if (s.WeeklyId == wk.Id && s.WeeklyBest > 0) wkText += $"      best · {Mmss(s.WeeklyBest)}";
        var weekly = new Button { Text = wkText, CustomMinimumSize = new Vector2(0, 84), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        weekly.AddThemeFontSizeOverride("font_size", 20);
        weekly.AddThemeColorOverride("font_color", UiTheme.Accent2);
        weekly.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Click(); App.StartWeekly(); };
        stack.AddChild(weekly);

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 16);
        stack.AddChild(grid);
        grid.AddChild(Nav("Star Map", App.ShowLevels));
        grid.AddChild(Nav("Endless", () => App.StartMission("res://data/missions/endless.json", "endless")));
        grid.AddChild(Nav("Research", App.ShowResearch));
        grid.AddChild(Nav("Protocols", App.ShowAbilities));
        grid.AddChild(Nav("Codex", App.ShowCodex));
        grid.AddChild(Nav("Settings", App.ShowSettings));

        if (s.EndlessBest > 0)
        {
            var eb = new Label { Text = $"Endless best · survived {Mmss(s.EndlessBest)}", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.4f) };
            eb.AddThemeFontSizeOverride("font_size", 14);
            stack.AddChild(eb);
        }
    }

    private static string Mmss(int secs) => $"{secs / 60}:{secs % 60:00}";

    private static Button Nav(string text, System.Action onPress)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 116), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        b.AddThemeFontSizeOverride("font_size", 27);
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
