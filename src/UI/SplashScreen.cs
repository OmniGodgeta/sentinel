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
        _checking.AddThemeFontSizeOverride("font_size", 18);
        AddChild(_checking);

        // Always-visible build number. Without it there's no way to tell from the device
        // whether a fix actually shipped or the old APK is still installed, which made
        // chasing the "Download does nothing" report much harder than it needed to be.
        var ver = new Label
        {
            Text = $"v{UpdateChecker.Current()}",
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetTop = -40, OffsetBottom = -12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1, 1, 1, 0.35f),
        };
        ver.AddThemeFontSizeOverride("font_size", 16);
        AddChild(ver);

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
                    // The apk asset URL matters: without it PromptInstall's card has nothing
                    // to download and silently bounces to a browser instead (v0.30.2 fix).
                    UpdateChecker.MarkAvailable(tag, url, UpdateChecker.ApkAssetUrl(d));
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
        h.AddThemeFontSizeOverride("font_size", 34);
        h.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.4f));
        col.AddChild(h);

        var msg = new Label
        {
            Text = $"A new build is required to play.\n\ninstalled  v{UpdateChecker.Current()}\navailable  {available}",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 1, 1, 0.82f),
        };
        msg.AddThemeFontSizeOverride("font_size", 21);
        col.AddChild(msg);

        // Progress + status live INSIDE the gate panel. The previous two attempts routed
        // this button through UpdateChecker.PromptInstall, which builds a second
        // CanvasLayer over the top — and any way that can go wrong (layer ordering, the
        // node not being added, an exception before the card renders) produces exactly
        // one symptom: the button does nothing at all, with nothing on screen to say why.
        // Downloading in place removes that whole class of failure, and every branch below
        // writes to `status`, so the button can never again be silently inert.
        var progress = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, Value = 0, ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 10), Visible = false,
        };
        col.AddChild(progress);

        var status = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false,
        };
        status.AddThemeFontSizeOverride("font_size", 18);
        col.AddChild(status);

        var dl = new Button { Text = "⬇   DOWNLOAD UPDATE", CustomMinimumSize = new Vector2(0, 90) };
        dl.AddThemeFontSizeOverride("font_size", 27);
        UiTheme.StylePrimary(dl);
        col.AddChild(dl);

        var hint = new Label
        {
            Text = $"installed build v{UpdateChecker.Current()} — install the new APK, then reopen Beyond",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 1, 1, 0.45f),
        };
        hint.AddThemeFontSizeOverride("font_size", 17);
        col.AddChild(hint);

        bool busy = false;
        dl.Pressed += () =>
        {
            if (busy) return;
            busy = true;
            status.Visible = true;
            status.Modulate = new Color(1, 1, 1, 0.8f);
            Sentinel.Audio.AudioManager.Instance?.Click();

            string apk = UpdateChecker.AvailableApkUrl;
            if (string.IsNullOrEmpty(apk))
            {
                // No asset URL (release still building, or the API response had no .apk).
                // Say so and hand them the page rather than appearing to do nothing.
                status.Text = "no APK on that release yet — opening the release page";
                OS.ShellOpen(url);
                busy = false;
                return;
            }

            dl.Disabled = true;
            progress.Visible = true;
            status.Text = OS.GetName() == "Android"
                ? "downloading…"
                : "downloading… (desktop build — saves the file, no installer)";

            UpdateDownloader.Start(this, apk,
                onProgress: f =>
                {
                    progress.Value = f;
                    status.Text = $"downloading… {f * 100f:0}%";
                },
                onDone: (ok, info) =>
                {
                    if (ok)
                    {
                        status.Text = "handing it to the installer — the game will close";
                        var tree = GetTree();
                        if (OS.GetName() != "Android") OS.SetRestartOnExit(true);
                        tree.CreateTimer(1.5).Timeout += () => tree.Quit();
                        return;
                    }
                    status.Text = $"download failed: {info}\ntap again to open the release page in your browser";
                    status.Modulate = new Color(1f, 0.62f, 0.55f);
                    dl.Disabled = false;
                    dl.Text = "OPEN RELEASE PAGE";
                    busy = false;
                    // next press goes to the browser
                    UpdateChecker.ClearApkUrl();
                });
        };
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
        l.AddThemeFontSizeOverride("font_size", 106);
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
