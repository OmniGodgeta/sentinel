using System.Collections.Generic;
using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// The Codex — in-world entries for the setting, the enemies, and (later) turrets
/// and abilities. Cheap content, high recall value for a sci-fi audience (spec §14).
/// TODO: gate entries behind first-encounter once encounter tracking lands.
/// </summary>
public sealed partial class CodexScreen : CanvasLayer
{
    public AppRoot App = null!;

    public override void _Ready()
    {
        Layer = 6;
        var bg = new ColorRect { Color = new Color(0.03f, 0.03f, 0.05f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(bg);

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -240, OffsetRight = 240, OffsetTop = 14, OffsetBottom = -12,
        };
        root.AddThemeConstantOverride("separation", 8);
        root.Theme = UiTheme.Instance;
        AddChild(root);

        var head = new HBoxContainer();
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(90, 34) };
        back.Pressed += () => App.ShowMenu();
        head.AddChild(back);
        var title = new Label { Text = "  CODEX" };
        title.AddThemeFontSizeOverride("font_size", 20);
        head.AddChild(title);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        root.AddChild(scroll);
        var list = new VBoxContainer { CustomMinimumSize = new Vector2(470, 0) };
        list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(list);

        string cat = "";
        foreach (var e in Sorted(App.Cfg.AllCodex))
        {
            if (e.Category != cat)
            {
                cat = e.Category;
                var h = new Label { Text = "— " + cat.ToUpperInvariant() + " —", Modulate = new Color(1, 1, 1, 0.6f) };
                h.AddThemeFontSizeOverride("font_size", 11);
                list.AddChild(h);
            }
            var panel = new PanelContainer();
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 2);
            panel.AddChild(col);
            var nm = new Label { Text = e.Title };
            nm.AddThemeFontSizeOverride("font_size", 13);
            col.AddChild(nm);
            var tx = new Label { Text = e.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.75f) };
            tx.AddThemeFontSizeOverride("font_size", 11);
            col.AddChild(tx);
            list.AddChild(panel);
        }
    }

    private static List<Config.CodexEntry> Sorted(IReadOnlyList<Config.CodexEntry> src)
    {
        var order = new Dictionary<string, int> { ["world"] = 0, ["enemy"] = 1, ["turret"] = 2, ["ability"] = 3 };
        var l = new List<Config.CodexEntry>(src);
        l.Sort((a, b) =>
        {
            int ca = order.GetValueOrDefault(a.Category, 9), cb = order.GetValueOrDefault(b.Category, 9);
            return ca != cb ? ca.CompareTo(cb) : string.CompareOrdinal(a.Title, b.Title);
        });
        return l;
    }
}
