using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>Landing screen — Earth backdrop, title, a big PLAY button, a nav grid.</summary>
public sealed partial class MenuScreen : CanvasLayer
{
    public AppRoot App = null!;

    public override void _Ready()
    {
        Layer = 5;
        AddChild(new MenuBackground { PlanetY = 0.24f });

        var s = App.Save;
        App.RefreshProgression();
        var p = App.Prog;

        // one centred column holding title + controls, in the lower 60% of the screen
        var wrap = new CenterContainer { AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.42f, AnchorBottom = 1f, OffsetBottom = -24 };
        wrap.Theme = UiTheme.Instance;
        AddChild(wrap);

        var stack = new VBoxContainer { CustomMinimumSize = new Vector2(500, 0) };
        stack.AddThemeConstantOverride("separation", 14);
        wrap.AddChild(stack);

        var title = new Label { Text = "SENTINEL", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 58);
        title.AddThemeColorOverride("font_color", new Color(0.92f, 0.96f, 1f));
        stack.AddChild(title);
        var tag = new Label { Text = "no ads   ·   no purchases   ·   ever", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.45f) };
        tag.AddThemeFontSizeOverride("font_size", 14);
        stack.AddChild(tag);

        var totals = new Label
        {
            Text = $"Commander {p.Commander}     Hero {p.Hero}/20\nRD {F(s.ResearchData)}     Alloy {F(s.ExoticAlloy)}     Cores {s.SentinelCores}",
            HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.72f),
        };
        totals.AddThemeFontSizeOverride("font_size", 15);
        stack.AddChild(totals);

        stack.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        var play = new Button { Text = "▶   PLAY", CustomMinimumSize = new Vector2(500, 92) };
        play.AddThemeFontSizeOverride("font_size", 34);
        play.AddThemeColorOverride("font_color", new Color(0.6f, 1f, 0.75f));
        play.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Confirm(); PlayPressed(); };
        stack.AddChild(play);

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        stack.AddChild(grid);
        grid.AddChild(Nav("Star Map", App.ShowLevels));
        grid.AddChild(Nav("Endless", () => App.StartMission("res://data/missions/endless.json", "endless")));
        grid.AddChild(Nav("Research", App.ShowResearch));
        grid.AddChild(Nav("Protocols", App.ShowAbilities));
        grid.AddChild(Nav("Codex", App.ShowCodex));
        grid.AddChild(Nav("Settings", App.ShowSettings));

        if (s.EndlessBest > 0)
        {
            var eb = new Label { Text = $"Endless best · wave {s.EndlessBest}", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.4f) };
            eb.AddThemeFontSizeOverride("font_size", 12);
            stack.AddChild(eb);
        }
    }

    private static Button Nav(string text, System.Action onPress)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(244, 58), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        b.AddThemeFontSizeOverride("font_size", 19);
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
