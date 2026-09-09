using Godot;

namespace Sentinel.UI;

/// <summary>One shared dark theme so every screen matches. Built once, in code.</summary>
public static class UiTheme
{
    private static Theme? _theme;
    public static Theme Instance => _theme ??= Build();

    public static readonly Color Accent = new(0.45f, 0.85f, 1f);
    public static readonly Color Ink = new(0.90f, 0.94f, 1f);
    public static readonly Color Panel = new(0.07f, 0.08f, 0.11f, 0.92f);

    private static Theme Build()
    {
        var t = new Theme { DefaultFontSize = 16 };

        var panel = Flat(Panel, new Color(1, 1, 1, 0.08f), 10, 1);
        t.SetStylebox("panel", "PanelContainer", panel);
        t.SetStylebox("panel", "Panel", panel);

        var normal = Flat(new Color(0.13f, 0.15f, 0.20f), new Color(1, 1, 1, 0.10f), 8, 1);
        var hover = Flat(new Color(0.18f, 0.22f, 0.30f), new Color(Accent, 0.5f), 8, 1);
        var pressed = Flat(new Color(0.10f, 0.30f, 0.42f), new Color(Accent, 0.8f), 8, 1);
        var disabled = Flat(new Color(0.10f, 0.11f, 0.14f), new Color(1, 1, 1, 0.05f), 8, 1);
        foreach (var (n, sb) in new[] { ("normal", normal), ("hover", hover), ("pressed", pressed), ("disabled", disabled), ("focus", hover) })
            t.SetStylebox(n, "Button", sb);
        t.SetColor("font_color", "Button", Ink);
        t.SetColor("font_hover_color", "Button", Colors.White);
        t.SetColor("font_disabled_color", "Button", new Color(1, 1, 1, 0.3f));
        t.SetConstant("h_separation", "Button", 6);

        t.SetColor("font_color", "Label", new Color(Ink, 0.92f));

        var pbBg = Flat(new Color(0, 0, 0, 0.5f), new Color(0, 0, 0, 0), 4, 0);
        var pbFg = Flat(Accent, new Color(0, 0, 0, 0), 4, 0);
        t.SetStylebox("background", "ProgressBar", pbBg);
        t.SetStylebox("fill", "ProgressBar", pbFg);

        return t;
    }

    private static StyleBoxFlat Flat(Color bg, Color border, int radius, int bw)
    {
        var s = new StyleBoxFlat
        {
            BgColor = bg,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10,
        };
        if (bw > 0)
        {
            s.BorderColor = border;
            s.BorderWidthLeft = s.BorderWidthRight = s.BorderWidthTop = s.BorderWidthBottom = bw;
        }
        return s;
    }
}
