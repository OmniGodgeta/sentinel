using Godot;
using Sentinel.Game;
using Sentinel.Config;

namespace Sentinel.UI;

/// <summary>
/// The Shop (design-spec §10). Cosmetics only — hero hulls, planet skins,
/// ordnance colours — bought with Commendations earned by playing. No combat
/// effect, no randomness, no rotating stock: the whole catalogue is visible
/// from day one with its price and, if locked, why.
/// </summary>
public sealed partial class ShopScreen : CanvasLayer
{
    public AppRoot App = null!;

    private Label _wallet = null!;
    private VBoxContainer _list = null!;
    private HBoxContainer _tabs = null!;
    private string _tab = "fleet";

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.16f, NebulaAlpha = 0.10f });

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -300, OffsetRight = 300, OffsetTop = 16, OffsetBottom = -12,
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
        var title = new Label { Text = "  SHOP", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        head.AddChild(title);

        _wallet = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _wallet.AddThemeFontSizeOverride("font_size", 16);
        _wallet.AddThemeColorOverride("font_color", UiTheme.Accent2);
        root.AddChild(_wallet);

        _tabs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _tabs.AddThemeConstantOverride("separation", 8);
        root.AddChild(_tabs);
        foreach (var t in Sentinel.Meta.Shop.Tabs)
        {
            string tt = t;
            var b = new Button
            {
                Text = Sentinel.Meta.Shop.TabTitle(t), ToggleMode = true,
                CustomMinimumSize = new Vector2(184, 54),
            };
            b.AddThemeFontSizeOverride("font_size", 15);
            b.Pressed += () => { _tab = tt; Rebuild(); };
            _tabs.AddChild(b);
        }

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        root.AddChild(scroll);
        _list = new VBoxContainer { CustomMinimumSize = new Vector2(580, 0) };
        _list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_list);

        Rebuild();
    }

    private void Rebuild()
    {
        App.RefreshProgression();
        var shop = App.Shop;
        _wallet.Text = $"✦ Commendations {shop.Balance}     ·     earned by clearing stars, Ascension, the weekly & the Codex";

        int i = 0;
        foreach (Node c in _tabs.GetChildren())
            if (c is Button b) b.ButtonPressed = Sentinel.Meta.Shop.Tabs[i++] == _tab;

        foreach (Node c in _list.GetChildren()) c.QueueFree();

        foreach (var it in shop.ItemsIn(_tab))
        {
            bool owned = shop.Owned(it.Id);
            bool equipped = shop.Equipped(it);

            var panel = new PanelContainer();
            if (equipped)
            {
                var sb = new StyleBoxFlat
                {
                    BgColor = new Color(UiTheme.Accent, 0.10f),
                    BorderColor = UiTheme.Accent,
                    BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
                    CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
                    CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
                    ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12,
                };
                panel.AddThemeStyleboxOverride("panel", sb);
            }
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 4);
            panel.AddChild(col);

            var trow = new HBoxContainer();
            col.AddChild(trow);
            var nm = new Label { Text = it.Name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
            nm.AddThemeFontSizeOverride("font_size", 19);
            trow.AddChild(nm);

            var tag = new Label { VerticalAlignment = VerticalAlignment.Center };
            tag.AddThemeFontSizeOverride("font_size", 15);
            if (equipped) { tag.Text = "EQUIPPED"; tag.AddThemeColorOverride("font_color", UiTheme.Accent); }
            else if (owned) { tag.Text = "owned"; tag.Modulate = new Color(1, 1, 1, 0.6f); }
            else { tag.Text = $"✦ {it.Cost}"; tag.AddThemeColorOverride("font_color", UiTheme.Accent2); }
            trow.AddChild(tag);

            var desc = new Label { Text = it.Desc, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.66f) };
            desc.AddThemeFontSizeOverride("font_size", 14);
            col.AddChild(desc);

            var act = new Button { CustomMinimumSize = new Vector2(0, 54), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            act.AddThemeFontSizeOverride("font_size", 17);
            var itc = it;
            if (equipped) { act.Text = "Equipped"; act.Disabled = true; }
            else if (owned) { act.Text = "Equip"; UiTheme.StylePrimary(act); act.Pressed += () => { App.Shop.Equip(itc); Sentinel.Audio.AudioManager.Instance?.Click(); Rebuild(); }; }
            else if (shop.CanBuy(it)) { act.Text = $"Requisition  ·  ✦ {it.Cost}"; UiTheme.StylePrimary(act); act.Pressed += () => { if (App.Shop.Buy(itc)) { Sentinel.Audio.AudioManager.Instance?.Confirm(); Rebuild(); } }; }
            else { act.Text = $"✦ {it.Cost} — need {it.Cost - shop.Balance} more"; act.Disabled = true; }
            col.AddChild(act);

            _list.AddChild(panel);
        }

        var note = new Label
        {
            Text = "Everything here is cosmetic. Nothing in the Shop affects combat, and every mission and Ascension tier can be cleared without opening it.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 1, 1, 0.4f),
        };
        note.AddThemeFontSizeOverride("font_size", 13);
        _list.AddChild(note);
    }
}
