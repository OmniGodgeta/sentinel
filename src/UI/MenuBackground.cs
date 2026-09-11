using Godot;
using Sentinel.Render;

namespace Sentinel.UI;

/// <summary>
/// Backdrop for the menu-family screens. Two modes:
///  • procedural — the "Beyond" key-art look: teal nebula to the left, magenta to
///    the right, a deep starfield, and the rotating shader Earth in the centre.
///  • image — a photographic space background (used by the star map) with a
///    legibility scrim.
/// </summary>
public sealed partial class MenuBackground : Node2D
{
    private Vector2[] _starF = System.Array.Empty<Vector2>();
    private float[] _mag = System.Array.Empty<float>();
    private float[] _twk = System.Array.Empty<float>();
    private Cloud[] _clouds = System.Array.Empty<Cloud>();
    private float _t;

    public float PlanetY = 0.42f;
    public float PlanetScale = 1f;
    public float NebulaAlpha = 1f;
    public string Image = "";
    public bool ShowPlanet = true;
    /// <summary>Slow idle zoom + pan on the planet so a static background screen still breathes.</summary>
    public bool AnimatePlanet = true;

    private Vector2 _basePos;
    private float _baseDiam;

    private PlanetView? _planet;
    private Texture2D? _tex;

    private struct Cloud { public Vector2 P; public float R, Drift, Phase; public bool Teal; }

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator { Seed = 424242 };
        int n = 320;
        _starF = new Vector2[n];
        _mag = new float[n];
        _twk = new float[n];
        for (int i = 0; i < n; i++)
        {
            _starF[i] = new Vector2(rng.Randf(), rng.Randf());
            _mag[i] = 0.08f + rng.Randf() * rng.Randf() * 0.9f;   // mostly faint, a few bright
            _twk[i] = rng.Randf() * 10f;
        }

        _clouds = new Cloud[26];
        for (int i = 0; i < _clouds.Length; i++)
        {
            bool teal = i % 2 == 0;
            _clouds[i] = new Cloud
            {
                P = new Vector2(teal ? rng.RandfRange(-0.1f, 0.55f) : rng.RandfRange(0.45f, 1.1f),
                                rng.RandfRange(-0.15f, 0.65f)),
                R = rng.RandfRange(0.14f, 0.34f),
                Drift = rng.RandfRange(0.006f, 0.02f) * (teal ? -1f : 1f),
                Phase = rng.Randf() * 10f,
                Teal = teal,
            };
        }

        if (!string.IsNullOrEmpty(Image) && ResourceLoader.Exists(Image))
            _tex = GD.Load<Texture2D>(Image);

        if (_tex == null && ShowPlanet)
        {
            _planet = new PlanetView { Skin = Game.AppRoot.Instance?.Save.Options.PlanetSkin ?? "earth" };
            AddChild(_planet);
        }
        SetProcess(true);
        Layout();
        GetViewport().SizeChanged += Layout;
    }

    private void Layout()
    {
        if (_planet == null) return;
        var vp = GetViewportRect().Size;
        _basePos = new Vector2(vp.X * 0.5f, vp.Y * PlanetY);
        _baseDiam = Mathf.Min(vp.X * 0.74f, 420f) * PlanetScale;
        _planet.Position = _basePos;
        _planet.Diameter = _baseDiam;
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_planet != null && AnimatePlanet)
        {
            float zoom = 1f + 0.05f * Mathf.Sin(_t * 0.045f);
            var pan = new Vector2(Mathf.Sin(_t * 0.028f) * _baseDiam * 0.05f, Mathf.Cos(_t * 0.037f) * _baseDiam * 0.035f);
            _planet.Position = _basePos + pan;
            _planet.Diameter = _baseDiam * zoom;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var vp = GetViewportRect().Size;
        var ink = new Color(0.012f, 0.016f, 0.035f);

        if (_tex != null) { DrawImageMode(vp, ink); return; }

        // deep space base with a soft radial vignette toward the corners
        DrawRect(new Rect2(Vector2.Zero, vp), new Color(0.02f, 0.03f, 0.06f));
        DrawRect(new Rect2(Vector2.Zero, vp), new Color(ink, 0.0f));

        var teal = new Color(0.20f, 0.72f, 0.78f);
        var mag = new Color(0.86f, 0.24f, 0.55f);

        // layered nebula clouds — cheap additive blobs that drift
        foreach (var cl in _clouds)
        {
            float breath = 0.6f + 0.4f * Mathf.Sin(_t * 0.25f + cl.Phase);
            var p = new Vector2(Mathf.PosMod(cl.P.X + _t * cl.Drift, 1.3f) - 0.15f, cl.P.Y) * vp;
            var col = cl.Teal ? teal : mag;
            float baseA = (cl.Teal ? 0.075f : 0.08f) * NebulaAlpha * breath;
            for (int k = 4; k >= 1; k--)
                DrawCircle(p, vp.X * cl.R * (0.4f + k * 0.32f), new Color(col, baseA / (k * 1.5f)));
        }

        // starfield — twinkle, a few with cross flares
        for (int i = 0; i < _starF.Length; i++)
        {
            float tw = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(_t * 1.2f + _twk[i]));
            var sp = _starF[i] * vp;
            float r = _mag[i] * 1.7f;
            DrawCircle(sp, r, new Color(1f, 1f, 1f, _mag[i] * tw * 0.7f));
            if (_mag[i] > 0.8f)
            {
                float fl = r * 4f * tw;
                var fc = new Color(0.8f, 0.95f, 1f, _mag[i] * tw * 0.35f);
                DrawLine(sp - new Vector2(fl, 0), sp + new Vector2(fl, 0), fc, 1f);
                DrawLine(sp - new Vector2(0, fl), sp + new Vector2(0, fl), fc, 1f);
            }
        }

        // vignette
        int vb = 7;
        for (int i = 0; i < vb; i++)
        {
            float f = i / (vb - 1f);
            DrawRect(new Rect2(0, 0, vp.X, vp.Y * 0.10f * (1f - f) + 4f), new Color(ink, 0.10f));
            DrawRect(new Rect2(0, vp.Y - vp.Y * 0.16f * (1f - f) - 4f, vp.X, vp.Y * 0.16f * (1f - f) + 4f), new Color(ink, 0.12f));
        }

        // a faint glow ring behind Earth so it reads as the focal point
        if (_planet != null)
        {
            var c = _planet.Position;
            float pr = _planet.Diameter * 0.5f;
            for (int k = 4; k >= 1; k--)
                DrawCircle(c, pr * (1f + k * 0.10f), new Color(0.35f, 0.7f, 0.95f, 0.03f));
        }
    }

    private void DrawImageMode(Vector2 vp, Color ink)
    {
        DrawRect(new Rect2(Vector2.Zero, vp), new Color(0.01f, 0.012f, 0.03f));
        Vector2 ts = _tex!.GetSize();
        float scale = Mathf.Max(vp.X / ts.X, vp.Y / ts.Y);
        Vector2 draw = ts * scale;
        Vector2 pos = (vp - draw) * 0.5f;
        DrawTextureRect(_tex, new Rect2(pos, draw), false, new Color(1, 1, 1, 0.85f));

        DrawRect(new Rect2(Vector2.Zero, vp), new Color(ink, 0.24f));
        const int bands = 22;
        for (int i = 0; i < bands; i++)
        {
            float f = i / (bands - 1f);
            float a = 0.20f + 0.55f * f * f + 0.16f * Mathf.Exp(-40f * (f - 0.30f) * (f - 0.30f));
            DrawRect(new Rect2(0, vp.Y * f, vp.X, vp.Y / bands + 2f), new Color(ink, a));
        }
        for (int i = 0; i < 90; i++)
        {
            float tw = _mag[i] * (0.6f + 0.4f * Mathf.Sin(_t * 1.5f + _twk[i]));
            DrawCircle(_starF[i] * vp, _mag[i] * 1.5f, new Color(1, 1, 1, tw * 0.35f));
        }
    }
}
