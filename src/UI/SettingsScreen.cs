using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>Sound, haptics, accessibility. Cosmetics live in the Shop.</summary>
public sealed partial class SettingsScreen : CanvasLayer
{
    public AppRoot App = null!;

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.22f, NebulaAlpha = 0.4f });

        var wrap = new CenterContainer();
        wrap.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        wrap.Theme = UiTheme.Instance;
        AddChild(wrap);

        var root = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
        root.AddThemeConstantOverride("separation", 18);
        wrap.AddChild(root);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(150, 60) };
        back.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Back(); App.ShowMenu(); };
        head.AddChild(back);
        var title = new Label { Text = "  SETTINGS", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        head.AddChild(title);

        var o = App.Save.Options;

        // sfx volume
        root.AddChild(Lbl("Sound effects"));
        var vol = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = o.SfxVolume, CustomMinimumSize = new Vector2(0, 48) };
        vol.ValueChanged += v =>
        {
            o.SfxVolume = (float)v; App.Save.Save();
            Sentinel.Audio.AudioManager.Instance?.SetVolume(o.SfxVolume, o.Muted);
        };
        vol.DragEnded += _ => Sentinel.Audio.AudioManager.Instance?.Play("ui_confirm", -4f);
        root.AddChild(vol);

        root.AddChild(Lbl("Music"));
        var mvol = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = o.MusicVolume, CustomMinimumSize = new Vector2(0, 48) };
        mvol.ValueChanged += v =>
        {
            o.MusicVolume = (float)v; App.Save.Save();
            Sentinel.Audio.MusicPlayer.Instance?.SetVolume(o.MusicVolume, o.Muted);
        };
        root.AddChild(mvol);

        var mute = new CheckButton { Text = "Mute everything", ButtonPressed = o.Muted };
        mute.Toggled += b =>
        {
            o.Muted = b; App.Save.Save();
            Sentinel.Audio.AudioManager.Instance?.SetVolume(o.SfxVolume, o.Muted);
            Sentinel.Audio.MusicPlayer.Instance?.SetVolume(o.MusicVolume, o.Muted);
        };
        root.AddChild(mute);

        var flash = new CheckButton { Text = "Reduce screen flashes", ButtonPressed = o.ReduceFlash };
        flash.Toggled += b => { o.ReduceFlash = b; App.Save.Save(); };
        root.AddChild(flash);

        root.AddChild(new HSeparator());
        root.AddChild(Lbl("Appearance"));
        var toShop = new Button { Text = "Hero hulls, worlds & ordnance colours  ›  Shop", CustomMinimumSize = new Vector2(0, 60), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        toShop.AddThemeFontSizeOverride("font_size", 17);
        toShop.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Click(); App.ShowShop(); };
        root.AddChild(toShop);
    }

    private static Label Lbl(string t)
    {
        var l = new Label { Text = t.ToUpperInvariant(), Modulate = new Color(1, 1, 1, 0.6f) };
        l.AddThemeFontSizeOverride("font_size", 16);
        return l;
    }
}
