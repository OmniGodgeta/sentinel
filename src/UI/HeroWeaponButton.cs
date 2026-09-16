using Godot;

namespace Sentinel.UI;

/// <summary>
/// Self-drawn ship-weapon "attack card" for the in-mission HUD bottom bar — same
/// armoured chamfered-card language as <see cref="AbilityButton"/> (icon, level,
/// a cooldown wipe + a thin cooldown line, ready pulse) so every weapon reads as
/// a card with a clear cooldown, not a plain text button.
/// </summary>
public sealed partial class HeroWeaponButton : Control
{
    public System.Action? OnPress;

    private string _name = "", _kind = "";
    private Color _accent;
    private int _level;
    private bool _alwaysOn;
    private float _cd, _cdMax;
    private float _t;

    public void Configure(string name, string kind, Color accent, bool alwaysOn)
    {
        _name = name; _kind = kind; _accent = accent; _alwaysOn = alwaysOn;
        CustomMinimumSize = new Vector2(100, 100);
        TooltipText = name;
    }

    public void SetState(int level, float cooldownLeft, float cooldownMax)
    {
        _level = level; _cd = cooldownLeft; _cdMax = Mathf.Max(0.01f, cooldownMax);
    }

    public override void _Process(double delta) { _t += (float)delta; QueueRedraw(); }

    public override void _GuiInput(InputEvent e)
    {
        if (_alwaysOn) return;
        if (e is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true })
        {
            OnPress?.Invoke();
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var sz = Size;
        bool ready = _cd <= 0.01f;
        float ch = 9f;
        var frame = new Rect2(1, 1, sz.X - 2, sz.Y - 2);

        if (ready && !_alwaysOn)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 4.5f);
            for (int i = 1; i <= 3; i++)
                CutOutline(Grow(frame, i * 2.5f), ch + i * 2.5f, new Color(_accent, (0.16f + 0.14f * pulse) / i), 2f);
        }

        CutFill(frame, ch, new Color(0.06f, 0.07f, 0.10f, 0.96f));
        CutFill(Shrink(frame, 3f), ch - 2f, new Color(_accent, _alwaysOn ? 0.16f : (ready ? 0.12f : 0.05f)));
        DrawRect(new Rect2(frame.Position.X + ch, frame.Position.Y + 3f, frame.Size.X - ch * 2f, 3f),
                 new Color(_accent, _alwaysOn ? 0.9f : (ready ? 0.95f : 0.5f)));
        CutOutline(frame, ch, new Color(_accent, _alwaysOn ? 0.85f : (ready ? 0.9f : 0.45f)), 2f);
        DrawCornerBrackets(Shrink(frame, 4f), 10f, new Color(_accent, _alwaysOn ? 0.8f : (ready ? 0.95f : 0.55f)));

        var gc = sz * new Vector2(0.5f, 0.4f);
        DrawIcon(gc, Mathf.Min(sz.X, sz.Y) * 0.22f, new Color(_accent.Lightened(0.15f), _alwaysOn || ready ? 1f : 0.7f));

        // level chip, top-left
        DrawRect(new Rect2(frame.Position.X + 4f, frame.Position.Y + 4f, 26f, 16f), new Color(0, 0, 0, 0.55f));
        DrawString(ThemeDB.FallbackFont, new Vector2(frame.Position.X + 7f, frame.Position.Y + 17f), $"L{_level}",
                   HorizontalAlignment.Left, 26f, 13, new Color(1, 1, 1, 0.85f));

        if (_alwaysOn)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(0, gc.Y + 24f), "ON",
                       HorizontalAlignment.Center, sz.X, 15, new Color(0.6f, 1f, 0.7f));
        }
        else if (!ready)
        {
            // dark cooldown curtain from the top, plus a thin cooldown LINE right under it
            float frac = Mathf.Clamp(_cd / _cdMax, 0f, 1f);
            float curtainH = (frame.Size.Y - 20f) * frac;
            DrawRect(new Rect2(frame.Position, new Vector2(frame.Size.X, curtainH)), new Color(0.02f, 0.03f, 0.05f, 0.76f));
            DrawRect(new Rect2(frame.Position.X, frame.Position.Y + curtainH - 2f, frame.Size.X, 2f), new Color(_accent, 0.8f));
            // the cooldown "line" — a thin bar just above the name plate
            float lineY = frame.Position.Y + frame.Size.Y - 22f;
            DrawRect(new Rect2(frame.Position.X + 4f, lineY, frame.Size.X - 8f, 4f), new Color(0, 0, 0, 0.5f));
            DrawRect(new Rect2(frame.Position.X + 4f, lineY, (frame.Size.X - 8f) * (1f - frac), 4f), new Color(_accent, 0.95f));
            DrawString(ThemeDB.FallbackFont, new Vector2(0, gc.Y + 26f), $"{_cd:0.0}s",
                       HorizontalAlignment.Center, sz.X, 15, Colors.White);
        }
        else
        {
            float lineY = frame.Position.Y + frame.Size.Y - 22f;
            DrawRect(new Rect2(frame.Position.X + 4f, lineY, frame.Size.X - 8f, 4f), new Color(_accent, 0.35f));
            float m = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(_t * 5f));
            CutOutline(Shrink(frame, 2f), ch - 1f, new Color(_accent, m * 0.6f), 2f);
        }

        DrawRect(new Rect2(frame.Position.X + 3f, frame.Position.Y + frame.Size.Y - 16f, frame.Size.X - 6f, 13f), new Color(0f, 0f, 0f, 0.5f));
        DrawString(ThemeDB.FallbackFont, new Vector2(0, sz.Y - 5f), _name.ToUpperInvariant(),
                   HorizontalAlignment.Center, sz.X, 10, new Color(_accent.Lightened(0.3f), 0.9f));
    }

    private void DrawIcon(Vector2 c, float s, Color col)
    {
        switch (_kind)
        {
            case "laser":
                for (int i = -1; i <= 1; i++)
                    DrawLine(c + new Vector2(i * s * 0.5f, s), c + new Vector2(i * s * 0.2f, -s), col, 3f);
                break;
            case "missiles":
                for (int i = -1; i <= 1; i++)
                    DrawLine(c + new Vector2(i * s * 0.6f, s * 0.7f), c + new Vector2(i * s * 0.6f, -s), col, 3f);
                DrawArc(c, s * 0.3f, 0, Mathf.Tau, 10, col, 2f);
                break;
            case "ion":
                DrawPolyline(new[] { c + new Vector2(-s, -s), c + new Vector2(-s * 0.2f, 0), c + new Vector2(s * 0.2f, -s * 0.2f), c + new Vector2(s, s) }, col, 3f);
                break;
            case "yamato":
                DrawColoredPolygon(new[] { c + new Vector2(-s * 0.5f, s), c + new Vector2(s * 0.5f, s), c + new Vector2(0, -s) }, col);
                break;
            case "plasma":
                DrawArc(c, s, 0, Mathf.Tau, 24, col, 3f);
                DrawArc(c, s * 0.5f, 0, Mathf.Tau, 16, col, 2f);
                break;
            case "shield":
                DrawArc(c + new Vector2(0, s * 0.5f), s, Mathf.Pi, Mathf.Tau, 20, col, 4f);
                break;
            default:
                DrawCircle(c, s * 0.6f, col);
                break;
        }
    }

    // ---- chamfered-rect helpers (shared visual language with AbilityButton) ----
    private static Vector2[] CutPoly(Rect2 r, float c)
    {
        float x0 = r.Position.X, y0 = r.Position.Y, x1 = r.End.X, y1 = r.End.Y;
        return new[]
        {
            new Vector2(x0 + c, y0), new Vector2(x1 - c, y0), new Vector2(x1, y0 + c),
            new Vector2(x1, y1 - c), new Vector2(x1 - c, y1), new Vector2(x0 + c, y1),
            new Vector2(x0, y1 - c), new Vector2(x0, y0 + c),
        };
    }
    private void CutFill(Rect2 r, float c, Color col) => DrawColoredPolygon(CutPoly(r, c), col);
    private void CutOutline(Rect2 r, float c, Color col, float w)
    {
        var p = CutPoly(r, c);
        var loop = new Vector2[p.Length + 1];
        p.CopyTo(loop, 0); loop[^1] = p[0];
        DrawPolyline(loop, col, w, true);
    }
    private static Rect2 Grow(Rect2 r, float d) => new(r.Position - new Vector2(d, d), r.Size + new Vector2(d * 2, d * 2));
    private static Rect2 Shrink(Rect2 r, float d) => Grow(r, -d);

    private void DrawCornerBrackets(Rect2 r, float len, Color col)
    {
        Vector2 tl = r.Position, tr = new(r.End.X, r.Position.Y), br = r.End, bl = new(r.Position.X, r.End.Y);
        void L(Vector2 corner, Vector2 a, Vector2 b)
        {
            DrawLine(corner, corner + a * len, col, 2f);
            DrawLine(corner, corner + b * len, col, 2f);
        }
        L(tl, Vector2.Right, Vector2.Down);
        L(tr, Vector2.Left, Vector2.Down);
        L(br, Vector2.Left, Vector2.Up);
        L(bl, Vector2.Right, Vector2.Up);
    }
}
