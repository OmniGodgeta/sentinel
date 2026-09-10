using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>Title card — space, the Earth, the "Beyond" wordmark, a Start button.</summary>
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
        AddChild(new MenuBackground { ShowPlanet = true, PlanetY = 0.52f, NebulaAlpha = 0.4f });

        // wordmark — Orbitron, close above the planet; dark drop copy + bright face
        _titleShadow = MakeTitle(new Color(0.02f, 0.05f, 0.09f, 0.9f), 4f);
        AddChild(_titleShadow);
        _title = MakeTitle(new Color(0.88f, 0.93f, 1f), 0f);
        _title.AddThemeConstantOverride("outline_size", 6);
        _title.AddThemeColorOverride("font_outline_color", new Color(0.10f, 0.22f, 0.38f, 0.9f));
        AddChild(_title);

        var start = new GlowButton
        {
            Text = "START", FontSize = 32, Primary = true,
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.70f, AnchorBottom = 0.70f,
            OffsetLeft = -150, OffsetRight = 150, OffsetTop = 0, OffsetBottom = 78,
        };
        start.SetFont(UiTheme.Display);
        start.Pressed += Leave;
        AddChild(start);

        _fade = new ColorRect { Color = new Color(0.01f, 0.012f, 0.03f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_fade);

        SetProcess(true);
    }

    private static Label MakeTitle(Color col, float dy)
    {
        var l = new Label
        {
            Text = "Beyond", HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.335f, AnchorBottom = 0.335f,
            OffsetTop = dy, Modulate = new Color(1, 1, 1, 0),
        };
        l.AddThemeFontOverride("font", UiTheme.Display);
        l.AddThemeFontSizeOverride("font_size", 76);
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
