using Godot;

namespace Sentinel.UI;

/// <summary>
/// Decorative frame for a menu panel: a faint rounded border with one bright segment that
/// travels the whole perimeter once every <see cref="LapSeconds"/> (5s by default), plus
/// corner ticks. Purely cosmetic and input-transparent — drop one in as a sibling of the
/// screen's root box with the same anchors (see <see cref="Around"/>) and it frames it.
/// </summary>
public sealed partial class MenuFrame : Control
{
    public Color Accent = UiTheme.Accent;
    public float LapSeconds = 5f;
    /// <summary>Fraction of the perimeter the bright travelling segment covers.</summary>
    public float SegmentFrac = 0.10f;

    private float _t;

    /// <summary>Build a frame that exactly overlays <paramref name="target"/> — copies its
    /// anchors/offsets, so it tracks the same box the screen's content lives in.</summary>
    public static MenuFrame Around(Control target, Color? accent = null) => new()
    {
        Accent = accent ?? UiTheme.Accent,
        AnchorLeft = target.AnchorLeft, AnchorRight = target.AnchorRight,
        AnchorTop = target.AnchorTop, AnchorBottom = target.AnchorBottom,
        OffsetLeft = target.OffsetLeft - 10, OffsetRight = target.OffsetRight + 10,
        OffsetTop = target.OffsetTop - 8, OffsetBottom = target.OffsetBottom + 8,
        MouseFilter = MouseFilterEnum.Ignore,
        ZIndex = -1,
    };

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();
    }

    /// <summary>Point at normalised distance <paramref name="u"/> (0-1) around the border.</summary>
    private Vector2 Perimeter(float u, Rect2 r)
    {
        float w = r.Size.X, h = r.Size.Y, per = 2f * (w + h);
        float d = Mathf.PosMod(u, 1f) * per;
        if (d < w) return new Vector2(r.Position.X + d, r.Position.Y);
        d -= w;
        if (d < h) return new Vector2(r.End.X, r.Position.Y + d);
        d -= h;
        if (d < w) return new Vector2(r.End.X - d, r.End.Y);
        d -= w;
        return new Vector2(r.Position.X, r.End.Y - d);
    }

    public override void _Draw()
    {
        var sz = Size;
        if (sz.X < 4f || sz.Y < 4f) return;
        var r = new Rect2(1, 1, sz.X - 2, sz.Y - 2);

        // faint static border
        DrawRect(r, new Color(Accent, 0.16f), false, 2f);

        // corner ticks
        float tick = Mathf.Min(26f, Mathf.Min(sz.X, sz.Y) * 0.06f);
        var tc = new Color(Accent, 0.45f);
        DrawLine(r.Position, r.Position + new Vector2(tick, 0), tc, 2.5f);
        DrawLine(r.Position, r.Position + new Vector2(0, tick), tc, 2.5f);
        DrawLine(new Vector2(r.End.X, r.Position.Y), new Vector2(r.End.X - tick, r.Position.Y), tc, 2.5f);
        DrawLine(new Vector2(r.End.X, r.Position.Y), new Vector2(r.End.X, r.Position.Y + tick), tc, 2.5f);
        DrawLine(r.End, r.End - new Vector2(tick, 0), tc, 2.5f);
        DrawLine(r.End, r.End - new Vector2(0, tick), tc, 2.5f);
        DrawLine(new Vector2(r.Position.X, r.End.Y), new Vector2(r.Position.X + tick, r.End.Y), tc, 2.5f);
        DrawLine(new Vector2(r.Position.X, r.End.Y), new Vector2(r.Position.X, r.End.Y - tick), tc, 2.5f);

        // the travelling segment — one lap per LapSeconds, drawn as a short fading tail so
        // it reads as a scan line rather than a hard dash
        float head = _t / Mathf.Max(0.5f, LapSeconds);
        const int steps = 14;
        for (int i = 0; i < steps; i++)
        {
            float f0 = head - SegmentFrac * (i / (float)steps);
            float f1 = head - SegmentFrac * ((i + 1) / (float)steps);
            float fade = 1f - i / (float)steps;
            DrawLine(Perimeter(f1, r), Perimeter(f0, r), new Color(Accent, 0.9f * fade * fade), 3f);
        }
        DrawCircle(Perimeter(head, r), 3.5f, new Color(1f, 1f, 1f, 0.9f));
    }
}
