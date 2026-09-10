using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// Home hub — a top identity/currency bar, the rotating Earth, the current stage
/// + BATTLE button, and a row of section icons. Laid out after the PDTD home
/// screen.
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
        var topBar = new Control { AnchorLeft = 0f, AnchorRight = 1f, OffsetTop = 10, OffsetBottom = 92, OffsetLeft = 12, OffsetRight = -12 };
        topBar.Theme = UiTheme.Instance;
        AddChild(topBar);

        // rank badge
        var badge = new PanelContainer { CustomMinimumSize = new Vector2(64, 64), OffsetLeft = 0, OffsetTop = 6, AnchorTop = 0f };
        badge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.10f, 0.18f, 0.9f),
            BorderColor = UiTheme.Accent, BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        });
        badge.Position = new Vector2(0, 6);
        var bl = new Label { Text = $"{p.Commander}", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        bl.AddThemeFontSizeOverride("font_size", 30);
        bl.AddThemeColorOverride("font_color", UiTheme.Accent);
        badge.AddChild(bl);
        topBar.AddChild(badge);

        var name = new Label { Text = "COMMANDER", Position = new Vector2(78, 8) };
        name.AddThemeFontSizeOverride("font_size", 22);
        topBar.AddChild(name);
        var sub = new Label { Text = $"Hero {p.Hero}/20   ·   RD {F(s.ResearchData)}   ·   Cores {s.SentinelCores}", Position = new Vector2(78, 40), Modulate = new Color(1, 1, 1, 0.62f) };
        sub.AddThemeFontSizeOverride("font_size", 14);
        topBar.AddChild(sub);

        var comm = new Label { Text = $"✦ {App.Shop.Balance}", AnchorLeft = 1f, AnchorRight = 1f, OffsetLeft = -190, OffsetRight = -78, OffsetTop = 22, HorizontalAlignment = HorizontalAlignment.Right };
        comm.AddThemeFontSizeOverride("font_size", 20);
        comm.AddThemeColorOverride("font_color", UiTheme.Accent2);
        topBar.AddChild(comm);

        var gear = new GlowButton { Text = "⚙", FontSize = 26, Alt = true, CustomMinimumSize = new Vector2(60, 60) };
        gear.AnchorLeft = 1f; gear.AnchorRight = 1f; gear.OffsetLeft = -60; gear.OffsetRight = 0; gear.OffsetTop = 6;
        gear.Pressed += () => { Click(); App.ShowSettings(); };
        topBar.AddChild(gear);

        // ---------- lower stack: stage + BATTLE + icons ----------
        var lower = new VBoxContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = 20, OffsetRight = -20, OffsetTop = -340, OffsetBottom = -18,
            Alignment = BoxContainer.AlignmentMode.End,
        };
        lower.AddThemeConstantOverride("separation", 12);
        lower.Theme = UiTheme.Instance;
        AddChild(lower);

        var (mfile, mid, mname, cleared) = NextMission();
        var stageLbl = new Label { Text = cleared ? "★  ALL CLEARED  —  PICK A STAGE" : mname.ToUpperInvariant(), HorizontalAlignment = HorizontalAlignment.Center };
        stageLbl.AddThemeFontSizeOverride("font_size", 22);
        stageLbl.AddThemeConstantOverride("outline_size", 5);
        stageLbl.AddThemeColorOverride("font_outline_color", new Color(0.01f, 0.03f, 0.06f, 0.9f));
        lower.AddChild(stageLbl);

        var battle = new GlowButton { Text = cleared ? "STAR MAP" : "BATTLE", FontSize = 40, Primary = true, CustomMinimumSize = new Vector2(0, 116) };
        battle.Pressed += () => { Confirm(); if (cleared) App.ShowLevels(); else App.StartMission(mfile, mid); };
        lower.AddChild(battle);

        var wk = Sentinel.Meta.WeeklyChallenge.Current();
        string wkText = $"★  WEEKLY · {wk.Title}" + (s.WeeklyId == wk.Id && s.WeeklyBest > 0 ? $"   ·   best {Mmss(s.WeeklyBest)}" : "");
        var weekly = new GlowButton { Text = wkText, FontSize = 17, Alt = true, CustomMinimumSize = new Vector2(0, 58) };
        weekly.Pressed += () => { Click(); App.StartWeekly(); };
        lower.AddChild(weekly);

        var icons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        icons.AddThemeConstantOverride("separation", 10);
        lower.AddChild(icons);
        icons.AddChild(IconBtn("◈", "Star Map", App.ShowLevels, false));
        icons.AddChild(IconBtn("∞", "Endless", () => App.StartMission("res://data/missions/endless.json", "endless"), false));
        icons.AddChild(IconBtn("⬡", "Research", App.ShowResearch, false));
        icons.AddChild(IconBtn("◆", "Protocols", App.ShowAbilities, true));
        icons.AddChild(IconBtn("✦", "Shop", App.ShowShop, true));
        icons.AddChild(IconBtn("☰", "Codex", App.ShowCodex, false));
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

    private GlowButton IconBtn(string glyph, string tip, System.Action onPress, bool alt)
    {
        var b = new GlowButton { Text = glyph, FontSize = 28, Alt = alt, CustomMinimumSize = new Vector2(0, 66) };
        b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        b.TooltipText = tip;
        b.Pressed += () => { Click(); onPress(); };
        return b;
    }

    private static void Click() => Sentinel.Audio.AudioManager.Instance?.Click();
    private static void Confirm() => Sentinel.Audio.AudioManager.Instance?.Confirm();
    private static string Mmss(int secs) => $"{secs / 60}:{secs % 60:00}";
    private static string F(double v) => Mathf.FloorToInt((float)v).ToString();
}
