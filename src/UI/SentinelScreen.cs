using Godot;
using Sentinel.Game;
using Sentinel.Sim;

namespace Sentinel.UI;

/// <summary>
/// SENTINELS — permanent upgrades for the planet's six orbital weapons. Each level
/// costs Commendations (coins) + Sentinel Cores (the exp-equivalent). The level
/// bought here is where the weapon starts every battle; the in-fight cards take it
/// further. Planet Shield lives here too — upgraded only outside battle.
/// </summary>
public sealed partial class SentinelScreen : CanvasLayer
{
    public AppRoot App = null!;

    private Label _wallet = null!;
    private VBoxContainer _list = null!;

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.5f, PlanetScale = 0.55f, NebulaAlpha = 0.10f });

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -320, OffsetRight = 320, OffsetTop = 34, OffsetBottom = -12,
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
        var title = new Label { Text = "  SENTINELS", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontOverride("font", UiTheme.Display);
        title.AddThemeFontSizeOverride("font_size", 24);
        head.AddChild(title);

        _wallet = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _wallet.AddThemeFontSizeOverride("font_size", 15);
        _wallet.AddThemeColorOverride("font_color", UiTheme.Accent2);
        root.AddChild(_wallet);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_list);

        Rebuild();
    }

    private static (int comm, int cores) Cost(int nextLevel) => (26 * nextLevel + 14, 1 + nextLevel / 2);
    private static int ShieldCost(int nextLevel) => 60 * nextLevel;
    private static int ShieldAlloyCost(int nextLevel) => nextLevel <= 4 ? 0 : (nextLevel - 4) * 3;

    private void Rebuild()
    {
        var s = App.Save;
        _wallet.Text = $"✦ {App.Shop.Balance}  Commendations      ✷ {s.SentinelCores}  Cores      ❖ {F(s.ExoticAlloy)}  Alloy";
        foreach (Node c in _list.GetChildren()) c.QueueFree();

        // ---- Planet Shield ----
        {
            int lvl = s.PlanetShieldLevel;
            int max = 12;
            var card = MakeCard(new Color(0.32f, 0.72f, 1f), "PLANET SHIELD", "planet_shield",
                $"Orbital barrier — absorbs {Mathf.RoundToInt(SimWorld.PlanetShieldStrength(lvl))} damage before the planet is touched. Recharges slowly in battle.",
                lvl, max);
            var row = card.GetNode<HBoxContainer>("row");
            if (lvl < max)
            {
                int cc = ShieldCost(lvl + 1); int ac = ShieldAlloyCost(lvl + 1);
                var buy = new Button
                {
                    Text = ac > 0 ? $"Upgrade\n✦{cc}  ❖{ac}" : $"Upgrade\n✦ {cc}",
                    CustomMinimumSize = new Vector2(150, 62),
                };
                buy.AddThemeFontSizeOverride("font_size", 14);
                buy.Disabled = App.Shop.Balance < cc || s.ExoticAlloy < ac;
                buy.Pressed += () =>
                {
                    s.CommendationsSpent += cc;
                    s.ExoticAlloy -= ac;
                    s.PlanetShieldLevel++;
                    s.Save();
                    Click(); Rebuild();
                };
                row.AddChild(buy);
            }
            else row.AddChild(new Label { Text = "MAX", VerticalAlignment = VerticalAlignment.Center });
            _list.AddChild(card);
        }

        // ---- the six orbital weapons ----
        foreach (var w in App.Cfg.OrbitalWeapons)
        {
            int lvl = s.OrbitalMeta.TryGetValue(w.Id, out int v) ? v : 0;
            int cap = w.MaxLevel;
            var card = MakeCard(HexColor(w.Accent), w.Name.ToUpperInvariant(), w.Id, w.Text, lvl, cap);
            var row = card.GetNode<HBoxContainer>("row");
            if (lvl < cap)
            {
                var (cc, xc) = Cost(lvl + 1);
                var buy = new Button { Text = $"Upgrade\n✦ {cc}   ✷ {xc}", CustomMinimumSize = new Vector2(150, 62) };
                buy.AddThemeFontSizeOverride("font_size", 14);
                buy.Disabled = App.Shop.Balance < cc || s.SentinelCores < xc;
                string id = w.Id;
                buy.Pressed += () =>
                {
                    s.CommendationsSpent += cc;
                    s.SentinelCores -= xc;
                    s.OrbitalMeta[id] = lvl + 1;
                    s.Save();
                    Click(); Rebuild();
                };
                row.AddChild(buy);
            }
            else row.AddChild(new Label { Text = "MAX", VerticalAlignment = VerticalAlignment.Center });
            _list.AddChild(card);
        }
    }

    private PanelContainer MakeCard(Color accent, string name, string imgId, string desc, int lvl, int max)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, 0.08f),
            BorderColor = new Color(accent, 0.6f),
            BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 10, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10,
        });
        var row = new HBoxContainer { Name = "row" };
        row.AddThemeConstantOverride("separation", 12);
        p.AddChild(row);

        var art = new TextureRect
        {
            Texture = CardTexture(imgId),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            CustomMinimumSize = new Vector2(64, 64),
            ClipContents = true,
        };
        var artFrame = new PanelContainer { CustomMinimumSize = new Vector2(64, 64) };
        artFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.3f), BorderColor = new Color(accent, 0.7f),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 0, ContentMarginRight = 0, ContentMarginTop = 0, ContentMarginBottom = 0,
        });
        artFrame.AddChild(art);
        row.AddChild(artFrame);

        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 3);
        row.AddChild(col);
        var nm = new Label { Text = $"{name}    ·    LV {lvl}/{max}" };
        nm.AddThemeFontOverride("font", UiTheme.Display);
        nm.AddThemeFontSizeOverride("font_size", 16);
        nm.AddThemeColorOverride("font_color", accent.Lightened(0.3f));
        col.AddChild(nm);
        var tx = new Label { Text = desc, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.7f) };
        tx.AddThemeFontSizeOverride("font_size", 12);
        col.AddChild(tx);
        return p;
    }

    private readonly System.Collections.Generic.Dictionary<string, Texture2D> _cardTex = new();
    private Texture2D? CardTexture(string id)
    {
        if (_cardTex.TryGetValue(id, out var t)) return t;
        t = GD.Load<Texture2D>($"res://assets/game/cards/{id}.jpg");
        _cardTex[id] = t;
        return t;
    }

    private static Color HexColor(string hex) { try { return new Color(hex); } catch { return UiTheme.Accent; } }
    private static void Click() => Sentinel.Audio.AudioManager.Instance?.Click();
    private static string F(double v) => Mathf.FloorToInt((float)v).ToString();
}
