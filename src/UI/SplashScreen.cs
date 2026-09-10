using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// Title card shown once per launch — key art, the wordmark, "tap to begin".
/// Tapping (or a few seconds) fades into the hub menu.
/// </summary>
public sealed partial class SplashScreen : CanvasLayer
{
    public AppRoot App = null!;
    public System.Action? Done;

    private float _t;
    private bool _leaving;
    private ColorRect _fade = null!;
    private Label _prompt = null!;
    private Label _title = null!;
    private Label _sub = null!;

    public override void _Ready()
    {
        Layer = 30;
        AddChild(new MenuBackground { ShowPlanet = true, PlanetY = 0.56f });

        var col = new VBoxContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.22f, AnchorBottom = 0.22f,
            OffsetBottom = 260, Alignment = BoxContainer.AlignmentMode.Begin,
        };
        col.AddThemeConstantOverride("separation", 10);
        AddChild(col);

        _title = new Label { Text = "B E Y O N D", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0) };
        _title.AddThemeFontSizeOverride("font_size", 84);
        _title.AddThemeColorOverride("font_color", UiTheme.Accent);
        _title.AddThemeConstantOverride("outline_size", 8);
        _title.AddThemeColorOverride("font_outline_color", new Color(0.01f, 0.03f, 0.06f, 0.95f));
        col.AddChild(_title);

        _sub = new Label { Text = "hold the line over Earth", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0) };
        _sub.AddThemeFontSizeOverride("font_size", 20);
        _sub.AddThemeColorOverride("font_color", new Color(UiTheme.Accent2, 0.9f));
        col.AddChild(_sub);

        _prompt = new Label
        {
            Text = "TAP TO BEGIN", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0),
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f, OffsetTop = -110,
        };
        _prompt.AddThemeFontSizeOverride("font_size", 24);
        _prompt.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 1f));
        _prompt.AddThemeConstantOverride("outline_size", 5);
        _prompt.AddThemeColorOverride("font_outline_color", new Color(0.01f, 0.03f, 0.06f, 0.9f));
        AddChild(_prompt);

        _fade = new ColorRect { Color = new Color(0.01f, 0.012f, 0.03f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_fade);

        SetProcess(true);
        SetProcessInput(true);
    }

    public override void _Input(InputEvent e)
    {
        if (_leaving) return;
        if (e is InputEventScreenTouch { Pressed: true } or InputEventMouseButton { Pressed: true } or InputEventKey { Pressed: true })
            Leave();
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _title.Modulate = new Color(1, 1, 1, Mathf.Clamp((_t - 0.2f) * 1.4f, 0f, 1f));
        _sub.Modulate = new Color(1, 1, 1, Mathf.Clamp((_t - 0.9f) * 1.4f, 0f, 1f));
        if (_t > 1.4f)
            _prompt.Modulate = new Color(1, 1, 1, 0.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(_t * 3f)));

        if (!_leaving && _t > 7f) Leave();
        if (_leaving)
        {
            var a = Mathf.Min(1f, _fade.Color.A + (float)delta * 2.2f);
            _fade.Color = new Color(_fade.Color, a);
            if (a >= 1f) { Done?.Invoke(); QueueFree(); }
        }
    }

    private void Leave()
    {
        if (_leaving) return;
        _leaving = true;
        Sentinel.Audio.AudioManager.Instance?.Confirm();
    }
}
