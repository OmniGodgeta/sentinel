using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// Landing screen — planet backdrop, title, a big PLAY button, a small nav row.
/// Fully anchor-driven so it adapts to any portrait viewport.
/// </summary>
public sealed partial class MenuScreen : CanvasLayer
{
    public AppRoot App = null!;

    public override void _Ready()
    {
        Layer = 5;
        AddChild(new MenuBackground { PlanetY = 0.28f });

        var s = App.Save;
        App.RefreshProgression();
        var p = App.Prog;

        // title band, sits just under the planet (planet centre ~0.28h, radius up to ~170)
        var titleBox = new VBoxContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.40f, AnchorBottom = 0.40f,
        };
        titleBox.Theme = UiTheme.Instance;
        AddChild(titleBox);
        var title = new Label { Text = "SENTINEL", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 54);
        title.AddThemeColorOverride("font_color", new Color(0.92f, 0.96f, 1f));
        titleBox.AddChild(title);
        var tag = new Label { Text = "no ads  ·  no purchases  ·  ever", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.45f) };
        tag.AddThemeFontSizeOverride("font_size", 13);
        titleBox.AddChild(tag);

        // bottom control stack, anchored to the bottom, centred, capped width
        var wrap = new CenterContainer { AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.55f, AnchorBottom = 1f, OffsetBottom = -30 };
        wrap.Theme = UiTheme.Instance;
        AddChild(wrap);
        var stack = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
        stack.AddThemeConstantOverride("separation", 12);
        wrap.AddChild(stack);

        var totals = new Label
        {
            Text = $"Commander {p.Commander}     Hero {p.Hero}/20\nRD {F(s.ResearchData)}     Alloy {F(s.ExoticAlloy)}     Cores {s.SentinelCores}",
            HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.72f),
        };
        totals.AddThemeFontSizeOverride("font_size", 13);
        stack.AddChild(totals);

        var play = new Button { Text = "▶   PLAY", CustomMinimumSize = new Vector2(420, 68) };
        play.AddThemeFontSizeOverride("font_size", 26);
        play.AddThemeColorOverride("font_color", new Color(0.6f, 1f, 0.75f));
        play.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Confirm(); PlayPressed(); };
        stack.AddChild(play);

        var row1 = Row();
        row1.AddChild(Nav("Levels", App.ShowLevels));
        row1.AddChild(Nav("Endless", () => App.StartMission("res://data/missions/endless.json", "endless")));
        stack.AddChild(row1);

        var row2 = Row();
        row2.AddChild(Nav("Research", App.ShowResearch));
        row2.AddChild(Nav("Protocols", App.ShowAbilities));
        stack.AddChild(row2);

        var row3 = Row();
        row3.AddChild(Nav("Codex", App.ShowCodex));
        row3.AddChild(Nav("Settings", App.ShowSettings));
        stack.AddChild(row3);

        if (s.EndlessBest > 0)
        {
            var eb = new Label { Text = $"Endless best · wave {s.EndlessBest}", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.4f) };
            eb.AddThemeFontSizeOverride("font_size", 11);
            stack.AddChild(eb);
        }
    }

    private static HBoxContainer Row()
    {
        var r = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        r.AddThemeConstantOverride("separation", 10);
        return r;
    }

    private static Button Nav(string text, System.Action onPress)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 44), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        b.AddThemeFontSizeOverride("font_size", 14);
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
