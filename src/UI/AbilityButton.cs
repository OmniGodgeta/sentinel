using Godot;
using Sentinel.Config;

namespace Sentinel.UI;

/// <summary>
/// Self-drawn battle-ability button in the armoured card style: a chamfered
/// dark-metal frame with corner brackets, a role-coloured accent, a drawn glyph,
/// a cooldown wipe + seconds readout, a ready pulse and an armed flash.
/// </summary>
public sealed partial class AbilityButton : Control
{
    public System.Action? OnPress;

    private AbilityDef? _def;
    private float _cd, _cdMax, _active, _armed;
    private bool _ready;
    private float _t;

    public void Configure(AbilityDef def)
    {
        _def = def;
        CustomMinimumSize = new Vector2(96, 108);
        TooltipText = $"{def.Name}\n{def.Role}";
    }

    public void SetState(float cooldownLeft, float cooldownMax, float activeLeft, bool armed)
    {
        _cd = cooldownLeft; _cdMax = Mathf.Max(0.01f, cooldownMax);
        _active = activeLeft; _armed = armed ? 1f : 0f;
        _ready = cooldownLeft <= 0.01f;
    }

    public override void _Process(double delta) { _t += (float)delta; QueueRedraw(); }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true })
        {
            OnPress?.Invoke();
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        if (_def == null) return;
        var sz = Size;
        var role = RoleColor(_def.Role);
        float ch = 8f;                                   // corner chamfer
        var frame = new Rect2(1, 1, sz.X - 2, sz.Y - 2);

        // ---- outer glow when ready ----
        if (_ready)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 4.5f);
            for (int i = 1; i <= 3; i++)
                CutOutline(Grow(frame, i * 2.5f), ch + i * 2.5f, new Color(role, (0.18f + 0.16f * pulse) / i), 2f);
        }

        // ---- body ----
        CutFill(frame, ch, new Color(0.06f, 0.07f, 0.10f, 0.96f));
        // faint role tint wash, stronger while ready
        CutFill(Shrink(frame, 3f), ch - 2f, new Color(role, _ready ? 0.12f : 0.05f));
        // top accent header
        DrawRect(new Rect2(frame.Position.X + ch, frame.Position.Y + 3f, frame.Size.X - ch * 2f, 3f), new Color(role, _ready ? 0.95f : 0.5f));
        // frame edge
        CutOutline(frame, ch, new Color(role, _ready ? 0.9f : 0.45f), 2f);

        // ---- corner brackets ----
        var bcol = new Color(role, _ready ? 0.95f : 0.55f);
        DrawCornerBrackets(Shrink(frame, 4f), 9f, bcol);

        // ---- glyph ----
        var gc = sz * new Vector2(0.5f, 0.44f);
        DrawIcon(gc, Mathf.Min(sz.X, sz.Y) * 0.24f, new Color(role.Lightened(0.15f), _ready ? 1f : 0.7f));

        // ---- cooldown wipe (dark curtain drops from the top) ----
        if (_cd > 0.01f)
        {
            float frac = Mathf.Clamp(_cd / _cdMax, 0f, 1f);
            DrawRect(new Rect2(frame.Position, new Vector2(frame.Size.X, frame.Size.Y * frac)), new Color(0.02f, 0.03f, 0.05f, 0.74f));
            DrawRect(new Rect2(frame.Position.X, frame.Position.Y + frame.Size.Y * frac - 2f, frame.Size.X, 2f), new Color(role, 0.7f));
            DrawString(ThemeDB.FallbackFont, new Vector2(0, gc.Y + 8f), Mathf.CeilToInt(_cd).ToString(),
                       HorizontalAlignment.Center, sz.X, 22, Colors.White);
        }
        else if (_active > 0.01f)
        {
            float m = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(_t * 5f));
            CutOutline(Shrink(frame, 2f), ch - 1f, new Color(role, m), 2.5f);
        }

        // ---- armed (reticle pending) ----
        if (_armed > 0f)
        {
            float g = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(_t * 7f));
            DrawCornerBrackets(Grow(frame, 3f), 12f, new Color(1f, 0.92f, 0.35f, g));
        }

        // ---- name plate ----
        DrawRect(new Rect2(frame.Position.X + 3f, frame.Position.Y + frame.Size.Y - 16f, frame.Size.X - 6f, 13f), new Color(0f, 0f, 0f, 0.5f));
        DrawString(ThemeDB.FallbackFont, new Vector2(0, sz.Y - 5f), _def.Name.ToUpperInvariant(),
                   HorizontalAlignment.Center, sz.X, 10, new Color(role.Lightened(0.3f), 0.9f));
    }

    // ---- chamfered-rect helpers (the card frame shape) ----
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

    private void DrawIcon(Vector2 c, float s, Color col)
    {
        switch (_def!.Kind)
        {
            case "barrage":
                for (int i = -1; i <= 1; i++)
                    DrawLine(c + new Vector2(i * s * 0.55f, -s), c + new Vector2(i * s * 0.55f, s), col, 3f);
                break;
            case "lance":
                DrawLine(c + new Vector2(-s, s), c + new Vector2(s, -s), col, 4f);
                DrawCircle(c + new Vector2(s, -s), 3f, col);
                break;
            case "nova":
                DrawArc(c, s, 0, Mathf.Tau, 24, col, 3f);
                DrawArc(c, s * 0.5f, 0, Mathf.Tau, 16, col, 2f);
                break;
            case "slow":
                DrawArc(c, s, 0, Mathf.Tau, 24, col, 3f);
                DrawLine(c, c + new Vector2(0, -s * 0.8f), col, 2f);
                DrawLine(c, c + new Vector2(s * 0.6f, 0), col, 2f);
                break;
            case "snare":
                for (int i = 0; i < 6; i++)
                    DrawLine(c + Vector2.FromAngle(i * 1.05f) * s, c, col, 2f);
                break;
            case "ion":
                DrawPolyline(new[] { c + new Vector2(-s, -s), c + new Vector2(-s * 0.2f, 0), c + new Vector2(s * 0.2f, -s * 0.2f), c + new Vector2(s, s) }, col, 3f);
                break;
            case "barrier":
                DrawArc(c + new Vector2(0, s * 0.6f), s, Mathf.Pi, Mathf.Tau, 20, col, 4f);
                break;
            case "pointdef":
                DrawArc(c, s, 0, Mathf.Tau, 24, col, 2f);
                for (int i = 0; i < 4; i++)
                    DrawLine(c + Vector2.FromAngle(i * 1.57f) * s * 0.4f, c + Vector2.FromAngle(i * 1.57f) * s, col, 2f);
                break;
            case "repair":
                DrawLine(c + new Vector2(-s, 0), c + new Vector2(s, 0), col, 3f);
                DrawLine(c + new Vector2(0, -s), c + new Vector2(0, s), col, 3f);
                break;
            case "overdrive":
                DrawColoredPolygon(new[] { c + new Vector2(-s * 0.3f, -s), c + new Vector2(s * 0.4f, -s * 0.1f), c + new Vector2(-s * 0.1f, 0), c + new Vector2(s * 0.3f, s), c + new Vector2(-s * 0.4f, 0f) }, col);
                break;
            case "salvage":
                DrawArc(c, s, 0, Mathf.Tau, 8, col, 2f);
                DrawCircle(c, s * 0.35f, col);
                break;
            case "drones":
                DrawCircle(c, s * 0.35f, col);
                for (int i = 0; i < 3; i++)
                    DrawCircle(c + Vector2.FromAngle(i * 2.1f) * s, 3f, col);
                break;
            default:
                DrawCircle(c, s * 0.6f, col);
                break;
        }
    }

    private static Color RoleColor(string role) => role switch
    {
        "offense" => new Color(0.95f, 0.34f, 0.30f),
        "control" => new Color(0.60f, 0.62f, 1f),
        "defense" => new Color(0.32f, 0.80f, 1f),
        "utility" => new Color(0.98f, 0.78f, 0.30f),
        _ => new Color(0.98f, 0.82f, 0.42f),
    };
}
