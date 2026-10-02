using Godot;

namespace Sentinel.UI;

/// <summary>
/// PDTD-style bottom nav: one continuous dark bar (not separate boxed buttons), where
/// the active tab gets a bright slanted "flag" shape behind its icon/label and every
/// other tab sits flat and dim. Modeled directly on the reference screenshots (Shop /
/// Upgrades / Research / Level-Up), which all share this exact nav language.
/// </summary>
public sealed partial class NavTab : Button
{
    public string Label = "";
    public Texture2D? IconTex;
    public string Glyph = "";
    public bool Active;

    private Label _label = null!;
    private Label? _glyphLabel;
    private TextureRect? _iconRect;
    private float _hover;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, 116);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Flat = true;
        MouseFilter = MouseFilterEnum.Stop;

        if (IconTex != null)
        {
            _iconRect = new TextureRect
            {
                Texture = IconTex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
                AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 0f,
                OffsetLeft = -18, OffsetRight = 18, OffsetTop = 14, OffsetBottom = 50,
            };
            AddChild(_iconRect);
        }
        else if (!string.IsNullOrEmpty(Glyph))
        {
            _glyphLabel = new Label
            {
                Text = Glyph, HorizontalAlignment = HorizontalAlignment.Center,
                AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0f, AnchorBottom = 0f,
                OffsetTop = 12, OffsetBottom = 52, MouseFilter = MouseFilterEnum.Ignore,
            };
            _glyphLabel.AddThemeFontSizeOverride("font_size", 26);
            AddChild(_glyphLabel);
        }

        _label = new Label
        {
            Text = Label, HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetTop = -30, OffsetBottom = -8, MouseFilter = MouseFilterEnum.Ignore,
        };
        _label.AddThemeFontSizeOverride("font_size", 15);
        AddChild(_label);

        MouseEntered += () => _hover = 1f;
        MouseExited += () => _hover = 0f;
        SetProcess(true);
        Refresh();
    }

    public void SetActive(bool active) { Active = active; Refresh(); }

    private void Refresh()
    {
        var ink = Active ? new Color(0.03f, 0.06f, 0.09f) : new Color(1f, 1f, 1f, 0.75f);
        _label.AddThemeColorOverride("font_color", ink);
        _glyphLabel?.AddThemeColorOverride("font_color", ink);
        if (_iconRect != null) _iconRect.Modulate = Active ? new Color(0.03f, 0.06f, 0.09f) : new Color(1f, 1f, 1f, 0.8f);
        QueueRedraw();
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (!Active) return;
        var sz = Size;
        // The PDTD "flag": a parallelogram leaning right, its bottom edge starting
        // further left than its top edge, filling almost the whole cell.
        float lean = sz.Y * 0.28f;
        var pts = new[]
        {
            new Vector2(lean, 0), new Vector2(sz.X, 0),
            new Vector2(sz.X - lean, sz.Y), new Vector2(0, sz.Y),
        };
        DrawColoredPolygon(pts, new Color(0.86f, 0.97f, 0.96f, 0.96f));
    }
}

public static class PdtdNav
{
    /// <summary>Builds the single continuous nav bar. <paramref name="tabs"/> is
    /// (glyph-or-empty, label, onPress); the caller marks exactly one active via
    /// <paramref name="activeIndex"/>.</summary>
    public static Control Build((string glyph, string label, System.Action onPress)[] tabs, int activeIndex)
    {
        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.07f, 0.11f, 0.96f),
            BorderColor = new Color(1f, 1f, 1f, 0.08f),
            BorderWidthTop = 1,
            ContentMarginLeft = 0, ContentMarginRight = 0, ContentMarginTop = 0, ContentMarginBottom = 0,
        });

        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        bar.AddChild(row);

        for (int i = 0; i < tabs.Length; i++)
        {
            var (glyph, label, onPress) = tabs[i];
            var tab = new NavTab { Glyph = glyph, Label = label, Active = i == activeIndex };
            tab.Pressed += onPress;
            row.AddChild(tab);
        }
        return bar;
    }
}
