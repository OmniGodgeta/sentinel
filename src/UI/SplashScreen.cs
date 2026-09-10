using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// Title card shown once per launch — space, the Earth, the "Beyond" wordmark and
/// a Start button. Laid out after the PDTD title screen.
/// </summary>
public sealed partial class SplashScreen : CanvasLayer
{
    public AppRoot App = null!;
    public System.Action? Done;

    private float _t;
    private bool _leaving;
    private ColorRect _fade = null!;
    private Label _title = null!;
    private Label _titleShadow = null!;

    public override void _Ready()
    {
        Layer = 30;
        AddChild(new MenuBackground { ShowPlanet = true, PlanetY = 0.52f, NebulaAlpha = 0.45f });

        // wordmark — a chrome-ish look: dark drop copy + a bright silver face
        _titleShadow = MakeTitle(new Color(0.02f, 0.05f, 0.09f, 0.9f), new Vector2(0, 4));
        AddChild(_titleShadow);
        _title = MakeTitle(new Color(0.86f, 0.92f, 1f), Vector2.Zero);
        _title.AddThemeConstantOverride("outline_size", 6);
        _title.AddThemeColorOverride("font_outline_color", new Color(0.10f, 0.22f, 0.38f, 0.9f));
        AddChild(_title);

        var start = new GlowButton
        {
            Text = "START", FontSize = 34, Primary = true,
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = -160, OffsetRight = 160, OffsetTop = -150, OffsetBottom = -74,
        };
        start.Pressed += Leave;
        AddChild(start);

        _fade = new ColorRect { Color = new Color(0.01f, 0.012f, 0.03f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_fade);

        SetProcess(true);
    }

    private static Label MakeTitle(Color col, Vector2 off)
    {
        var l = new Label
        {
            Text = "Beyond", HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.14f, AnchorBottom = 0.14f,
            OffsetTop = off.Y, Modulate = new Color(1, 1, 1, 0),
        };
        l.AddThemeFontSizeOverride("font_size", 96);
        l.AddThemeColorOverride("font_color", col);
        return l;
    }

    private void Leave()
    {
        if (_leaving) return;
        _leaving = true;
        Sentinel.Audio.AudioManager.Instance?.Confirm();
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        float a = Mathf.Clamp((_t - 0.15f) * 1.6f, 0f, 1f);
        _title.Modulate = new Color(1, 1, 1, a);
        _titleShadow.Modulate = new Color(1, 1, 1, a);

        if (_leaving)
        {
            var fa = Mathf.Min(1f, _fade.Color.A + (float)delta * 2.4f);
            _fade.Color = new Color(_fade.Color, fa);
            if (fa >= 1f) { Done?.Invoke(); QueueFree(); }
        }
    }
}
