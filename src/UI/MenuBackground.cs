using Godot;

namespace Sentinel.UI;

/// <summary>Animated space + planet backdrop shared by the menu-family screens.</summary>
public sealed partial class MenuBackground : Node2D
{
    // stars/dust stored as fractions of the viewport so they adapt to any size
    private Vector2[] _starF = System.Array.Empty<Vector2>();
    private float[] _mag = System.Array.Empty<float>();
    private Vector2[] _dustF = System.Array.Empty<Vector2>();
    private float _t;
    public float PlanetScale = 1f;
    public float PlanetY = 0.30f;   // fraction of viewport height
    public Vector2 PlanetCenter { get; private set; }

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator { Seed = 424242 };
        _starF = new Vector2[240];
        _mag = new float[240];
        for (int i = 0; i < _starF.Length; i++)
        {
            _starF[i] = new Vector2(rng.Randf(), rng.Randf());
            _mag[i] = 0.1f + rng.Randf() * 0.7f;
        }
        _dustF = new Vector2[36];
        for (int i = 0; i < _dustF.Length; i++)
            _dustF[i] = new Vector2(rng.Randf(), rng.Randf());
        SetProcess(true);
    }

    public override void _Process(double delta) { _t += (float)delta; QueueRedraw(); }

    public override void _Draw()
    {
        var vp = GetViewportRect().Size;
        PlanetCenter = new Vector2(vp.X * 0.5f, vp.Y * PlanetY);
        DrawRect(new Rect2(Vector2.Zero, vp), new Color(0.02f, 0.02f, 0.045f));

        // nebula wash
        for (int i = 0; i < _dustF.Length; i++)
        {
            float pulse = 0.02f + 0.015f * Mathf.Sin(_t * 0.3f + i);
            DrawCircle(_dustF[i] * vp, vp.X * 0.22f, new Color(0.3f, 0.35f, 0.6f, pulse));
        }
        for (int i = 0; i < _starF.Length; i++)
        {
            float tw = _mag[i] * (0.6f + 0.4f * Mathf.Sin(_t * 1.5f + i * 12.9898f));
            DrawCircle(_starF[i] * vp, _mag[i] * 1.7f, new Color(1, 1, 1, tw * 0.5f));
        }

        // planet
        float pr = Mathf.Min(vp.X * 0.34f, 170f) * PlanetScale;
        var c = PlanetCenter;
        for (int g = 6; g >= 1; g--)
            DrawCircle(c, pr + g * 12f, new Color(0.35f, 0.55f, 0.95f, 0.035f));
        DrawCircle(c, pr, new Color(0.10f, 0.14f, 0.24f));
        // rotating surface bands
        for (int b = 0; b < 4; b++)
        {
            float off = _t * 0.15f + b * 1.4f;
            DrawArc(c, pr * (0.35f + b * 0.16f), off, off + 2.4f, 24, new Color(0.5f, 0.6f, 0.85f, 0.06f), 5f);
        }
        DrawArc(c, pr, 0, Mathf.Tau, 64, new Color(0.4f, 0.7f, 1f, 0.5f), 2f);
        // a slow orbiting ship dot
        float sa = _t * 0.5f;
        var sp = c + Vector2.FromAngle(sa) * (pr + 60f);
        DrawColoredPolygon(new[] { sp + Vector2.FromAngle(sa + 1.6f) * 8f, sp + Vector2.FromAngle(sa + 2.9f) * 6f, sp + Vector2.FromAngle(sa + 0.3f) * 6f },
                           new Color(0.6f, 0.9f, 1f, 0.8f));
        // incoming threat blips
        for (int k = 0; k < 3; k++)
        {
            float ph = (_t * 0.12f + k * 0.33f) % 1f;
            float a2 = k * 2.1f + _t * 0.05f;
            var ep = c + Vector2.FromAngle(a2) * Mathf.Lerp(pr + 380f, pr + 30f, ph);
            DrawCircle(ep, 3f, new Color(1f, 0.4f, 0.4f, 0.8f * (1f - ph * 0.5f)));
        }
    }
}
