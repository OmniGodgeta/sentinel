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

    /// <summary><paramref name="compact"/> is for the auto-firing planet systems (the missile
    /// battery and the orbital sentinels) — same card, drawn small, since they sit alongside
    /// the bigger tappable ship-weapon cards in the same wrapping row.</summary>
    public void Configure(string name, string kind, Color accent, bool alwaysOn, bool compact = false)
    {
        _name = name; _kind = kind; _accent = accent; _alwaysOn = alwaysOn;
        CustomMinimumSize = compact ? new Vector2(132, 132) : new Vector2(150, 150);
        TooltipText = name;
    }

    public void SetState(int level, float cooldownLeft, float cooldownMax)
    {
        _level = level; _cd = cooldownLeft; _cdMax = Mathf.Max(0.01f, cooldownMax);
    }

    private float _ultCharge;
    private bool _ultActive;
    private int _stars, _starMax;

    /// <summary>Ultimate charge (0..1), whether it's firing, and the sentinel's star
    /// pips. A charging ultimate draws a rising ring around the tile; a firing one
    /// animates the whole card, which is PDTD's "the card becomes animated" cue.</summary>
    public void SetUltimate(float charge, bool active, int stars, int starMax)
    {
        _ultCharge = Mathf.Clamp(charge, 0f, 1f);
        _ultActive = active;
        _stars = stars; _starMax = starMax;
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

    /// <summary>How many segments the charge bar under the tile is cut into. PDTD draws a
    /// row of short gold blocks rather than one continuous bar.</summary>
    private const int ChargeSegments = 9;

    public override void _Draw()
    {
        // PDTD's HUD tile: a square of full-bleed sentinel art in a light chamfered
        // frame, with a segmented charge bar underneath. No level chip, no "ON" text and
        // no name plate — the art identifies the weapon and the bar carries the state.
        var sz = Size;
        bool ready = _cd <= 0.01f;
        float k = sz.X / 136f;
        float ch = 11f * k;

        float barH = 9f * k;                       // the segmented bar
        float barGap = 5f * k;
        float artSide = Mathf.Max(8f, sz.Y - barH - barGap - 2f);
        var frame = new Rect2(1, 1, sz.X - 2, artSide);

        if (ready && !_alwaysOn)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 4.5f);
            for (int i = 1; i <= 2; i++)
                CutOutline(Grow(frame, i * 2.5f * k), ch + i * 2.5f * k, new Color(_accent, (0.14f + 0.12f * pulse) / i), 2f * k);
        }

        CutFill(frame, ch, new Color(0.05f, 0.07f, 0.11f, 0.98f));

        // --- full-bleed art, clipped to the chamfered frame ---
        var tile = Sentinel.Render.Art.Pdtd("tiles/" + TileFor(_kind));
        if (tile != null)
        {
            var poly = CutPoly(Shrink(frame, 2f * k), ch - 1.5f * k);
            // UVs map the frame's bounds onto the square tile texture, so the art fills
            // the chamfer corner-to-corner instead of floating in the middle.
            var inner = Shrink(frame, 2f * k);
            var uv = new Vector2[poly.Length];
            for (int i = 0; i < poly.Length; i++)
                uv[i] = (poly[i] - inner.Position) / inner.Size;
            float dim = _alwaysOn || ready ? 1f : 0.55f;
            DrawPolygon(poly, new[] { new Color(dim, dim, dim, 1f) }, uv, tile);
        }
        else
        {
            CutFill(Shrink(frame, 3f * k), ch - 2f * k, new Color(_accent, _alwaysOn ? 0.16f : (ready ? 0.12f : 0.05f)));
            DrawIcon(frame.Position + frame.Size * 0.5f, Mathf.Min(sz.X, artSide) * 0.24f,
                     new Color(_accent.Lightened(0.15f), _alwaysOn || ready ? 1f : 0.7f));
        }

        // --- frame over the art ---
        CutOutline(frame, ch, new Color(0.62f, 0.74f, 0.88f, _alwaysOn || ready ? 0.85f : 0.42f), 2.2f * k);
        CutOutline(Shrink(frame, 3f * k), ch - 2f * k, new Color(1f, 1f, 1f, 0.10f), 1.4f * k);
        DrawCornerBrackets(Shrink(frame, 5f * k), 9f * k, new Color(_accent, _alwaysOn ? 0.75f : (ready ? 0.9f : 0.45f)));

        // --- cooldown curtain ---
        if (!_alwaysOn && !ready)
        {
            float frac = Mathf.Clamp(_cd / _cdMax, 0f, 1f);
            float curtainH = frame.Size.Y * frac;
            DrawRect(new Rect2(frame.Position, new Vector2(frame.Size.X, curtainH)), new Color(0.02f, 0.03f, 0.05f, 0.68f));
            DrawRect(new Rect2(frame.Position.X, frame.Position.Y + curtainH - 2f * k, frame.Size.X, 2f * k), new Color(_accent, 0.85f));
            DrawString(ThemeDB.FallbackFont, new Vector2(0, frame.Position.Y + frame.Size.Y * 0.58f), $"{_cd:0.0}",
                       HorizontalAlignment.Center, sz.X, Mathf.RoundToInt(22 * k), new Color(1, 1, 1, 0.92f));
        }

        // --- ultimate ---
        // Charging: a bright arc creeps around the tile's edge. Firing: the whole frame
        // pulses gold and the art brightens — PDTD animates the card while its ultimate
        // is up, and this is the closest thing the 2D HUD has to that.
        if (_ultActive)
        {
            float p = 0.5f + 0.5f * Mathf.Sin(_t * 12f);
            for (int i = 1; i <= 3; i++)
                CutOutline(Grow(frame, i * 3f * k), ch + i * 3f * k,
                           new Color(1f, 0.86f, 0.35f, (0.55f + 0.35f * p) / i), 3f * k);
            CutOutline(frame, ch, new Color(1f, 0.95f, 0.65f, 0.9f), 3.5f * k);
            DrawString(ThemeDB.FallbackFont, new Vector2(0, frame.Position.Y + 22f * k), "ULTIMATE",
                       HorizontalAlignment.Center, sz.X, Mathf.RoundToInt(15 * k),
                       new Color(1f, 0.93f, 0.55f, 0.95f));
        }
        else if (_ultCharge > 0.001f)
        {
            // a ring that fills clockwise from the top as the ultimate charges
            float r = Mathf.Min(frame.Size.X, frame.Size.Y) * 0.5f - 2f * k;
            var c0 = frame.Position + frame.Size * 0.5f;
            DrawArc(c0, r, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * _ultCharge, 40,
                    new Color(1f, 0.86f, 0.35f, _ultCharge >= 1f ? 0.95f : 0.5f), 3f * k);
        }

        // --- star pips ---
        // Same three-pip row PDTD puts under an equipped sentinel, purple once promoted.
        if (_starMax > 0)
        {
            float pipR = 3.2f * k;
            float gap = 10f * k;
            float total = (_starMax - 1) * gap;
            float sy = frame.Position.Y + frame.Size.Y - 11f * k;
            var litc = _level > 1 ? new Color(0.76f, 0.52f, 1f) : new Color(1f, 0.93f, 0.62f);
            for (int s = 0; s < _starMax; s++)
            {
                var pc = new Vector2(sz.X * 0.5f - total * 0.5f + s * gap, sy);
                DrawCircle(pc, pipR + 1.2f * k, new Color(0, 0, 0, 0.55f));
                DrawCircle(pc, pipR, s < _stars ? litc : new Color(0.42f, 0.47f, 0.55f, 0.8f));
            }
        }

        // --- segmented charge bar ---
        // Always-on systems read as fully charged; everything else fills left-to-right as
        // the cooldown drains, so a nearly-ready weapon is legible at a glance.
        float fill = _alwaysOn ? 1f : 1f - Mathf.Clamp(_cd / _cdMax, 0f, 1f);
        float barY = frame.Position.Y + frame.Size.Y + barGap;
        float segGap = 2f * k;
        float segW = (sz.X - 2f - segGap * (ChargeSegments - 1)) / ChargeSegments;
        var lit = _alwaysOn ? new Color(0.60f, 0.95f, 0.72f) : new Color(0.96f, 0.82f, 0.45f);
        for (int i = 0; i < ChargeSegments; i++)
        {
            float x = 1f + i * (segW + segGap);
            // a segment lights once the fill passes its midpoint
            bool on = fill >= (i + 0.5f) / ChargeSegments;
            DrawRect(new Rect2(x, barY, segW, barH), on ? lit : new Color(0.28f, 0.33f, 0.40f, 0.55f));
        }
    }

    /// <summary>Which `pdtd/tiles/*` art a weapon kind wears.
    ///
    /// The orbital kinds already match PDTD's own names, so they pass straight through.
    /// The ship's weapons and the planet's missile battery are Beyond's own and have no
    /// PDTD counterpart, so each borrows the closest sentinel's art — otherwise those
    /// tiles fall back to a drawn glyph and sit in the bar looking like placeholder
    /// programmer art next to eight real ones.</summary>
    private static string TileFor(string kind) => kind switch
    {
        "missiles" => "missile",        // the planet's missile battery
        "ion" => "lightning",           // Ion Cannon -> Chain Lightning craft
        "yamato" => "railgun",          // one huge shot -> the railgun platform
        "plasma" => "rad_zone",         // a sustained field
        "shield" => "force_field",      // Shields Boost -> the gravity/shield platform
        _ => kind,
    };

    private void DrawIcon(Vector2 c, float s, Color col)
    {
        float k = Size.X / 136f;
        switch (_kind)
        {
            case "laser":
                for (int i = -1; i <= 1; i++)
                    DrawLine(c + new Vector2(i * s * 0.5f, s), c + new Vector2(i * s * 0.2f, -s), col, 3f * k);
                break;
            case "missiles":
                for (int i = -1; i <= 1; i++)
                    DrawLine(c + new Vector2(i * s * 0.6f, s * 0.7f), c + new Vector2(i * s * 0.6f, -s), col, 3f * k);
                DrawArc(c, s * 0.3f, 0, Mathf.Tau, 10, col, 2f * k);
                break;
            case "ion":
                DrawPolyline(new[] { c + new Vector2(-s, -s), c + new Vector2(-s * 0.2f, 0), c + new Vector2(s * 0.2f, -s * 0.2f), c + new Vector2(s, s) }, col, 3f * k);
                break;
            case "yamato":
                DrawColoredPolygon(new[] { c + new Vector2(-s * 0.5f, s), c + new Vector2(s * 0.5f, s), c + new Vector2(0, -s) }, col);
                break;
            case "plasma":
                DrawArc(c, s, 0, Mathf.Tau, 24, col, 3f * k);
                DrawArc(c, s * 0.5f, 0, Mathf.Tau, 16, col, 2f * k);
                break;
            case "shield":
                DrawArc(c + new Vector2(0, s * 0.5f), s, Mathf.Pi, Mathf.Tau, 20, col, 4f * k);
                break;

            // ---- orbital sentinel kinds (data/orbital_weapons.json) ----
            case "beam_laser":   // one thick locked beam
                DrawLine(c + new Vector2(-s, s * 0.6f), c + new Vector2(s, -s * 0.6f), col, 5f * k);
                DrawCircle(c + new Vector2(s, -s * 0.6f), 4f * k, Colors.White);
                break;
            case "lightning":    // zigzag chain
                DrawPolyline(new[] { c + new Vector2(-s * 0.7f, -s), c + new Vector2(0, -s * 0.15f),
                                     c + new Vector2(-s * 0.3f, 0), c + new Vector2(s * 0.6f, s) }, col, 3f * k);
                break;
            case "rad_line":     // two relay nodes joined by a link
                DrawLine(c + new Vector2(-s * 0.8f, 0), c + new Vector2(s * 0.8f, 0), col, 3f * k);
                DrawCircle(c + new Vector2(-s * 0.8f, 0), 4.5f * k, col);
                DrawCircle(c + new Vector2(s * 0.8f, 0), 4.5f * k, col);
                break;
            case "rad_zone":     // trefoil-ish drifting zone
                DrawArc(c, s * 0.85f, 0, Mathf.Tau, 20, col, 2.5f * k);
                for (int i = 0; i < 3; i++)
                    DrawLine(c, c + Vector2.FromAngle(i * Mathf.Tau / 3f - Mathf.Pi / 2f) * s * 0.8f, col, 3f * k);
                break;
            case "shock_orb":    // ball lightning
                DrawCircle(c, s * 0.45f, col);
                for (int i = 0; i < 6; i++)
                {
                    var a = i * Mathf.Tau / 6f;
                    DrawLine(c + Vector2.FromAngle(a) * s * 0.55f, c + Vector2.FromAngle(a) * s, col, 2f * k);
                }
                break;
            case "waterdrop":    // droplet
            {
                var pts = new[] { c + new Vector2(0, -s), c + new Vector2(-s * 0.62f, s * 0.35f),
                                  c + new Vector2(0, s * 0.9f), c + new Vector2(s * 0.62f, s * 0.35f) };
                DrawColoredPolygon(pts, col);
                break;
            }
            case "space_bomb":   // bomb + blast spikes
                DrawCircle(c, s * 0.5f, col);
                for (int i = 0; i < 8; i++)
                {
                    var a = i * Mathf.Tau / 8f;
                    DrawLine(c + Vector2.FromAngle(a) * s * 0.62f, c + Vector2.FromAngle(a) * s, col, 2.5f * k);
                }
                break;
            case "force_field":  // nested domes
                for (int i = 1; i <= 3; i++)
                    DrawArc(c, s * (0.35f + i * 0.22f), 0, Mathf.Tau, 20, col, 2.2f * k);
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
        float bw = 2f * (Size.X / 136f);
        Vector2 tl = r.Position, tr = new(r.End.X, r.Position.Y), br = r.End, bl = new(r.Position.X, r.End.Y);
        void L(Vector2 corner, Vector2 a, Vector2 b)
        {
            DrawLine(corner, corner + a * len, col, bw);
            DrawLine(corner, corner + b * len, col, bw);
        }
        L(tl, Vector2.Right, Vector2.Down);
        L(tr, Vector2.Left, Vector2.Down);
        L(br, Vector2.Left, Vector2.Up);
        L(bl, Vector2.Right, Vector2.Up);
    }
}
