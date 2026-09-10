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

    public override void _Ready()
    {
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

            if (!IsNewer(tag, Current())) { QueueFree(); return; }
            ShowCard(string.IsNullOrWhiteSpace(name) ? tag : name, tag, notes, url);
        }
        catch { QueueFree(); }
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

    private void ShowCard(string title, string tag, string notes, string url)
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
        dl.Pressed += () => { OS.ShellOpen(url); QueueFree(); };
        row.AddChild(dl);
    }
}
