using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// EVENTS — a PDTD-style "Expedition" list surfacing the game's rotating/long-form
/// modes (Weekly Challenge, Endless Hold, Ascension) as cards instead of buried nav
/// buttons, plus a locked teaser for Galaxy Arena (see docs/ROADMAP.md — a deferred,
/// not-yet-built PvE wave-survival arena + ranking mode).
/// </summary>
public sealed partial class EventsScreen : CanvasLayer
{
    public AppRoot App = null!;

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
        back.Pressed += () => App.ShowMenu();
        head.AddChild(back);
        var title = new Label { Text = "  EVENTS", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontOverride("font", UiTheme.Display);
        title.AddThemeFontSizeOverride("font_size", 34);
        head.AddChild(title);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(list);

        var wk = Sentinel.Meta.WeeklyChallenge.Current();
        list.AddChild(Card(
            "WEEKLY CHALLENGE", wk.Title, wk.MutatorBlurb,
            $"deepest so far: wave {App.Save.WeeklyBest}", new Color(0.86f, 0.27f, 0.57f),
            () => App.StartWeekly(), locked: false));

        list.AddChild(Card(
            "ENDLESS HOLD", "One planet. No end.", "Survive as long as you can against an endlessly escalating hold.",
            $"best: wave {App.Save.EndlessBest}", UiTheme.Accent,
            () => App.StartMission("res://data/missions/endless.json", "endless"), locked: false));

        list.AddChild(Card(
            "ASCENSION", "Harder mission tiers, bigger rewards.", "Re-run a cleared mission at a tougher Ascension tier for better payouts.",
            "pick a stage on the Star Map", new Color(0.95f, 0.78f, 0.25f),
            () => App.ShowLevels(), locked: false));

        list.AddChild(Card(
            "GALAXY ARENA", "Coming soon", "PvE wave-survival arena with a ranking system — see docs/ROADMAP.md. Not built yet.",
            "", new Color(0.5f, 0.5f, 0.6f), null, locked: true));

        var note = new Label
        {
            Text = "Keys for the Armory drop from these too — the same \"earned by playing, never for sale\" rule as everything else.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 1, 1, 0.4f),
        };
        note.AddThemeFontSizeOverride("font_size", 17);
        list.AddChild(note);
    }

    private Control Card(string kicker, string title, string blurb, string footer, Color accent, System.Action? onEnter, bool locked)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, locked ? 0.05f : 0.10f), BorderColor = new Color(accent, locked ? 0.35f : 0.65f),
            BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 14, ContentMarginBottom = 14,
        });
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 4);
        p.AddChild(col);

        var kk = new Label { Text = kicker, Modulate = new Color(accent, locked ? 0.5f : 0.85f) };
        kk.AddThemeFontSizeOverride("font_size", 15);
        col.AddChild(kk);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        col.AddChild(row);
        var tCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        tCol.AddThemeConstantOverride("separation", 3);
        row.AddChild(tCol);

        var nm = new Label { Text = title, Modulate = locked ? new Color(1, 1, 1, 0.55f) : Colors.White };
        nm.AddThemeFontOverride("font", UiTheme.Display);
        nm.AddThemeFontSizeOverride("font_size", 22);
        tCol.AddChild(nm);
        var bl = new Label { Text = blurb, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, locked ? 0.45f : 0.7f) };
        bl.AddThemeFontSizeOverride("font_size", 17);
        tCol.AddChild(bl);
        if (footer != "")
        {
            var ft = new Label { Text = footer, Modulate = new Color(1, 1, 1, 0.5f) };
            ft.AddThemeFontSizeOverride("font_size", 15);
            tCol.AddChild(ft);
        }

        var enter = new Button
        {
            Text = locked ? "Locked" : "Enter", CustomMinimumSize = new Vector2(140, 63),
            Disabled = locked,
        };
        enter.AddThemeFontSizeOverride("font_size", 18);
        if (!locked && onEnter != null) enter.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Confirm(); onEnter(); };
        row.AddChild(enter);

        return p;
    }
}
