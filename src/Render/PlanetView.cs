using Godot;

namespace Sentinel.Render;

/// <summary>The defended planet. "earth" is a shader-rendered rotating globe from
/// NASA Blue Marble maps; other skins are Kenney planet sprites, slowly spun.</summary>
public sealed partial class PlanetView : Node2D
{
    public string Skin = "earth";
    private float _diameter = 240f;
    public float Diameter
    {
        get => _diameter;
        set { _diameter = value; QueueRedraw(); _disc?.QueueRedraw(); }
    }

    private EarthDisc _disc = null!;
    private Texture2D? _skinTex;
    private float _spin;
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
        if (Skin == "earth")
        {
            _disc = new EarthDisc { Owner2D = this, Material = Art.EarthMaterial() };
            AddChild(_disc);
        }
        else
        {
            string p = $"res://assets/game/planets/{Skin}.png";
            _skinTex = ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : null;
        }
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _spin += (float)delta * 0.03f;
        _disc?.QueueRedraw();
        if (_skinTex != null) QueueRedraw();
    }

    public override void _Draw()
    {
        float r = _diameter * 0.5f;
        for (int g = 6; g >= 1; g--)
            DrawCircle(Vector2.Zero, r + g * (r * 0.06f), new Color(0.35f, 0.55f, 0.95f, 0.03f));

        if (_skinTex != null)
        {
            var ts = _skinTex.GetSize();
            float sc = _diameter / Mathf.Max(ts.X, ts.Y);
            DrawSetTransform(Vector2.Zero, _spin * 0.4f, new Vector2(sc, sc));
            DrawTexture(_skinTex, -ts * 0.5f, Colors.White);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 48, new Color(0.5f, 0.7f, 1f, 0.35f), 2f);
        }
    }

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
