using Godot;
using Sentinel.UI;

namespace Sentinel.Meta;

/// <summary>
/// On the first visit to the menu each launch, asks the public GitHub releases API
/// whether a newer tag exists and, if so, drops a dismissible "update available"
/// card over the menu with a Download button (opens the release page in a browser).
/// Silent on any failure — offline, rate-limited, no releases yet.
/// </summary>
public sealed partial class UpdateChecker : CanvasLayer
{
    private const string Repo = "OmniGodgeta/sentinel";
    private const string Api = "https://api.github.com/repos/" + Repo + "/releases/latest";

    private static bool _checkedThisLaunch;

    /// <summary>Set by whichever check (splash gate or this menu card) resolves first each
    /// launch — lets any other screen, including the in-mission HUD, know a build is
    /// waiting without re-hitting the GitHub API.</summary>
    public static bool Available { get; private set; }
    public static string AvailableTag { get; private set; } = "";
    public static string AvailableUrl { get; private set; } = "";
    public static string AvailableApkUrl { get; private set; } = "";
    public static void MarkAvailable(string tag, string url, string apkUrl = "")
    {
        Available = true; AvailableTag = tag; AvailableUrl = url; AvailableApkUrl = apkUrl;
    }

    public override void _Ready()
    {
        if (HasMeta("prompt_only")) return;   // opened by PromptInstall, card is built by hand
        Layer = 20;
        if (_checkedThisLaunch) { QueueFree(); return; }
        _checkedThisLaunch = true;

        var req = new HttpRequest { UseThreads = true, Timeout = 8 };
        AddChild(req);
        req.RequestCompleted += OnCompleted;
        string[] headers = { "User-Agent: Beyond-Game", "Accept: application/vnd.github+json" };
        if (req.Request(Api, headers) != Error.Ok) QueueFree();
    }

    private void OnCompleted(long result, long code, string[] headers, byte[] body)
    {
        if (code != 200) { QueueFree(); return; }
        try
        {
            var json = Json.ParseString(body.GetStringFromUtf8());
            if (json.VariantType != Variant.Type.Dictionary) { QueueFree(); return; }
            var d = json.AsGodotDictionary();
            string Get(string k, string fallback) => d.TryGetValue(k, out var val) ? val.AsString() : fallback;
            string tag = Get("tag_name", "");
            string url = Get("html_url", "https://github.com/" + Repo + "/releases");
            string notes = Get("body", "");
            string name = Get("name", tag);

            string apkUrl = ApkAssetUrl(d);

            if (!IsNewer(tag, Current())) { QueueFree(); return; }
            MarkAvailable(tag, url, apkUrl);
            ShowCard(string.IsNullOrWhiteSpace(name) ? tag : name, tag, notes, url, apkUrl);
        }
        catch { QueueFree(); }
    }

    /// <summary>Pull the release's APK asset's direct-download URL out of a parsed
    /// /releases/latest payload, so Download can fetch it in-app instead of only opening
    /// the release page in a browser. Shared with <see cref="UI.SplashScreen"/>, which
    /// used to skip this entirely and hand <see cref="MarkAvailable"/> an empty apk URL —
    /// the gate's Download button then always fell through to the browser (v0.30.2 fix).</summary>
    public static string ApkAssetUrl(Godot.Collections.Dictionary d)
    {
        if (!d.TryGetValue("assets", out var assetsVal) || assetsVal.VariantType != Variant.Type.Array)
            return "";
        foreach (var av in assetsVal.AsGodotArray())
        {
            if (av.VariantType != Variant.Type.Dictionary) continue;
            var ad = av.AsGodotDictionary();
            string an = ad.TryGetValue("name", out var anv) ? anv.AsString() : "";
            if (an.EndsWith(".apk"))
                return ad.TryGetValue("browser_download_url", out var auv) ? auv.AsString() : "";
        }
        return "";
    }

    public static string Current()
        => ((string)ProjectSettings.GetSetting("application/config/version", "0.0.0")).Trim();

    /// <summary>true if <paramref name="tag"/> (e.g. "v0.9.1") is a higher semver than <paramref name="cur"/>.</summary>
    public static bool IsNewer(string tag, string cur)
    {
        var a = Parse(tag);
        var b = Parse(cur);
        for (int i = 0; i < 3; i++)
        {
            if (a[i] != b[i]) return a[i] > b[i];
        }
        return false;
    }

    private static int[] Parse(string v)
    {
        v = v.TrimStart('v', 'V').Trim();
        var parts = v.Split('.', '-', '+');
        var o = new int[3];
        for (int i = 0; i < 3 && i < parts.Length; i++) int.TryParse(parts[i], out o[i]);
        return o;
    }

    /// <summary>Open the download/install card over whatever screen is up, using the
    /// release this launch already found. Used by the HUD's update badge and the splash
    /// button so they run the real in-app download+install instead of kicking the player
    /// out to a browser.</summary>
    public static void PromptInstall(Node parent)
    {
        if (!Available) return;
        // Above EVERYTHING. This used to sit at 24, which is under SplashScreen's
        // layer 30 — so the mandatory-update gate's own Download button spawned a card
        // nobody could see or tap, and the updater looked completely dead (v0.30.2 fix).
        var layer = new UpdateChecker { Layer = 200 };
        layer.SetMeta("prompt_only", true);
        parent.GetTree().Root.AddChild(layer);
        layer.ShowCard(AvailableTag, AvailableTag, "", AvailableUrl, AvailableApkUrl);
    }

    private void ShowCard(string title, string tag, string notes, string url, string apkUrl)
    {
        // a slim, non-blocking card pinned near the top of the menu
        var wrap = new MarginContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0f, AnchorBottom = 0f,
            OffsetTop = 14,
        };
        wrap.AddThemeConstantOverride("margin_left", 14);
        wrap.AddThemeConstantOverride("margin_right", 14);
        wrap.Theme = UiTheme.Instance;
        AddChild(wrap);

        var panel = new PanelContainer();
        var psb = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.06f, 0.11f, 0.97f),
            BorderColor = UiTheme.Accent,
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12,
        };
        panel.AddThemeStyleboxOverride("panel", psb);
        wrap.AddChild(panel);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        panel.AddChild(col);

        var h = new Label { Text = $"⬆  UPDATE AVAILABLE   —   {title} ({tag})" };
        h.AddThemeFontSizeOverride("font_size", 18);
        h.AddThemeColorOverride("font_color", UiTheme.Accent);
        col.AddChild(h);

        var v = new Label { Text = $"installed v{Current()}. New build ready on GitHub Releases." };
        v.AddThemeFontSizeOverride("font_size", 14);
        v.Modulate = new Color(1, 1, 1, 0.72f);
        col.AddChild(v);

        if (!string.IsNullOrWhiteSpace(notes))
        {
            string trimmed = notes.Length > 200 ? notes[..200].TrimEnd() + "…" : notes;
            var nl = new Label { Text = trimmed, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.55f) };
            nl.AddThemeFontSizeOverride("font_size", 13);
            col.AddChild(nl);
        }

        var progress = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, Value = 0, ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 8), Visible = false,
        };
        col.AddChild(progress);

        var status = new Label { Visible = false, HorizontalAlignment = HorizontalAlignment.Center };
        status.AddThemeFontSizeOverride("font_size", 13);
        status.Modulate = new Color(1, 1, 1, 0.7f);
        col.AddChild(status);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        row.AddThemeConstantOverride("separation", 10);
        col.AddChild(row);

        var later = new Button { Text = "Later", CustomMinimumSize = new Vector2(120, 50) };
        later.AddThemeFontSizeOverride("font_size", 16);
        later.Pressed += QueueFree;
        row.AddChild(later);

        var dl = new Button { Text = "Download", CustomMinimumSize = new Vector2(180, 50) };
        dl.AddThemeFontSizeOverride("font_size", 17);
        UiTheme.StylePrimary(dl);
        bool downloadFailed = false;
        dl.Pressed += () =>
        {
            if (string.IsNullOrEmpty(apkUrl) || downloadFailed)
            {
                // No in-app path available. Say so on the card instead of closing it
                // instantly — ShellOpen can no-op on Android, and a card that just
                // vanishes reads as "the button does nothing".
                OS.ShellOpen(url);
                status.Visible = true;
                status.Text = "opening the release page in your browser…";
                return;
            }
            dl.Disabled = true; later.Disabled = true;
            progress.Visible = true; status.Visible = true;
            status.Text = OS.GetName() == "Android" ? "downloading…" : "downloading… (desktop build — no installer, just saves the file)";
            UpdateDownloader.Start(this, apkUrl,
                onProgress: f => progress.Value = f,
                onDone: (ok, info) =>
                {
                    if (ok)
                    {
                        // Android: the system installer has the APK now and will replace us.
                        // Quitting here means the player lands back in the NEW build instead
                        // of a stale running copy. Desktop: relaunch ourselves.
                        status.Text = "installing — the game will restart";
                        var tree = GetTree();
                        if (OS.GetName() != "Android") OS.SetRestartOnExit(true);
                        tree.CreateTimer(1.5).Timeout += () => tree.Quit();
                        return;
                    }
                    downloadFailed = true;
                    status.Text = $"download failed ({info}) — tap to open the release page instead";
                    status.Modulate = new Color(1f, 0.6f, 0.55f);
                    dl.Disabled = false; later.Disabled = false;
                    dl.Text = "Open in Browser";
                });
        };
        row.AddChild(dl);
    }
}
