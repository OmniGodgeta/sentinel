using Godot;

namespace Sentinel.Render;

/// <summary>Full-screen flashes and a low-integrity red vignette. Screen-space,
/// so it lives on its own CanvasLayer above the play field and below the HUD.</summary>
public sealed partial class ScreenFx : CanvasLayer
{
    private ColorRect _flash = null!;
    private ColorRect _vignette = null!;
    private float _flashLeft;
    private Color _flashColor = Colors.White;
    public float IntegrityFrac = 1f;

    public override void _Ready()
    {
        Layer = 8;
        _vignette = new ColorRect { Color = new Color(1f, 0.15f, 0.12f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _vignette.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_vignette);

        _flash = new ColorRect { Color = new Color(1, 1, 1, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_flash);
    }

    public void Flash(Color c, float strength)
    {
        _flashColor = c;
        _flashLeft = Mathf.Max(_flashLeft, Mathf.Clamp(strength, 0f, 0.9f));
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_flashLeft > 0f)
        {
            _flashLeft = Mathf.MoveToward(_flashLeft, 0f, dt * 2.5f);
            _flash.Color = new Color(_flashColor.R, _flashColor.G, _flashColor.B, _flashLeft);
        }
        // pulsing danger vignette below 35% integrity
        float target = IntegrityFrac < 0.35f ? (0.35f - IntegrityFrac) * 0.9f : 0f;
        float pulse = target > 0f ? target * (0.6f + 0.4f * Mathf.Sin(Time.GetTicksMsec() / 180f)) : 0f;
        _vignette.Color = new Color(1f, 0.15f, 0.12f, pulse);
    }
}
