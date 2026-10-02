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

        // ---------- top identity bar, PDTD's layout: portrait + underlapping rank
        // plate, name + XP bar, currency pills (no "+" — there's nothing to buy them
        // with, on purpose) ----------
        var topBar = new Control { AnchorLeft = 0f, AnchorRight = 1f, OffsetTop = 12, OffsetBottom = 172, OffsetLeft = 14, OffsetRight = -14 };
        topBar.Theme = UiTheme.Instance;
        AddChild(topBar);

        // portrait badge
        var badge = new PanelContainer { CustomMinimumSize = new Vector2(112, 112), Position = new Vector2(0, 4) };
        badge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.12f, 0.22f, 0.95f),
            BorderColor = UiTheme.Accent, BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
        });
        var bl = new Label
        {
            Text = $"{p.Commander}", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0f, AnchorBottom = 1f, OffsetBottom = -20,
        };
        bl.AddThemeFontOverride("font", UiTheme.Display);
        bl.AddThemeFontSizeOverride("font_size", 46);
        bl.AddThemeColorOverride("font_color", UiTheme.Accent);
        badge.AddChild(bl);
        topBar.AddChild(badge);

        // rank plate — underlaps the portrait's bottom edge, PDTD-style
        var rankPlate = new PanelContainer
        {
            CustomMinimumSize = new Vector2(112, 30), Position = new Vector2(0, 4 + 112 - 24),
        };
        rankPlate.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.03f, 0.06f, 0.96f),
            BorderColor = new Color(UiTheme.Accent, 0.7f), BorderWidthTop = 1,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        });
        var rankLbl = new Label { Text = "COMMANDER", HorizontalAlignment = HorizontalAlignment.Center };
        rankLbl.AddThemeFontSizeOverride("font_size", 12);
        rankPlate.AddChild(rankLbl);
        topBar.AddChild(rankPlate);

        var name = new Label { Text = "SENTINEL PILOT", Position = new Vector2(126, 6) };
        name.AddThemeFontOverride("font", UiTheme.Display);
        name.AddThemeFontSizeOverride("font_size", 26);
        topBar.AddChild(name);

        // XP bar toward the next Commander level — PDTD always shows this under the name
        double xpFloor = Meta.Progression.CmdrXpForLevel(p.Commander);
        double xpCeil = Meta.Progression.CmdrXpForLevel(p.Commander + 1);
        var xpBar = new ProgressBar
        {
            Position = new Vector2(126, 40), Size = new Vector2(220, 14),
            ShowPercentage = false, MaxValue = 1.0,
            Value = xpCeil > xpFloor ? Mathf.Clamp((s.Xp - xpFloor) / (xpCeil - xpFloor), 0, 1) : 0,
        };
        topBar.AddChild(xpBar);

        var sub = new Label { Text = $"Hero {p.Hero}/20    ·    RD {F(s.ResearchData)}    ·    Cores {s.SentinelCores}", Position = new Vector2(126, 74), Modulate = new Color(1, 1, 1, 0.66f) };
        sub.AddThemeFontSizeOverride("font_size", 17);
        topBar.AddChild(sub);

        // tap the rank badge / name to open the Commander profile
        var profileTap = new Button { Flat = true, Position = Vector2.Zero, Size = new Vector2(400, 116) };
        profileTap.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        profileTap.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        profileTap.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        profileTap.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        profileTap.Pressed += () => { Click(); App.ShowProfile(); };
        topBar.AddChild(profileTap);

        // top-right: currency pill (icon + number, no "+" — nothing to spend real
        // money on) then gear/codex as small plain icon buttons beside it.
        var pillRow = new HBoxContainer
        {
            AnchorLeft = 1f, AnchorRight = 1f, OffsetLeft = -360, OffsetRight = 0, OffsetTop = 4,
            Alignment = BoxContainer.AlignmentMode.End,
        };
        pillRow.AddThemeConstantOverride("separation", 8);
        topBar.AddChild(pillRow);

        pillRow.AddChild(CurrencyPill("✦", $"{App.Shop.Balance}", UiTheme.Accent2, ShowWallet));
        pillRow.AddChild(IconButton("☰", () => { Click(); App.ShowCodex(); }));
        pillRow.AddChild(IconButton("⚙", () => { Click(); App.ShowSettings(); }));

        var (mfile, mid2, mname, cleared) = NextMission();

        // ---------- stage label, tucked just under the planet ----------
        var stageLbl = new Label
        {
            Text = cleared ? "★  ALL CLEARED  —  PICK A STAGE" : mname.ToUpperInvariant(),
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.60f, AnchorBottom = 0.60f,
        };
        stageLbl.AddThemeFontOverride("font", UiTheme.Display);
        stageLbl.AddThemeFontSizeOverride("font_size", 28);
        stageLbl.AddThemeConstantOverride("outline_size", 5);
        stageLbl.AddThemeColorOverride("font_outline_color", new Color(0.01f, 0.03f, 0.06f, 0.9f));
        AddChild(stageLbl);

        // ---------- BATTLE — a raised CTA sitting just above the nav bar ----------
        var battle = new GlowButton
        {
            Text = cleared ? "STAR MAP" : "BATTLE", FontSize = 30, Primary = true,
            CustomMinimumSize = new Vector2(0, 92),
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = 60, OffsetRight = -60, OffsetTop = -226, OffsetBottom = -134,
        };
        battle.SetFont(UiTheme.Display);
        battle.Pressed += () => { Confirm(); if (cleared) App.ShowLevels(); else App.StartMission(mfile, mid2); };
        AddChild(battle);

        // ---------- PDTD-style bottom nav: one continuous bar, active tab gets the
        // slanted highlight, instead of five separate glowing boxes ----------
        var navBar = PdtdNav.Build(new (string, string, System.Action)[]
        {
            ("✦", "SHOP", () => { Click(); App.ShowShop(); }),
            ("⬡", "UPGRADES", () => { Click(); App.ShowUpgrades(); }),
            ("◈", "STAR MAP", () => { Click(); App.ShowLevels(); }),
            ("★", "EVENTS", () => { Click(); App.ShowEvents(); }),
        }, activeIndex: -1);
        navBar.Theme = UiTheme.Instance;
        navBar.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        navBar.OffsetTop = -116;
        AddChild(navBar);

        // ---------- app version, sits just above the nav bar ----------
        var ver = new Label
        {
            Text = $"BEYOND  v{ProjectSettings.GetSetting("application/config/version", "0.0.0")}",
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetTop = -134, OffsetBottom = -116,
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1, 1, 1, 0.4f),
        };
        ver.AddThemeFontSizeOverride("font_size", 15);
        AddChild(ver);
    }

    /// <summary>PDTD-style currency pill: a round icon chip, the number, no "+" —
    /// there's nothing to spend real money to top it up with, on purpose.</summary>
    private static Control CurrencyPill(string glyph, string value, Color accent, System.Action onPress)
    {
        var b = new Button { CustomMinimumSize = new Vector2(96, 52), Flat = true };
        b.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.09f, 0.92f),
            BorderColor = new Color(accent, 0.6f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 26, CornerRadiusTopRight = 26, CornerRadiusBottomLeft = 26, CornerRadiusBottomRight = 26,
        });
        b.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.08f, 0.13f, 0.95f),
            BorderColor = accent, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 26, CornerRadiusTopRight = 26, CornerRadiusBottomLeft = 26, CornerRadiusBottomRight = 26,
        });
        var lbl = new Label { Text = $"{glyph} {value}", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        lbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        lbl.AddThemeFontSizeOverride("font_size", 19);
        lbl.AddThemeColorOverride("font_color", accent);
        lbl.MouseFilter = Control.MouseFilterEnum.Ignore;
        b.AddChild(lbl);
        b.Pressed += onPress;
        return b;
    }

    /// <summary>A small plain circular icon button — settings/codex, not currencies.</summary>
    private static Control IconButton(string glyph, System.Action onPress)
    {
        var b = new Button { CustomMinimumSize = new Vector2(52, 52), Flat = true, Text = glyph };
        b.AddThemeFontSizeOverride("font_size", 20);
        b.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.09f, 0.92f),
            BorderColor = new Color(1, 1, 1, 0.18f), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 26, CornerRadiusTopRight = 26, CornerRadiusBottomLeft = 26, CornerRadiusBottomRight = 26,
        });
        b.Pressed += onPress;
        return b;
    }

    /// <summary>Popup that lists every currency balance.</summary>
    private void ShowWallet()
    {
        var s = App.Save;
        var pop = TapAwayPopup.Open(this, "WALLET", 820f);
        void Row(string label, string val, Color col)
        {
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 24);
            var a = new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            a.AddThemeFontSizeOverride("font_size", 28);
            var b = new Label { Text = val, HorizontalAlignment = HorizontalAlignment.Right };
            b.AddThemeFontOverride("font", UiTheme.Display);
            b.AddThemeFontSizeOverride("font_size", 30);
            b.AddThemeColorOverride("font_color", col);
            h.AddChild(a); h.AddChild(b);
            pop.Body.AddChild(h);
        }
        Row("✦  Commendations", $"{App.Shop.Balance}", UiTheme.Accent2);
        Row("◇  Research Data", F(s.ResearchData), UiTheme.Accent);
        Row("❖  Exotic Alloy", F(s.ExoticAlloy), new Color(0.95f, 0.78f, 0.42f));
        Row("✷  Sentinel Cores", $"{s.SentinelCores}", new Color(0.72f, 0.86f, 1f));
        Row("🔑  Silver Keys", $"{s.SilverKeys}", new Color(0.72f, 0.76f, 0.84f));
        Row("🔶  Gold Keys", $"{s.GoldKeys}", new Color(0.95f, 0.78f, 0.25f));
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

    private static void Click() => Sentinel.Audio.AudioManager.Instance?.Click();
    private static void Confirm() => Sentinel.Audio.AudioManager.Instance?.Confirm();
    private static string Mmss(int secs) => $"{secs / 60}:{secs % 60:00}";
    private static string F(double v) => Mathf.FloorToInt((float)v).ToString();
}
