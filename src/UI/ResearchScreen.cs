using Godot;
using Sentinel.Game;
using Sentinel.Meta;

namespace Sentinel.UI;

/// <summary>
/// The research tree, rendered from config as a branch-selector + tier list
/// (not a node graph). Permanent, account-wide; capstone choices swap freely.
/// </summary>
public sealed partial class ResearchScreen : CanvasLayer
{
    public AppRoot App = null!;

    private string _branch = "armaments";
    private Label _wallet = null!;
    private VBoxContainer _list = null!;
    private HBoxContainer _tabs = null!;

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.5f, PlanetScale = 0.55f, NebulaAlpha = 0.12f });

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -500, OffsetRight = 500, OffsetTop = 34, OffsetBottom = -12,
        };
        root.AddThemeConstantOverride("separation", 10);
        root.Theme = UiTheme.Instance;
        AddChild(MenuFrame.Around(root));
        AddChild(root);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(210, 84) };
        back.Pressed += () => App.ShowMenu();
        head.AddChild(back);
        var title = new Label { Text = "  RESEARCH", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 34);
        head.AddChild(title);

        _wallet = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _wallet.AddThemeFontSizeOverride("font_size", 21);
        root.AddChild(_wallet);

        var tabScroll = new ScrollContainer { VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 87) };
        root.AddChild(tabScroll);
        // Fill the scroll's width and centre the row, so the branch tabs sit under the
        // middle of the screen instead of hugging the left edge when they all fit.
        _tabs = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _tabs.AddThemeConstantOverride("separation", 8);
        tabScroll.AddChild(_tabs);
        foreach (var b in ResearchDb.Branches)
        {
            string bb = b;
            var t = new Button { Text = ResearchDb.BranchName(b).Split(' ')[0], ToggleMode = true, CustomMinimumSize = new Vector2(168, 78) };
            t.AddThemeFontSizeOverride("font_size", 21);
            t.Pressed += () => { _branch = bb; Rebuild(); };
            _tabs.AddChild(t);
        }

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        root.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);

        Rebuild();
    }

    private void Rebuild()
    {
        App.RefreshProgression();
        var p = App.Prog;
        var s = App.Save;
        _wallet.Text = $"Commander {p.Commander}   ·   RD {Mathf.FloorToInt((float)s.ResearchData)}   ·   Alloy {Mathf.FloorToInt((float)s.ExoticAlloy)}";

        int ti = 0;
        foreach (Node c in _tabs.GetChildren())
            if (c is Button b) { b.ButtonPressed = ResearchDb.Branches[ti] == _branch; ti++; }

        foreach (Node c in _list.GetChildren()) c.QueueFree();

        if (!p.BranchOpen(_branch))
        {
            _list.AddChild(Note($"{ResearchDb.BranchName(_branch)} unlocks at Commander {BranchGate(_branch)}."));
            return;
        }

        for (int tier = 1; tier <= 6; tier++)
        {
            bool tierOpen = p.TierOpen(_branch, tier);
            var th = new Label { Text = tierOpen ? $"— TIER {tier} —" : $"— TIER {tier} (locked · Cmdr {Progression.TierCommanderGate(tier)}, 3 in tier {tier - 1}) —" };
            th.AddThemeFontSizeOverride("font_size", 22);
            th.Modulate = tierOpen ? new Color(1, 1, 1, 0.7f) : new Color(1, 1, 1, 0.3f);
            _list.AddChild(th);

            foreach (var node in App.Research.NodesIn(_branch, tier))
                _list.AddChild(NodeRow(node, p));
        }
    }

    private Control NodeRow(ResearchNode node, Progression p)
    {
        var s = App.Save;
        var row = new VBoxContainer();
        row.AddThemeConstantOverride("separation", 2);

        int ranks = p.Ranks(node.Id);
        bool cap = node.IsCapstone;
        bool capActive = cap && s.Capstones.TryGetValue(node.Branch, out var cc) && cc == node.Id;
        (double rd, double alloy) = p.NodeCost(node);

        var name = new Label
        {
            Text = cap ? $"◆ {node.Name}" + (capActive ? "   ✓ active" : "")
                       : $"{node.Name}   [{ranks}/{node.RankCount}]",
        };
        name.AddThemeFontSizeOverride("font_size", 21);
        row.AddChild(name);

        var desc = new Label { Text = node.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.6f) };
        desc.AddThemeFontSizeOverride("font_size", 21);
        row.AddChild(desc);

        var actionRow = new HBoxContainer();
        row.AddChild(actionRow);
        string costStr = alloy > 0 ? $"{rd:0} RD + {alloy:0} Alloy" : $"{rd:0} RD";
        bool can = p.CanBuy(node, out string reason);
        var buy = new Button
        {
            Text = cap ? (capActive ? "active" : $"choose  ({costStr})")
                       : ranks >= node.RankCount ? "maxed" : $"buy rank {ranks + 1}  ({costStr})",
            Disabled = !can,
            CustomMinimumSize = new Vector2(0, 78),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        buy.AddThemeFontSizeOverride("font_size", 21);
        buy.Pressed += () => { if (p.Buy(node)) Rebuild(); };
        actionRow.AddChild(buy);

        if (!can && reason is not ("maxed" or "active"))
        {
            var r = new Label { Text = "  " + reason, Modulate = new Color(1f, 0.6f, 0.5f), VerticalAlignment = VerticalAlignment.Center };
            r.AddThemeFontSizeOverride("font_size", 21);
            actionRow.AddChild(r);
        }
        // Accent + a faint branch emblem behind the row — these used to be unstyled
        // panels, which read as flat grey text slabs.
        return ArtCard.Wrap(row, BranchAccent(node.Branch), BranchEmblem(node.Branch),
                            dim: ranks >= node.RankCount && !cap);
    }

    /// <summary>Each research branch's colour and the PDTD emblem used as its watermark.</summary>
    private static Color BranchAccent(string branch) => branch switch
    {
        "armaments" => new Color(0.95f, 0.55f, 0.35f),
        "fortification" => new Color(0.42f, 0.78f, 0.95f),
        "fleet" => new Color(0.55f, 0.85f, 0.55f),
        "sentinel" => new Color(0.75f, 0.55f, 0.98f),
        _ => new Color(0.98f, 0.82f, 0.40f),
    };

    private static string BranchEmblem(string branch) => branch switch
    {
        "armaments" => "techpoint/missile",
        "fortification" => "techpoint/gravitynova",
        "fleet" => "techpoint/beam",
        "sentinel" => "techpoint/radiationlink",
        _ => "techpoint/random",
    };

    private static Label Note(string t)
    {
        var l = new Label { Text = t, HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.6f) };
        l.AddThemeFontSizeOverride("font_size", 21);
        return l;
    }

    private static int BranchGate(string b) => b switch
    { "fortification" => 5, "fleet" => 10, "sentinel" => 15, _ => 1 };
}
