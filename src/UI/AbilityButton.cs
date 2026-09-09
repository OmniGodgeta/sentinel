using Godot;
using Sentinel.Config;

namespace Sentinel.UI;

/// <summary>Self-drawn ability button: role-coloured icon, radial cooldown sweep,
/// active-timer ring, and a ready pulse. Tapping fires the callback.</summary>
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
        CustomMinimumSize = new Vector2(104, 104);
        TooltipText = def.Name;
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
        var c = sz * 0.5f;
        float r = Mathf.Min(sz.X, sz.Y) * 0.42f;
        var role = RoleColor(_def.Role);

        // base disc
        DrawCircle(c, r, new Color(0.08f, 0.09f, 0.13f));
        DrawArc(c, r, 0, Mathf.Tau, 40, new Color(role, _ready ? 0.9f : 0.3f), 2f);

        if (_ready)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 4f);
            DrawArc(c, r + 2f, 0, Mathf.Tau, 40, new Color(role, 0.25f + 0.35f * pulse), 3f);
        }

        // icon
        DrawIcon(c, r * 0.62f, role);

        // cooldown sweep (dark wedge shrinking clockwise)
        if (_cd > 0.01f)
        {
            float frac = Mathf.Clamp(_cd / _cdMax, 0f, 1f);
            int steps = 32;
            var pts = new System.Collections.Generic.List<Vector2> { c };
            float start = -Mathf.Pi / 2f;
            for (int i = 0; i <= steps; i++)
                pts.Add(c + Vector2.FromAngle(start + Mathf.Tau * frac * i / steps) * r);
            DrawColoredPolygon(pts.ToArray(), new Color(0, 0, 0, 0.66f));
            DrawString(ThemeDB.FallbackFont, c - new Vector2(13, -6), Mathf.CeilToInt(_cd).ToString(),
                       HorizontalAlignment.Center, 26, 20, Colors.White);
        }
        else if (_active > 0.01f)
        {
            DrawArc(c, r - 2f, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * 0.999f, 32, new Color(role, 0.8f), 3f);
        }

        if (_armed > 0f)
        {
            float g = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(_t * 6f));
            DrawArc(c, r + 4f, 0, Mathf.Tau, 40, new Color(1f, 0.95f, 0.4f, g), 3f);
        }

        // name
        DrawString(ThemeDB.FallbackFont, new Vector2(0, sz.Y - 3), _def.Name,
                   HorizontalAlignment.Center, sz.X, 12, new Color(1, 1, 1, 0.75f));
    }

    private void DrawIcon(Vector2 c, float s, Color col)
    {
        switch (_def!.Kind)
        {
            case "barrage":
                for (int i = -1; i <= 1; i++)
                    DrawLine(c + new Vector2(i * s * 0.5f, -s), c + new Vector2(i * s * 0.5f, s), col, 3f);
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
        "offense" => new Color(1f, 0.5f, 0.4f),
        "control" => new Color(0.62f, 0.62f, 1f),
        "defense" => new Color(0.42f, 0.86f, 1f),
        _ => new Color(1f, 0.85f, 0.45f),
    };
}
