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
            OffsetLeft = -460, OffsetRight = 460, OffsetTop = 198, OffsetBottom = -22,
        };
        root.AddThemeConstantOverride("separation", 14);
        root.Theme = UiTheme.Instance;
        AddChild(MenuFrame.Around(root));
        AddChild(root);

        // Arena header (title + back button)
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        root.AddChild(head);

        var back = new Button { Text = "‹  EXIT", CustomMinimumSize = new Vector2(210, 84) };
        back.Pressed += () => App.ShowEvents();
        
        var title = new Label { Text = "  GALAXY ARENA", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontOverride("font", UiTheme.Display);
        title.AddThemeFontSizeOverride("font_size", 34);
        head.AddChild(back);
        head.AddChild(title);

        // Gold display
        var goldBox = new HBoxContainer
        {
            Alignment = new Vector2(1, 0.5f),
            OffsetRight = -34,
        };
        goldBox.AddThemeConstantOverride("separation", 8);
        root.AddChild(goldBox);

        var goldIcon = new TextureRect
        {
            Texture = new Texture2D(GetViewport().GetWindowSize(), 1), // placeholder
            Size = new Vector2(64, 34),
            Stretch = Texture.StretchFlags.KeepAspect,
        };
        var goldLabel = new Label { Text = "100", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        goldLabel.AddThemeFontSizeOverride("font_size", 24);
        goldLabel.AddThemeFontOverride("font", UiTheme.Display);
        goldLabel.Modulate = new Color(1, 1, 0, 1);
        goldIcon.Modulate = new Color(1, 1, 0, 0.8f);
        goldBox.AddChild(goldIcon);
        goldBox.AddChild(goldLabel);
        root.AddChild(goldLabel); // Keep goldLabel for now

        // Wave info
        var waveInfo = new Label
        {
            Text = "Wave  1    Enemies  35    Arena Gold  120",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 1, 1, 0.85f),
        };
        waveInfo.AddThemeFontSizeOverride("font_size", 18);
        root.AddChild(waveInfo);

        // Intermission shop (initially hidden, shown between waves)
        var shopContainer = new HBoxContainer
        {
            OffsetTop = 12,
        };
        shopContainer.AddThemeConstantOverride("separation", 8);
        shopContainer.Set("theme_override/alignment", new Vector2(0, 0.5f));
        AddChild(shopContainer);

        var upgradeBox = new ScrollContainer
        {
            HScrollMode = ScrollMode.Disabled,
            VScrollMode = ScrollMode.Auto,
            Size = new Vector2(0, 384),
        };
        upgradeBox.Set("theme_override/corners", new Vector2(16, 16, 16, 16));
        upgradeBox.AddChild(new RectangleShape2D { Size = new Vector2(560, 384) }.ToControl() as Control);
        upgradeBox.AddChild(new BoxContainer { GrowthPolicy = BoxContainer.GrowthPolicy.Filler });
        root.AddChild(upgradeBox);

        var backBtn = new Button { Text = "Back", CustomMinimumSize = new Vector2(200, 44) };
        backBtn.Pressed += () => App.ShowEvents();
        shopContainer.AddChild(backBtn);

        // Placeholder buttons for future upgrades
        var btnContainer = new HBoxContainer { OffsetTop = 140, OffsetBottom = 14, Set("theme_override/theme_override/corners", new Vector2(10, 10, 10, 10)) };
        shopContainer.AddChild(btnContainer);
    }
}
