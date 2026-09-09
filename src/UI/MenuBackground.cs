using Godot;
using Sentinel.Render;

namespace Sentinel.UI;

/// <summary>Animated space + Earth backdrop for the menu-family screens.</summary>
public sealed partial class MenuBackground : Node2D
{
    private Vector2[] _starF = System.Array.Empty<Vector2>();
    private float[] _mag = System.Array.Empty<float>();
    private Vector2[] _dustF = System.Array.Empty<Vector2>();
    private float _t;
    public float PlanetY = 0.30f;
    public float NebulaAlpha = 1f;
    private PlanetView _planet = null!;

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
        _dustF = new Vector2[30];
        for (int i = 0; i < _dustF.Length; i++)
            _dustF[i] = new Vector2(rng.Randf(), rng.Randf());

        _planet = new PlanetView { Skin = Game.AppRoot.Instance?.Save.Options.PlanetSkin ?? "earth" };
        AddChild(_planet);
        SetProcess(true);
        Layout();
        GetViewport().SizeChanged += Layout;
    }

    private void Layout()
    {
        var vp = GetViewportRect().Size;
        _planet.Position = new Vector2(vp.X * 0.5f, vp.Y * PlanetY);
        _planet.Diameter = Mathf.Min(vp.X * 0.7f, 360f);
    }

    public override void _Process(double delta) { _t += (float)delta; QueueRedraw(); }

    public override void _Draw()
    {
        var vp = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, vp), new Color(0.015f, 0.02f, 0.04f));

        for (int i = 0; i < _dustF.Length; i++)
        {
            float pulse = (0.012f + 0.008f * Mathf.Sin(_t * 0.3f + i)) * NebulaAlpha;
            DrawCircle(_dustF[i] * vp, vp.X * 0.20f, new Color(0.26f, 0.30f, 0.52f, pulse));
        }
        for (int i = 0; i < _starF.Length; i++)
        {
            float tw = _mag[i] * (0.6f + 0.4f * Mathf.Sin(_t * 1.5f + i * 12.9898f));
            DrawCircle(_starF[i] * vp, _mag[i] * 1.7f, new Color(1, 1, 1, tw * 0.5f));
        }

        // a couple of incoming threat blips arcing toward Earth
        var c = _planet.Position;
        float pr = _planet.Diameter * 0.5f;
        for (int k = 0; k < 3; k++)
        {
            float ph = (_t * 0.1f + k * 0.34f) % 1f;
            float a2 = k * 2.1f + _t * 0.04f;
            var ep = c + Vector2.FromAngle(a2) * Mathf.Lerp(pr + vp.X * 0.5f, pr + 20f, ph);
            DrawCircle(ep, 3f, new Color(1f, 0.4f, 0.4f, 0.8f * (1f - ph * 0.5f)));
        }
    }
}
