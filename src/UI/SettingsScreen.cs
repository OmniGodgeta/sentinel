using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>Sound, visuals, and the planet skin.</summary>
public sealed partial class SettingsScreen : CanvasLayer
{
    public AppRoot App = null!;

    private static readonly (string id, string name)[] Skins =
    {
        ("earth", "Earth"), ("mars", "Mars"), ("ice", "Ice World"),
        ("volcanic", "Volcanic"), ("gas", "Gas Giant"), ("shattered", "Shattered"),
    };

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.22f, NebulaAlpha = 0.4f });

        var wrap = new CenterContainer();
        wrap.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        wrap.Theme = UiTheme.Instance;
        AddChild(wrap);

        var root = new VBoxContainer { CustomMinimumSize = new Vector2(440, 0) };
        root.AddThemeConstantOverride("separation", 14);
        wrap.AddChild(root);

        var head = new HBoxContainer();
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(90, 34) };
        back.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Back(); App.ShowMenu(); };
        head.AddChild(back);
        var title = new Label { Text = "  SETTINGS" };
        title.AddThemeFontSizeOverride("font_size", 20);
        head.AddChild(title);

        var o = App.Save.Options;

        // sfx volume
        root.AddChild(Lbl("Sound effects"));
        var vol = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = o.SfxVolume, CustomMinimumSize = new Vector2(0, 28) };
        vol.ValueChanged += v =>
        {
            o.SfxVolume = (float)v; App.Save.Save();
            Sentinel.Audio.AudioManager.Instance?.SetVolume(o.SfxVolume, o.Muted);
        };
        vol.DragEnded += _ => Sentinel.Audio.AudioManager.Instance?.Play("ui_confirm", -4f);
        root.AddChild(vol);

        var mute = new CheckButton { Text = "Mute all sound", ButtonPressed = o.Muted };
        mute.Toggled += b =>
        {
            o.Muted = b; App.Save.Save();
            Sentinel.Audio.AudioManager.Instance?.SetVolume(o.SfxVolume, o.Muted);
        };
        root.AddChild(mute);

        var flash = new CheckButton { Text = "Reduce screen flashes", ButtonPressed = o.ReduceFlash };
        flash.Toggled += b => { o.ReduceFlash = b; App.Save.Save(); };
        root.AddChild(flash);

        root.AddChild(new HSeparator());
        root.AddChild(Lbl("Home planet"));
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        root.AddChild(grid);
        foreach (var (id, name) in Skins)
        {
            var b = new Button { Text = name, ToggleMode = true, ButtonPressed = o.PlanetSkin == id, CustomMinimumSize = new Vector2(138, 40) };
            b.Pressed += () =>
            {
                o.PlanetSkin = id; App.Save.Save();
                Sentinel.Audio.AudioManager.Instance?.Click();
                App.ShowSettings(); // reload so the backdrop updates
            };
            grid.AddChild(b);
        }
    }

    private static Label Lbl(string t)
    {
        var l = new Label { Text = t.ToUpperInvariant(), Modulate = new Color(1, 1, 1, 0.6f) };
        l.AddThemeFontSizeOverride("font_size", 11);
        return l;
    }
}
