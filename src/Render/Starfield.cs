using Godot;

namespace Sentinel.Render;

/// <summary>Parallax-ish static starfield drawn in world space behind everything.</summary>
public sealed partial class Starfield : Node2D
{
    private Vector2[] _stars = System.Array.Empty<Vector2>();
    private float[] _mag = System.Array.Empty<float>();
    public float Radius = 900f;

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator { Seed = 0xB1A5ED };
        int n = 200;
        _stars = new Vector2[n];
        _mag = new float[n];
        for (int i = 0; i < n; i++)
        {
            float a = rng.Randf() * Mathf.Tau;
            float d = Mathf.Sqrt(rng.Randf()) * Radius * 1.3f;
            _stars[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            _mag[i] = 0.12f + rng.Randf() * 0.6f;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        for (int i = 0; i < _stars.Length; i++)
            DrawCircle(_stars[i], _mag[i] * 1.7f, new Color(1, 1, 1, _mag[i] * 0.45f));
    }
}
