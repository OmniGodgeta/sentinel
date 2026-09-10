using Godot;
using Sentinel.Game;
using Sentinel.Config;

namespace Sentinel.UI;

/// <summary>
/// Shown after a mission whenever a Commander level was gained — pick one of
/// three upgrade cards. Loops until every owed pick is made, then returns.
/// </summary>
public sealed partial class LevelUpScreen : CanvasLayer
{
    public AppRoot App = null!;
    public System.Action? Done;

    private VBoxContainer _cards = null!;
    private Label _header = null!;

    public override void _Ready()
    {
        Layer = 7;
        AddChild(new MenuBackground { PlanetY = 0.24f });

        var wrap = new CenterContainer();
        wrap.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        wrap.Theme = UiTheme.Instance;
        AddChild(wrap);

        var root = new VBoxContainer { CustomMinimumSize = new Vector2(620, 0) };
        root.AddThemeConstantOverride("separation", 16);
        wrap.AddChild(root);

        var title = new Label { Text = "COMMANDER PROMOTION", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 33);
        title.AddThemeColorOverride("font_color", UiTheme.Accent);
        root.AddChild(title);

        _header = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.7f) };
        _header.AddThemeFontSizeOverride("font_size", 17);
        root.AddChild(_header);

        _cards = new VBoxContainer();
        _cards.AddThemeConstantOverride("separation", 10);
        root.AddChild(_cards);

        Rebuild();
    }

    private void Rebuild()
    {
        App.RefreshProgression();
        var p = App.Prog;
        if (p.PendingLevelUps <= 0) { Done?.Invoke(); return; }

        _header.Text = $"Commander {p.Commander}   ·   choose an upgrade   ({p.PendingLevelUps} left)";
        foreach (Node c in _cards.GetChildren()) c.QueueFree();

        var offer = p.LevelCardOffer();
        if (offer.Count == 0) { p.PickLevelCard("__skip__"); Rebuild(); return; }

        foreach (var card in offer)
        {
            var panel = new PanelContainer();
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 3);
            panel.AddChild(col);

            var cat = new Label { Text = card.Cat.ToUpperInvariant(), Modulate = CatColor(card.Cat) };
            cat.AddThemeFontSizeOverride("font_size", 14);
            col.AddChild(cat);
            var nm = new Label { Text = card.Name };
            nm.AddThemeFontSizeOverride("font_size", 21);
            col.AddChild(nm);
            var tx = new Label { Text = card.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.72f) };
            tx.AddThemeFontSizeOverride("font_size", 15);
            col.AddChild(tx);
            var btn = new Button { Text = "Take", CustomMinimumSize = new Vector2(0, 62) };
            btn.AddThemeFontSizeOverride("font_size", 22);
            string id = card.Id;
            btn.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Play("card_pick", -2f); App.Prog.PickLevelCard(id); Rebuild(); };
            col.AddChild(btn);

            _cards.AddChild(panel);
        }
    }

    private static Color CatColor(string cat) => cat switch
    {
        "Battery" => new Color(1f, 0.7f, 0.4f),
        "Sentinel" => new Color(0.6f, 1f, 0.8f),
        "Arsenal" => new Color(1f, 0.55f, 0.5f),
        "Protocol" => new Color(0.65f, 0.7f, 1f),
        _ => new Color(0.85f, 0.9f, 1f),
    };
}
