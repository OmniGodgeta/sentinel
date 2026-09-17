using Godot;

namespace Sentinel.UI;

/// <summary>
/// A list-row panel with an accent border and a large, very faint emblem bleeding off its
/// right edge. The Research and Codex lists were bare unstyled <c>PanelContainer</c>s,
/// which read as flat grey slabs of text; the watermark gives each row a subject at a
/// glance without competing with the text over it.
///
/// The emblem is a PDTD sprite path as <see cref="Render.Art.Pdtd"/> takes it
/// (e.g. "techpoint/waterdrop"). A path that hasn't been extracted just draws nothing,
/// leaving a plain accented panel — never a broken row.
/// </summary>
public sealed partial class ArtCard : PanelContainer
{
    /// <summary>Builds the row. <paramref name="content"/> is added inside it.</summary>
    public static ArtCard Wrap(Control content, Color accent, string? emblemPath, bool dim = false)
    {
        var card = new ArtCard();
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, dim ? 0.04f : 0.09f),
            BorderColor = new Color(accent, dim ? 0.25f : 0.55f),
            BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            ContentMarginLeft = 14, ContentMarginRight = 14,
            ContentMarginTop = 10, ContentMarginBottom = 10,
        });

        var art = emblemPath == null ? null : Render.Art.Pdtd(emblemPath);
        if (art != null)
        {
            // Behind the content and clipped to the panel, anchored off the right edge so
            // it reads as a watermark rather than an icon competing for attention.
            var clip = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
            clip.SetAnchorsPreset(LayoutPreset.FullRect);
            card.AddChild(clip);

            var tex = new TextureRect
            {
                Texture = art,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
                Modulate = new Color(1, 1, 1, dim ? 0.05f : 0.11f),
                AnchorLeft = 0.60f, AnchorRight = 1.06f,
                AnchorTop = -0.18f, AnchorBottom = 1.18f,
            };
            clip.AddChild(tex);
        }

        card.AddChild(content);
        return card;
    }
}
