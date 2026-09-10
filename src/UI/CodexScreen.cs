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
            OffsetLeft = -270, OffsetRight = 270, OffsetTop = 16, OffsetBottom = -12,
        };
        root.AddThemeConstantOverride("separation", 10);
        root.Theme = UiTheme.Instance;
        AddChild(root);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(150, 60) };
        back.Pressed += () => App.ShowMenu();
        head.AddChild(back);
        var title = new Label { Text = "  CODEX", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        head.AddChild(title);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        root.AddChild(scroll);
        var list = new VBoxContainer { CustomMinimumSize = new Vector2(530, 0) };
        list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(list);

        var seen = new HashSet<string>(App.Save.CodexSeen);
        var all = Sorted(App.Cfg.AllCodex);

        string cat = "";
        int hiddenInCat = 0;
        void FlushHidden()
        {
            if (hiddenInCat == 0) return;
            var s = new Label { Text = $"   {hiddenInCat} more — undiscovered", Modulate = new Color(1, 1, 1, 0.35f) };
            s.AddThemeFontSizeOverride("font_size", 15);
            list.AddChild(s);
            hiddenInCat = 0;
        }

        foreach (var e in all)
        {
            if (e.Category != cat)
            {
                FlushHidden();
                cat = e.Category;
                var h = new Label { Text = "— " + cat.ToUpperInvariant() + " —", Modulate = new Color(1, 1, 1, 0.6f) };
                h.AddThemeFontSizeOverride("font_size", 16);
                list.AddChild(h);
            }

            if (!seen.Contains(e.Id)) { hiddenInCat++; continue; }

            var panel = new PanelContainer();
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 2);
            panel.AddChild(col);
            var nm = new Label { Text = e.Title };
            nm.AddThemeFontSizeOverride("font_size", 16);
            col.AddChild(nm);
            var tx = new Label { Text = e.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.75f) };
            tx.AddThemeFontSizeOverride("font_size", 16);
            col.AddChild(tx);
            list.AddChild(panel);
        }
        FlushHidden();

        int total = all.Count, found = 0;
        foreach (var e in all) if (seen.Contains(e.Id)) found++;
        title.Text = $"  CODEX   ·   {found}/{total}";
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
