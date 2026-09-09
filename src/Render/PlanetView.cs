using Godot;

namespace Sentinel.Render;

/// <summary>A shader-rendered rotating Earth. Used in-mission and on the menu.</summary>
public sealed partial class PlanetView : Node2D
{
    private float _diameter = 240f;
    public float Diameter
    {
        get => _diameter;
        set { _diameter = value; QueueRedraw(); _disc?.QueueRedraw(); }
    }

    private EarthDisc _disc = null!;
    private static Texture2D? _white;

    private static Texture2D White()
    {
        if (_white != null) return _white;
        var img = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
        img.Fill(Colors.White);
        _white = ImageTexture.CreateFromImage(img);
        return _white;
    }

    public override void _Ready()
    {
        _disc = new EarthDisc { Owner2D = this, Material = Art.EarthMaterial() };
        AddChild(_disc);
        SetProcess(true);
    }

    public override void _Process(double delta) => _disc.QueueRedraw();

    public override void _Draw()
    {
        float r = _diameter * 0.5f;
        for (int g = 6; g >= 1; g--)
            DrawCircle(Vector2.Zero, r + g * (r * 0.06f), new Color(0.35f, 0.55f, 0.95f, 0.03f));
    }

    /// <summary>Child that owns the shader material and draws the Earth quad with UVs.</summary>
    private sealed partial class EarthDisc : Node2D
    {
        public PlanetView Owner2D = null!;
        public override void _Draw()
        {
            float r = Owner2D.Diameter * 0.5f;
            DrawTextureRect(White(), new Rect2(-r, -r, Owner2D.Diameter, Owner2D.Diameter), false);
        }
    }
}
