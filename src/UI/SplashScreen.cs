using Godot;
using Sentinel.Game;
using Sentinel.Meta;

namespace Sentinel.UI;

/// <summary>Title card — space, the Earth, the "Beyond" wordmark, a Start button.
/// Also the mandatory-update gate: if GitHub has a newer release, Start is
/// replaced by a Download button and there is no way past this screen.</summary>
public sealed partial class SplashScreen : CanvasLayer
{
    public AppRoot App = null!;
    public System.Action? Done;

    private const string Repo = "OmniGodgeta/sentinel";

    private float _t;
    private bool _leaving;
    private ColorRect _fade = null!;
    private Label _title = null!;
    private Label _titleShadow = null!;
    private GlowButton _start = null!;
    private Label _checking = null!;
    private Control _gate = null!;

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

        _start = new GlowButton
        {
            Text = "START", FontSize = 32, Primary = true,
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.70f, AnchorBottom = 0.70f,
            OffsetLeft = -150, OffsetRight = 150, OffsetTop = 0, OffsetBottom = 78,
        };
        _start.SetFont(UiTheme.Display);
        _start.Pressed += Leave;
        AddChild(_start);

        _checking = new Label
        {
            Text = "checking for updates…",
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.70f, AnchorBottom = 0.70f,
            OffsetTop = 88, HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1, 1, 1, 0.4f),
        };
        _checking.AddThemeFontSizeOverride("font_size", 13);
        AddChild(_checking);

        _fade = new ColorRect { Color = new Color(0.01f, 0.012f, 0.03f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_fade);

        CheckForUpdate();
        SetProcess(true);
    }

    private void CheckForUpdate()
    {
        var req = new HttpRequest { UseThreads = true, Timeout = 8 };
        AddChild(req);
        req.RequestCompleted += (result, code, headers, body) =>
        {
            _checking.Visible = false;
            if (code != 200) return;   // fail-open — a network blip must not lock the player out
            try
            {
                var json = Json.ParseString(body.GetStringFromUtf8());
                if (json.VariantType != Variant.Type.Dictionary) return;
                var d = json.AsGodotDictionary();
                string tag = d.TryGetValue("tag_name", out var t) ? t.AsString() : "";
                string url = d.TryGetValue("html_url", out var u) ? u.AsString() : $"https://github.com/{Repo}/releases";
                string name = d.TryGetValue("name", out var n) ? n.AsString() : tag;
                if (UpdateChecker.IsNewer(tag, UpdateChecker.Current()))
                {
                    string label = string.IsNullOrWhiteSpace(name) || name == tag ? tag : $"{name} ({tag})";
                    ShowGate(label, url);
                }
            }
            catch { /* fail-open */ }
        };
        string[] hdrs = { "User-Agent: Beyond-Game", "Accept: application/vnd.github+json" };
        if (req.Request($"https://api.github.com/repos/{Repo}/releases/latest", hdrs) != Error.Ok)
            _checking.Visible = false;
    }

    private void ShowGate(string available, string url)
    {
        _start.Visible = false;

        _gate = new Control { AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.5f, AnchorBottom = 0.5f };
        _gate.Theme = UiTheme.Instance;
        AddChild(_gate);

        var panel = new PanelContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, OffsetLeft = 24, OffsetRight = -24, OffsetTop = -150, OffsetBottom = 150,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.03f, 0.06f, 0.98f),
            BorderColor = new Color(0.95f, 0.5f, 0.35f),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 20, ContentMarginBottom = 20,
        });
        _gate.AddChild(panel);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);
        panel.AddChild(col);

        var h = new Label { Text = "UPDATE REQUIRED", HorizontalAlignment = HorizontalAlignment.Center };
        h.AddThemeFontOverride("font", UiTheme.Display);
        h.AddThemeFontSizeOverride("font_size", 24);
        h.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.4f));
        col.AddChild(h);

        var msg = new Label
        {
            Text = $"A new build is required to play.\n\ninstalled  v{UpdateChecker.Current()}\navailable  {available}",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 1, 1, 0.82f),
        };
        msg.AddThemeFontSizeOverride("font_size", 15);
        col.AddChild(msg);

        var dl = new Button { Text = "⬇   DOWNLOAD UPDATE", CustomMinimumSize = new Vector2(0, 64) };
        dl.AddThemeFontSizeOverride("font_size", 19);
        UiTheme.StylePrimary(dl);
        dl.Pressed += () => OS.ShellOpen(url);
        col.AddChild(dl);

        var hint = new Label
        {
            Text = "install the new APK, then reopen Beyond",
            HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.45f),
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        col.AddChild(hint);
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
