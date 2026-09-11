using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// Home hub — a top identity/currency bar, the rotating Earth, the current stage
/// + BATTLE button just under the planet, and a row of section icons. Laid out
/// after the PDTD home screen.
/// </summary>
public sealed partial class MenuScreen : CanvasLayer
{
    public AppRoot App = null!;

    public override void _Ready()
    {
        Layer = 5;
        AddChild(new MenuBackground { ShowPlanet = true, PlanetY = 0.40f, PlanetScale = 1.05f });
        AddChild(new Sentinel.Meta.UpdateChecker());

        var s = App.Save;
        App.RefreshProgression();
        var p = App.Prog;

        // ---------- top identity + currency bar ----------
        var topBar = new Control { AnchorLeft = 0f, AnchorRight = 1f, OffsetTop = 12, OffsetBottom = 118, OffsetLeft = 14, OffsetRight = -14 };
        topBar.Theme = UiTheme.Instance;
        AddChild(topBar);

        // rank badge
        var badge = new PanelContainer { CustomMinimumSize = new Vector2(84, 84) };
        badge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.10f, 0.18f, 0.9f),
            BorderColor = UiTheme.Accent, BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
        });
        badge.Position = new Vector2(0, 6);
        var bl = new Label { Text = $"{p.Commander}", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        bl.AddThemeFontOverride("font", UiTheme.Display);
        bl.AddThemeFontSizeOverride("font_size", 38);
        bl.AddThemeColorOverride("font_color", UiTheme.Accent);
        badge.AddChild(bl);
        topBar.AddChild(badge);

        var name = new Label { Text = "COMMANDER", Position = new Vector2(100, 8) };
        name.AddThemeFontOverride("font", UiTheme.Display);
        name.AddThemeFontSizeOverride("font_size", 28);
        topBar.AddChild(name);
        var sub = new Label { Text = $"Hero {p.Hero}/20    ·    RD {F(s.ResearchData)}    ·    Cores {s.SentinelCores}", Position = new Vector2(100, 50), Modulate = new Color(1, 1, 1, 0.66f) };
        sub.AddThemeFontSizeOverride("font_size", 18);
        topBar.AddChild(sub);

        // tap the rank badge / name to open the Commander profile
        var profileTap = new Button { Flat = true, Position = Vector2.Zero, Size = new Vector2(330, 92) };
        profileTap.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        profileTap.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        profileTap.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        profileTap.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        profileTap.Pressed += () => { Click(); App.ShowProfile(); };
        topBar.AddChild(profileTap);

        // currency chip — tap to see every balance
        var comm = new GlowButton { Text = $"✦ {App.Shop.Balance}", FontSize = 24, Alt = true, CustomMinimumSize = new Vector2(150, 84) };
        comm.AnchorLeft = 1f; comm.AnchorRight = 1f; comm.OffsetLeft = -246; comm.OffsetRight = -100; comm.OffsetTop = 6;
        comm.Pressed += () => { Click(); ShowWallet(); };
        topBar.AddChild(comm);

        var gear = new GlowButton { Text = "⚙", FontSize = 40, Alt = true, CustomMinimumSize = new Vector2(84, 84) };
        gear.AnchorLeft = 1f; gear.AnchorRight = 1f; gear.OffsetLeft = -84; gear.OffsetRight = 0; gear.OffsetTop = 6;
        gear.Pressed += () => { Click(); App.ShowSettings(); };
        topBar.AddChild(gear);

        var (mfile, mid2, mname, cleared) = NextMission();

        // ---------- stage label, tucked just under the planet ----------
        var stageLbl = new Label
        {
            Text = cleared ? "★  ALL CLEARED  —  PICK A STAGE" : mname.ToUpperInvariant(),
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.60f, AnchorBottom = 0.60f,
        };
        stageLbl.AddThemeFontOverride("font", UiTheme.Display);
        stageLbl.AddThemeFontSizeOverride("font_size", 20);
        stageLbl.AddThemeConstantOverride("outline_size", 5);
        stageLbl.AddThemeColorOverride("font_outline_color", new Color(0.01f, 0.03f, 0.06f, 0.9f));
        AddChild(stageLbl);

        var starMapBtn = new GlowButton
        {
            Text = "◈  STAR MAP", FontSize = 15, Alt = false,
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.60f, AnchorBottom = 0.60f,
            OffsetLeft = -90, OffsetRight = 90, OffsetTop = 30, OffsetBottom = 74,
        };
        starMapBtn.Theme = UiTheme.Instance;
        starMapBtn.Pressed += () => { Click(); App.ShowLevels(); };
        AddChild(starMapBtn);

        // ---------- PDTD-style bottom bar: Shop · Upgrades · BATTLE · Sentinels · Events ----------
        var bar = new HBoxContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = 14, OffsetRight = -14, OffsetTop = -134, OffsetBottom = -30,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        bar.AddThemeConstantOverride("separation", 8);
        bar.Theme = UiTheme.Instance;
        AddChild(bar);

        bar.AddChild(NavBtn("✦\nSHOP", App.ShowShop, false));
        bar.AddChild(NavBtn("⬡\nUPGRADES", App.ShowUpgrades, false));

        var battle = new GlowButton
        {
            Text = cleared ? "STAR MAP" : "BATTLE", FontSize = 28, Primary = true,
            CustomMinimumSize = new Vector2(170, 104),
        };
        battle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        battle.SizeFlagsStretchRatio = 1.5f;
        battle.SetFont(UiTheme.Display);
        battle.Pressed += () => { Confirm(); if (cleared) App.ShowLevels(); else App.StartMission(mfile, mid2); };
        bar.AddChild(battle);

        bar.AddChild(NavBtn("✷\nSENTINELS", App.ShowSentinels, true));

        var wk = Sentinel.Meta.WeeklyChallenge.Current();
        bar.AddChild(NavBtn("★\nEVENTS", App.StartWeekly, true));

        // ---------- app version, bottom-centre ----------
        var ver = new Label
        {
            Text = $"BEYOND  v{ProjectSettings.GetSetting("application/config/version", "0.0.0")}",
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetTop = -34, OffsetBottom = -8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1, 1, 1, 0.4f),
        };
        ver.AddThemeFontSizeOverride("font_size", 15);
        AddChild(ver);
    }

    /// <summary>Popup that lists every currency balance.</summary>
    private void ShowWallet()
    {
        var s = App.Save;
        var dlg = new AcceptDialog
        {
            Title = "WALLET",
            Theme = UiTheme.Instance,
            Unresizable = true,
            OkButtonText = "CLOSE",
        };
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        void Row(string label, string val, Color col)
        {
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 24);
            var a = new Label { Text = label, CustomMinimumSize = new Vector2(230, 0) };
            a.AddThemeFontSizeOverride("font_size", 22);
            var b = new Label { Text = val, HorizontalAlignment = HorizontalAlignment.Right, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            b.AddThemeFontOverride("font", UiTheme.Display);
            b.AddThemeFontSizeOverride("font_size", 24);
            b.AddThemeColorOverride("font_color", col);
            h.AddChild(a); h.AddChild(b);
            box.AddChild(h);
        }
        Row("✦  Commendations", $"{App.Shop.Balance}", UiTheme.Accent2);
        Row("◇  Research Data", F(s.ResearchData), UiTheme.Accent);
        Row("❖  Exotic Alloy", F(s.ExoticAlloy), new Color(0.95f, 0.78f, 0.42f));
        Row("✷  Sentinel Cores", $"{s.SentinelCores}", new Color(0.72f, 0.86f, 1f));
        dlg.AddChild(box);
        AddChild(dlg);
        dlg.Confirmed += dlg.QueueFree;
        dlg.Canceled += dlg.QueueFree;
        dlg.PopupCentered(new Vector2I(560, 360));
    }

    private (string file, string id, string name, bool cleared) NextMission()
    {
        var ids = new System.Collections.Generic.List<string>();
        foreach (var m in App.Cfg.Arc.Missions) ids.Add(m.Id);
        int idx = 1;
        foreach (var m in App.Cfg.Arc.Missions)
        {
            if (!App.Save.Record(m.Id).Cleared && App.Save.IsUnlocked(m.Id, ids))
                return (m.File, m.Id, $"Stage {idx} · {m.Name.Split('·')[^1].Trim()}", false);
            idx++;
        }
        return ("", "", "", true);
    }

    private GlowButton NavBtn(string label, System.Action onPress, bool alt)
    {
        var b = new GlowButton { Text = label, FontSize = 15, Alt = alt, CustomMinimumSize = new Vector2(0, 92) };
        b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        b.Pressed += () => { Click(); onPress(); };
        return b;
    }

    private static void Click() => Sentinel.Audio.AudioManager.Instance?.Click();
    private static void Confirm() => Sentinel.Audio.AudioManager.Instance?.Confirm();
    private static string Mmss(int secs) => $"{secs / 60}:{secs % 60:00}";
    private static string F(double v) => Mathf.FloorToInt((float)v).ToString();
}
