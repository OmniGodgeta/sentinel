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
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.Theme = UiTheme.Instance;
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(620, 0) };
        center.AddChild(panel);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);
        panel.AddChild(col);

        var h = new Label { Text = "UPDATE AVAILABLE", HorizontalAlignment = HorizontalAlignment.Center };
        h.AddThemeFontSizeOverride("font_size", 26);
        h.AddThemeColorOverride("font_color", UiTheme.Accent);
        col.AddChild(h);

        var v = new Label
        {
            Text = $"{title}   ({tag})\ninstalled: v{Current()}",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1, 1, 1, 0.8f),
        };
        v.AddThemeFontSizeOverride("font_size", 17);
        col.AddChild(v);

        if (!string.IsNullOrWhiteSpace(notes))
        {
            string trimmed = notes.Length > 320 ? notes[..320].TrimEnd() + "…" : notes;
            var nl = new Label { Text = trimmed, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.62f) };
            nl.AddThemeFontSizeOverride("font_size", 15);
            col.AddChild(nl);
        }

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 14);
        col.AddChild(row);

        var dl = new Button { Text = "Download", CustomMinimumSize = new Vector2(220, 62) };
        dl.AddThemeFontSizeOverride("font_size", 20);
        UiTheme.StylePrimary(dl);
        dl.Pressed += () => { OS.ShellOpen(url); QueueFree(); };
        row.AddChild(dl);

        var later = new Button { Text = "Later", CustomMinimumSize = new Vector2(160, 62) };
        later.AddThemeFontSizeOverride("font_size", 19);
        later.Pressed += QueueFree;
        row.AddChild(later);
    }
}
