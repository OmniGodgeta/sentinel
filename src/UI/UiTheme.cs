using Godot;

namespace Sentinel.UI;

/// <summary>
/// One shared dark theme so every screen matches — the "Beyond" palette, drawn
/// from the app icon: deep space navy, teal on one side, magenta on the other.
/// Built once, in code. Sizes here are deliberately finger-friendly.
/// </summary>
public static class UiTheme
{
    private static Theme? _theme;
    public static Theme Instance => _theme ??= Build();

    /// <summary>Icon teal — primary accent (the "BEYOND" wordmark colour).</summary>
    public static readonly Color Accent = new(0.26f, 0.82f, 0.87f);
    /// <summary>Icon magenta — secondary accent (the ship's glow / right nebula).</summary>
    public static readonly Color Accent2 = new(0.86f, 0.27f, 0.57f);
    public static readonly Color Ink = new(0.88f, 0.95f, 0.97f);
    public static readonly Color Panel = new(0.05f, 0.07f, 0.12f, 0.94f);
    public static readonly Color Deep = new(0.015f, 0.02f, 0.04f);

    // Minimum comfortable touch target on a phone.
    public const int TapMin = 56;

    private static Theme Build()
    {
        var t = new Theme { DefaultFontSize = 18 };

        var panel = Flat(Panel, new Color(Accent, 0.14f), 12, 1);
        t.SetStylebox("panel", "PanelContainer", panel);
        t.SetStylebox("panel", "Panel", panel);

        var normal = Flat(new Color(0.14f, 0.18f, 0.27f), new Color(Accent, 0.55f), 10, 2);
        var hover = Flat(new Color(0.16f, 0.27f, 0.35f), new Color(Accent, 0.9f), 10, 2);
        var pressed = Flat(new Color(0.30f, 0.12f, 0.26f), new Color(Accent2, 0.85f), 10, 2);
        var disabled = Flat(new Color(0.09f, 0.10f, 0.13f), new Color(1, 1, 1, 0.05f), 10, 1);
        foreach (var (n, sb) in new[] { ("normal", normal), ("hover", hover), ("pressed", pressed), ("disabled", disabled), ("focus", hover) })
            t.SetStylebox(n, "Button", sb);
        t.SetColor("font_color", "Button", Ink);
        t.SetColor("font_hover_color", "Button", Colors.White);
        t.SetColor("font_pressed_color", "Button", new Color(1f, 0.85f, 0.93f));
        t.SetColor("font_disabled_color", "Button", new Color(1, 1, 1, 0.3f));
        t.SetConstant("h_separation", "Button", 8);
        t.SetFontSize("font_size", "Button", 19);

        // CheckButton — the toggle pill; give it real height too
        foreach (var (n, sb) in new[] { ("normal", normal), ("hover", hover), ("pressed", pressed), ("disabled", disabled) })
            t.SetStylebox(n, "CheckButton", sb);
        t.SetColor("font_color", "CheckButton", Ink);
        t.SetFontSize("font_size", "CheckButton", 18);

        t.SetColor("font_color", "Label", new Color(Ink, 0.92f));

        // Sliders — a fat, easy-to-drag grabber
        var grab = new StyleBoxFlat
        {
            BgColor = Accent,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 14, ContentMarginBottom = 14,
        };
        t.SetStylebox("grabber_area", "HSlider", grab);
        t.SetStylebox("grabber_area_highlight", "HSlider", grab);
        var track = Flat(new Color(0, 0, 0, 0.5f), new Color(Accent, 0.2f), 6, 1);
        t.SetStylebox("slider", "HSlider", track);
        t.SetConstant("grabber_offset", "HSlider", 0);

        var pbBg = Flat(new Color(0, 0, 0, 0.5f), new Color(0, 0, 0, 0), 4, 0);
        var pbFg = Flat(Accent, new Color(0, 0, 0, 0), 4, 0);
        t.SetStylebox("background", "ProgressBar", pbBg);
        t.SetStylebox("fill", "ProgressBar", pbFg);

        return t;
    }

    /// <summary>A primary call-to-action stylebox set (teal fill).</summary>
    public static void StylePrimary(Button b)
    {
        var fill = Flat(new Color(Accent, 0.92f), new Color(Accent, 1f), 12, 0);
        var fillHover = Flat(new Color(0.36f, 0.92f, 0.97f, 1f), new Color(Colors.White, 0.6f), 12, 0);
        var fillPress = Flat(new Color(Accent2, 0.95f), new Color(Colors.White, 0.5f), 12, 0);
        b.AddThemeStyleboxOverride("normal", fill);
        b.AddThemeStyleboxOverride("hover", fillHover);
        b.AddThemeStyleboxOverride("pressed", fillPress);
        b.AddThemeStyleboxOverride("focus", fillHover);
        b.AddThemeColorOverride("font_color", new Color(0.03f, 0.06f, 0.09f));
        b.AddThemeColorOverride("font_hover_color", new Color(0.03f, 0.06f, 0.09f));
        b.AddThemeColorOverride("font_pressed_color", Colors.White);
    }

    private static StyleBoxFlat Flat(Color bg, Color border, int radius, int bw)
    {
        var s = new StyleBoxFlat
        {
            BgColor = bg,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 14, ContentMarginBottom = 14,
        };
        if (bw > 0)
        {
            s.BorderColor = border;
            s.BorderWidthLeft = s.BorderWidthRight = s.BorderWidthTop = s.BorderWidthBottom = bw;
        }
        return s;
    }
}
