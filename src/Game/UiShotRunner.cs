using Godot;

namespace Sentinel.Game;

/// <summary>
/// Dev-only: boots the real AppRoot and drives it straight to Menu/Shop/Upgrades/Research/Codex by
/// calling the same methods the UI buttons call — no synthetic input, no clicking.
/// Screenshots are written via the viewport texture (same trick as ShotRunner), so this
/// never needs to know where the window sits on screen or steal the real mouse.
///   godot --path . scenes/UiShots.tscn
/// Files land in the Godot user dir (see the printed path each Grab() logs).
/// </summary>
public sealed partial class UiShotRunner : Node
{
    private AppRoot _app = null!;
    private double _t;
    private int _shot;
    private int _step;

    public override void _Ready()
    {
        var win = GetWindow();
        win.Mode = Window.ModeEnum.Windowed;
        win.Size = new Vector2I(432, 768); // the project's portrait window override — a phone-shaped frame
        // A tiling WM (Hyprland) ignores the requested size, and with aspect=expand a
        // landscape tile re-lays-out every screen as if on a tablet. Keep pins the real
        // 1080x1920 phone canvas (letterboxed), so shots match what a phone shows.
        win.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
        _app = new AppRoot();
        AddChild(_app);
    }

    public override void _Process(double delta)
    {
        _t += delta;
        if (AppRoot.Instance == null) return;

        switch (_step)
        {
            case 0:
                if (_t > 0.3) { AppRoot.Instance.ShowMenu(); Advance("menu"); }
                break;
            case 1:
                if (_t > 1.2) { AppRoot.Instance.ShowShop(); Advance("shop"); }
                break;
            case 2:
                if (_t > 1.2) { AppRoot.Instance.ShowUpgrades(); Advance("upgrades_chip"); }
                break;
            case 3:
                if (_t > 1.2) { AppRoot.Instance.ShowResearch(); Advance("research"); }
                break;
            case 4:
                if (_t > 1.2) { AppRoot.Instance.ShowCodex(); Advance("codex"); }
                break;
            case 5:
                if (_t > 2.0) { GD.Print("=== UiShotRunner timeout, quitting"); GetTree().Quit(); }
                break;
        }
    }

    /// <summary>Grab the screen the current step just landed on (well before the next
    /// step's own threshold fires, so the swap never races the screenshot), then move on.</summary>
    private void Advance(string labelJustShown)
    {
        GetTree().CreateTimer(0.6).Timeout += () => Grab(labelJustShown);
        _step++;
        _t = 0;
    }

    private void Grab(string label)
    {
        var img = GetViewport().GetTexture().GetImage();
        string path = $"user://uishot_{_shot++}_{label}.png";
        img.SavePng(path);
        GD.Print($"wrote {ProjectSettings.GlobalizePath(path)}");
    }
}
