using Godot;

namespace Sentinel.UI;

/// <summary>
/// A self-drawn menu button in the Beyond palette (teal by default, magenta for
/// the alt accent). Animated: a slow idle shimmer, a hover lift + glow, and a
/// press flash. Text is a child <see cref="Label"/> drawn over the panel.
/// </summary>
public sealed partial class GlowButton : BaseButton
{
    public string Text = "";
    public int FontSize = 30;
    public bool Alt;                 // magenta accent instead of teal
    public bool Primary;             // filled CTA (the PLAY button)

    private Label _label = null!;
    private float _hover, _press, _t;
    private float _seed;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, CustomMinimumSize.Y <= 0 ? 132 : CustomMinimumSize.Y);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        _seed = GD.Randf() * 10f;

        _label = new Label
        {
            Text = Text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _label.SetAnchorsPreset(LayoutPreset.FullRect);
        _label.AddThemeFontSizeOverride("font_size", FontSize);
        _label.AddThemeColorOverride("font_color", Primary ? new Color(0.02f, 0.05f, 0.08f) : new Color(0.92f, 0.98f, 1f));
        AddChild(_label);

        MouseEntered += () => _hoverTarget = 1f;
        MouseExited += () => _hoverTarget = 0f;
        Pressed += () => { _press = 1f; };
        SetProcess(true);
    }

    private float _hoverTarget;

    public override void _Process(double delta)
    {
        float d = (float)delta;
        _t += d;
        _hover = Mathf.MoveToward(_hover, _hoverTarget, d * 6f);
        _press = Mathf.MoveToward(_press, 0f, d * 3.5f);
        if (ButtonPressed) _hover = Mathf.Max(_hover, 0.6f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var sz = Size;
        var teal = new Color(0.26f, 0.82f, 0.87f);
        var mag = new Color(0.86f, 0.27f, 0.57f);
        var accent = Alt ? mag : teal;
        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 1.6f + _seed);
        float lift = _hover;

        var rect = new Rect2(Vector2.Zero, sz);
        float rad = 14f;

        // outer glow — expanding soft outlines, grows on hover, gentle idle breath
        float glowA = (0.06f + 0.20f * lift + 0.05f * pulse) + _press * 0.30f;
        for (int i = 1; i <= 3; i++)
        {
            float g = i * (2.5f + 4.5f * lift);
            RoundRectOutline(new Rect2(-new Vector2(g, g), sz + new Vector2(g * 2, g * 2)), rad + g,
                             2.5f, new Color(accent, glowA / (i * 1.7f)));
        }

        // body — a vertical gradient, teal-tinted navy, brighter toward the top
        if (Primary)
        {
            RoundRect(rect, rad, new Color(accent, 0.92f));
            RoundRect(new Rect2(0, 0, sz.X, sz.Y * 0.5f), rad, new Color(1f, 1f, 1f, 0.12f));
        }
        else
        {
            var top = new Color(0.10f, 0.16f, 0.24f).Lerp(accent, 0.10f + 0.10f * lift);
            var bot = new Color(0.04f, 0.06f, 0.11f);
            int strips = 10;
            for (int i = 0; i < strips; i++)
            {
                float f = i / (float)strips;
                RoundRectStrip(rect, rad, f, 1f / strips, top.Lerp(bot, f), i == 0, i == strips - 1);
            }
        }

        // moving diagonal sheen
        float sweep = Mathf.PosMod(_t * 0.35f + _seed, 2.4f) - 0.7f;
        float sx = sweep * sz.X;
        var sheen = new[]
        {
            new Vector2(sx, 0), new Vector2(sx + 46, 0),
            new Vector2(sx + 46 - sz.Y * 0.5f, sz.Y), new Vector2(sx - sz.Y * 0.5f, sz.Y),
        };
        DrawColoredPolygon(sheen, new Color(accent, (0.05f + 0.06f * lift) * (Primary ? 0.5f : 1f)));

        // border
        var bcol = new Color(accent, 0.35f + 0.5f * lift + 0.3f * _press);
        RoundRectOutline(rect, rad, 2f + 1.5f * lift, bcol);

        // press flash
        if (_press > 0.01f)
            RoundRect(rect, rad, new Color(mag, 0.18f * _press));

        // left accent tick
        if (!Primary)
            DrawRect(new Rect2(0, sz.Y * 0.28f, 3f + 2f * lift, sz.Y * 0.44f), new Color(accent, 0.7f + 0.3f * lift));
    }

    // ---- rounded-rect helpers (Godot has no built-in) ----
    private void RoundRect(Rect2 r, float rad, Color c)
    {
        rad = Mathf.Min(rad, Mathf.Min(r.Size.X, r.Size.Y) * 0.5f);
        if (rad <= 1f) { DrawRect(r, c); return; }
        DrawRect(new Rect2(r.Position + new Vector2(rad, 0), new Vector2(r.Size.X - rad * 2, r.Size.Y)), c);
        DrawRect(new Rect2(r.Position + new Vector2(0, rad), new Vector2(rad, r.Size.Y - rad * 2)), c);
        DrawRect(new Rect2(r.Position + new Vector2(r.Size.X - rad, rad), new Vector2(rad, r.Size.Y - rad * 2)), c);
        foreach (var (cx, cy) in new[] { (rad, rad), (r.Size.X - rad, rad), (rad, r.Size.Y - rad), (r.Size.X - rad, r.Size.Y - rad) })
            DrawCircle(r.Position + new Vector2(cx, cy), rad, c);
    }

    private void RoundRectStrip(Rect2 r, float rad, float startF, float hF, Color c, bool topCap, bool botCap)
    {
        float y = r.Position.Y + r.Size.Y * startF;
        float h = r.Size.Y * hF;
        var sr = new Rect2(r.Position.X, y, r.Size.X, h + 1f);
        if (topCap || botCap) RoundRect(sr, rad, c);
        else DrawRect(sr, c);
    }

    private void RoundRectOutline(Rect2 r, float rad, float w, Color c)
    {
        rad = Mathf.Min(rad, Mathf.Min(r.Size.X, r.Size.Y) * 0.5f);
        var pts = new System.Collections.Generic.List<Vector2>();
        void Arc(Vector2 ctr, float a0, float a1)
        {
            for (int i = 0; i <= 6; i++)
                pts.Add(ctr + Vector2.FromAngle(Mathf.Lerp(a0, a1, i / 6f)) * rad);
        }
        Arc(r.Position + new Vector2(r.Size.X - rad, rad), -Mathf.Pi / 2f, 0f);
        Arc(r.Position + new Vector2(r.Size.X - rad, r.Size.Y - rad), 0f, Mathf.Pi / 2f);
        Arc(r.Position + new Vector2(rad, r.Size.Y - rad), Mathf.Pi / 2f, Mathf.Pi);
        Arc(r.Position + new Vector2(rad, rad), Mathf.Pi, Mathf.Pi * 1.5f);
        pts.Add(pts[0]);
        DrawPolyline(pts.ToArray(), c, w, true);
    }
}
