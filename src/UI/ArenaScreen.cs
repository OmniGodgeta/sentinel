using Godot;

namespace Sentinel.UI;

/// <summary>
/// Galaxy Arena pre-run screen — blurb + Enter. The live wave/gold display and the
/// intermission turret shop are built by <c>GameRoot</c> itself once a run is in
/// progress (see its arena overlay); this screen is swapped out the moment the
/// mission starts, same as every other <c>AppRoot</c>-level screen.
/// </summary>
public sealed partial class ArenaScreen : CanvasLayer
{
    public Game.AppRoot App = null!;

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.5f, PlanetScale = 0.55f, NebulaAlpha = 0.10f });

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -460, OffsetRight = 460, OffsetTop = 34, OffsetBottom = -12,
        };
        root.AddThemeConstantOverride("separation", 14);
        root.Theme = UiTheme.Instance;
        AddChild(MenuFrame.Around(root));
        AddChild(root);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(210, 84) };
        back.Pressed += () => App.ShowEvents();
        head.AddChild(back);
        var title = new Label { Text = "  GALAXY ARENA", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontOverride("font", UiTheme.Display);
        title.AddThemeFontSizeOverride("font_size", 34);
        head.AddChild(title);

        var blurb = new Label
        {
            Text = "Wave after wave, no end. Turrets you build stay up between waves, and the " +
                   "Arena Gold you earn from kills buys turret upgrades during a short " +
                   "intermission after each clear. How far can you push it?",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 1, 1, 0.75f),
        };
        blurb.AddThemeFontSizeOverride("font_size", 20);
        root.AddChild(blurb);

        var start = new Button { Text = "Enter the Arena", CustomMinimumSize = new Vector2(0, 84) };
        start.AddThemeFontSizeOverride("font_size", 24);
        UiTheme.StylePrimary(start);
        start.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Confirm(); App.StartArena(); };
        root.AddChild(start);
    }
}
