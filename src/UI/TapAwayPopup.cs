using Godot;

namespace Sentinel.UI;

/// <summary>
/// A small centred popup that closes when you tap anywhere outside it — no CLOSE button
/// to hunt for. Sizes itself to whatever you put in <see cref="Body"/> rather than a fixed
/// rect, so a four-row wallet doesn't render as a mostly-empty box.
/// Usage: <c>var p = TapAwayPopup.Open(this, "WALLET"); p.Body.AddChild(myRows);</c>
/// </summary>
public sealed partial class TapAwayPopup : CanvasLayer
{
    /// <summary>Put the popup's content in here.</summary>
    public VBoxContainer Body { get; private set; } = null!;

    public static TapAwayPopup Open(Node parent, string title, float maxWidth = 900f)
    {
        var p = new TapAwayPopup();
        parent.AddChild(p);
        p.Build(title, maxWidth);
        return p;
    }

    private void Build(string title, float maxWidth)
    {
        Layer = 30;

        // dim backdrop — any press anywhere on it dismisses
        var dim = new ColorRect { Color = new Color(0.01f, 0.02f, 0.04f, 0.72f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dim.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true })
            {
                Sentinel.Audio.AudioManager.Instance?.Back();
                QueueFree();
            }
        };
        AddChild(dim);

        // the card itself — centred, hugging its content, and it swallows taps so clicking
        // inside doesn't dismiss
        var panel = new PanelContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both, GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Theme = UiTheme.Instance,
        };
        panel.CustomMinimumSize = new Vector2(maxWidth, 0);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.07f, 0.12f, 0.98f),
            BorderColor = new Color(UiTheme.Accent, 0.6f),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ContentMarginLeft = 26, ContentMarginRight = 26, ContentMarginTop = 20, ContentMarginBottom = 22,
        });
        AddChild(panel);
        AddChild(MenuFrame.Around(panel));

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 12);
        panel.AddChild(col);

        var t = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
        t.AddThemeFontOverride("font", UiTheme.Display);
        t.AddThemeFontSizeOverride("font_size", 30);
        t.AddThemeColorOverride("font_color", UiTheme.Accent);
        col.AddChild(t);

        Body = new VBoxContainer();
        Body.AddThemeConstantOverride("separation", 10);
        col.AddChild(Body);

        var hint = new Label { Text = "tap anywhere to close", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.35f) };
        hint.AddThemeFontSizeOverride("font_size", 18);
        col.AddChild(hint);
    }
}
