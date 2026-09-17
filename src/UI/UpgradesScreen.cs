using System.Collections.Generic;

using Godot;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// UPGRADES — everything that improves the planet and the commander outside battle,
/// laid out the way PDTD's own Upgrades screen is.
///
/// PDTD puts a vertical category rail down the left (Chip / Planet / Force Shield /
/// MotherShip / Cosmic) and a row of sub-tabs along the bottom whose contents change
/// with the category. Beyond's equivalents:
///
///   Chip         -> the Armory's chip inventory + equipped slots
///   Planet       -> Research / Ultimate / Skins, as PDTD's Planet page splits
///   Force Shield -> the persistent Planet Shield track
///   MotherShip   -> the commander's ship: hero weapons and abilities
///   Cosmic       -> account-wide modules
///
/// Each category owns its own sub-tabs, and the body is rebuilt on every switch. The
/// previous version was a flat list of nav buttons to separate full-screen screens,
/// which is why it read as nothing like the reference.
/// </summary>
public sealed partial class UpgradesScreen : CanvasLayer
{
    public AppRoot App = null!;

    private static readonly (string id, string name, string icon)[] Categories =
    {
        ("chip",    "Chip",         "module/icon_quantacore"),
        ("planet",  "Planet",       "module/icon_reactor"),
        ("shield",  "Force Shield", "module/icon_shield"),
        ("ship",    "MotherShip",   "module/icon_weapon"),
        ("cosmic",  "Cosmic",       "module/icon_radar"),
    };

    /// <summary>Sub-tabs per category, mirroring PDTD's bottom tab row.</summary>
    private static readonly System.Collections.Generic.Dictionary<string, (string id, string name)[]> Tabs = new()
    {
        ["chip"] = new[] { ("chips", "Chip"), ("modules", "Module"), ("items", "Item") },
        ["planet"] = new[] { ("research", "Research"), ("ultimate", "Ultimate"), ("skins", "Planet") },
        ["shield"] = new[] { ("upgrade", "Upgrade") },
        ["ship"] = new[] { ("weapons", "Weapons"), ("abilities", "Protocols") },
        ["cosmic"] = new[] { ("modules", "Module") },
    };

    private string _cat = "chip";
    private string _tab = "chips";

    private VBoxContainer _body = null!;
    private HBoxContainer _tabRow = null!;
    private Control _walletBar = null!;

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.42f, PlanetScale = 0.6f, NebulaAlpha = 0.12f });

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -540, OffsetRight = 540, OffsetTop = 26, OffsetBottom = -10,
        };
        root.AddThemeConstantOverride("separation", 8);
        root.Theme = UiTheme.Instance;
        AddChild(MenuFrame.Around(root));
        AddChild(root);

        // ---- header ----
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(190, 76) };
        back.Pressed += () => App.ShowMenu();
        head.AddChild(back);
        var title = new Label { Text = "  UPGRADES", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontOverride("font", UiTheme.Display);
        title.AddThemeFontSizeOverride("font_size", 32);
        head.AddChild(title);

        _walletBar = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        head.AddChild(_walletBar);

        // ---- middle: category rail + body ----
        var mid = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        mid.AddThemeConstantOverride("separation", 10);
        root.AddChild(mid);

        var rail = new VBoxContainer { CustomMinimumSize = new Vector2(150, 0) };
        rail.AddThemeConstantOverride("separation", 6);
        mid.AddChild(rail);
        foreach (var (id, name, icon) in Categories)
        {
            string cid = id;
            rail.AddChild(RailButton(cid, name, icon));
        }

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        mid.AddChild(scroll);
        _body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_body);

        // ---- bottom sub-tabs ----
        _tabRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _tabRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(_tabRow);

        Rebuild();
    }

    private Control RailButton(string id, string name, string iconPath)
    {
        var b = new Button
        {
            CustomMinimumSize = new Vector2(0, 92),
            ToggleMode = true,
            TooltipText = name,
        };
        b.AddThemeFontSizeOverride("font_size", 15);
        b.Text = "\n" + name;
        b.Pressed += () =>
        {
            _cat = id;
            _tab = Tabs[id][0].id;
            Sentinel.Audio.AudioManager.Instance?.Click();
            Rebuild();
        };
        b.SetMeta("cat", id);

        var tex = Render.Art.Pdtd(iconPath);
        if (tex != null)
        {
            var ic = new TextureRect
            {
                Texture = tex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 0f,
                OffsetLeft = -22, OffsetRight = 22, OffsetTop = 8, OffsetBottom = 52,
                Modulate = new Color(1, 1, 1, 0.9f),
            };
            b.AddChild(ic);
        }
        return b;
    }

    private void Rebuild()
    {
        App.RefreshProgression();
        BuildWallet();

        // rail selection
        foreach (Node n in ((HBoxContainer)_body.GetParent().GetParent()).GetChild(0).GetChildren())
            if (n is Button rb && rb.HasMeta("cat")) rb.ButtonPressed = (string)rb.GetMeta("cat") == _cat;

        // sub-tabs
        foreach (Node c in _tabRow.GetChildren()) c.QueueFree();
        foreach (var (id, name) in Tabs[_cat])
        {
            string tid = id;
            var t = new Button
            {
                Text = name, ToggleMode = true, ButtonPressed = _tab == id,
                CustomMinimumSize = new Vector2(168, 72),
            };
            t.AddThemeFontSizeOverride("font_size", 20);
            t.Pressed += () => { _tab = tid; Sentinel.Audio.AudioManager.Instance?.Click(); Rebuild(); };
            _tabRow.AddChild(t);
        }

        foreach (Node c in _body.GetChildren()) c.QueueFree();

        // PDTD keeps the equipped module slots pinned above the tab contents on every
        // page of this screen, so you can always see what's actually fitted while you
        // shop for upgrades. Same here.
        BuildSlotStrip();

        switch (_cat + "/" + _tab)
        {
            case "planet/ultimate": BuildUltimate(); break;
            case "planet/skins": BuildSkins(); break;
            case "planet/research": BuildResearch(); break;
            case "chip/chips": BuildChips(); break;
            case "chip/modules":
            case "cosmic/modules": BuildModules(); break;
            case "chip/items": BuildItems(); break;
            case "shield/upgrade": BuildShield(); break;
            case "ship/weapons": BuildShip(); break;
            case "ship/abilities": Jump("Open Protocols", () => App.ShowAbilities()); break;
            default: BuildItems(); break;
        }
    }

    // -------------------------------------------------------------- slot strip ----

    /// <summary>The six equipped module slots, as PDTD pins them above the tab body:
    /// tier badge, the module's art, and its level.</summary>
    private void BuildSlotStrip()
    {
        var p = App.Prog;
        var strip = new GridContainer { Columns = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        strip.AddThemeConstantOverride("h_separation", 6);
        strip.AddThemeConstantOverride("v_separation", 6);
        _body.AddChild(strip);

        var mods = App.Cfg.Modules.Modules;
        for (int i = 0; i < App.Cfg.Modules.Slots; i++)
        {
            var def = i < mods.Count ? mods[i] : null;
            int lvl = def == null ? 0 : p.ModuleLevel(def.Id);
            int tier = Sentinel.Meta.Progression.ModuleTier(lvl);
            var accent = TierColor(tier);

            var cell = new PanelContainer { CustomMinimumSize = new Vector2(0, 92) };
            cell.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(accent, lvl > 0 ? 0.13f : 0.05f),
                BorderColor = new Color(accent, lvl > 0 ? 0.75f : 0.3f),
                BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
                ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6,
            });
            strip.AddChild(cell);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            cell.AddChild(row);

            var art = Render.Art.Pdtd("module/" + ModuleArt(i));
            if (art != null)
                row.AddChild(new TextureRect
                {
                    Texture = art, CustomMinimumSize = new Vector2(58, 58),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    Modulate = new Color(1, 1, 1, lvl > 0 ? 1f : 0.4f),
                });

            var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            col.AddThemeConstantOverride("separation", 1);
            row.AddChild(col);
            var badge = new Label { Text = tier == 0 ? "—" : $"◈ T{tier}" };
            badge.AddThemeFontOverride("font", UiTheme.Display);
            badge.AddThemeFontSizeOverride("font_size", 16);
            badge.AddThemeColorOverride("font_color", accent.Lightened(0.3f));
            col.AddChild(badge);
            var nm = new Label { Text = def?.Name ?? "empty", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            nm.AddThemeFontSizeOverride("font_size", 12);
            nm.Modulate = new Color(1, 1, 1, 0.6f);
            col.AddChild(nm);
            var lv = new Label { Text = lvl > 0 ? $"Lv.{lvl}" : "—" };
            lv.AddThemeFontSizeOverride("font_size", 15);
            col.AddChild(lv);
        }
    }

    private static Color TierColor(int tier) => tier switch
    {
        0 => new Color(0.45f, 0.48f, 0.56f),
        1 => new Color(0.42f, 0.78f, 0.95f),
        2 => new Color(0.72f, 0.45f, 0.95f),
        _ => new Color(0.98f, 0.78f, 0.30f),
    };

    /// <summary>PDTD's six equipment families, in slot order, for the strip's art.</summary>
    private static string ModuleArt(int slot) => slot switch
    {
        0 => "art_weapon",
        1 => "art_shield",
        2 => "art_engine",
        3 => "art_reactor",
        4 => "art_radar",
        _ => "art_quantacore",
    };

    // -------------------------------------------------------------------- chips ----

    private void BuildChips()
    {
        var vault = App.Chips;

        _body.AddChild(Header("EQUIPPED"));
        var slots = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        slots.AddThemeConstantOverride("separation", 8);
        _body.AddChild(slots);
        for (int i = 0; i < App.Cfg.Chips.EquipSlots; i++)
        {
            string? k = i < App.Save.EquippedChips.Count ? App.Save.EquippedChips[i] : null;
            slots.AddChild(EquippedChipCell(k));
        }

        var head = new HBoxContainer();
        _body.AddChild(head);
        var h = Header("CHIP");
        h.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(h);
        var qm = new Button { Text = "⚡ Quick Merge", CustomMinimumSize = new Vector2(190, 62) };
        qm.AddThemeFontSizeOverride("font_size", 17);
        qm.Pressed += () => { vault.QuickMergeAll(); Sentinel.Audio.AudioManager.Instance?.Click(); Rebuild(); };
        head.AddChild(qm);

        var grid = new GridContainer { Columns = 6, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        _body.AddChild(grid);

        bool any = false;
        foreach (var def in vault.Archetypes)
            foreach (var td in vault.Tiers)
            {
                int n = vault.Count(def.Id, td.Tier);
                if (n <= 0) continue;
                any = true;
                grid.AddChild(ChipCell(def, td, n));
            }
        if (!any) _body.AddChild(Dim("No chips yet — open a chest in the Armory."));

        var toArmory = new Button { Text = "🗝  Armory — chests and fusing", CustomMinimumSize = new Vector2(0, 76) };
        toArmory.AddThemeFontSizeOverride("font_size", 20);
        UiTheme.StylePrimary(toArmory);
        toArmory.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Confirm(); App.ShowChips(); };
        _body.AddChild(toArmory);
    }

    private Control EquippedChipCell(string? key)
    {
        var holder = new PanelContainer { CustomMinimumSize = new Vector2(96, 96) };
        Config.ChipDef? def = null; Config.ChipTierDef? td = null;
        if (key != null)
        {
            var parts = key.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out int tier))
            {
                def = App.Cfg.Chips.Chips.Find(x => x.Id == parts[0]);
                td = App.Chips.TierDef(tier);
            }
        }
        var accent = td != null ? HexColor(td.Color, UiTheme.Accent) : new Color(0.4f, 0.45f, 0.55f);
        holder.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, def != null ? 0.14f : 0.04f),
            BorderColor = new Color(accent, def != null ? 0.8f : 0.3f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 4, ContentMarginBottom = 4,
        });
        if (def != null && td != null)
        {
            var b = new Button { Flat = true, TooltipText = $"{def.Name} · {td.Name}\ntap to unequip" };
            b.Pressed += () => { App.Chips.Unequip(def.Id, td.Tier); Rebuild(); };
            holder.AddChild(b);
            var plate = ChipScreen.ChipPlate(def, td, 78);
            plate.MouseFilter = Control.MouseFilterEnum.Ignore;
            holder.AddChild(plate);
        }
        else
        {
            var l = new Label
            {
                Text = "empty", HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, Modulate = new Color(1, 1, 1, 0.35f),
            };
            l.AddThemeFontSizeOverride("font_size", 14);
            holder.AddChild(l);
        }
        return holder;
    }

    private Control ChipCell(Config.ChipDef def, Config.ChipTierDef td, int count)
    {
        var accent = HexColor(td.Color, UiTheme.Accent);
        bool equipped = App.Chips.IsEquipped(def.Id, td.Tier);
        var p = new PanelContainer { CustomMinimumSize = new Vector2(0, 104) };
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, equipped ? 0.18f : 0.07f),
            BorderColor = new Color(accent, equipped ? 0.95f : 0.45f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 3, ContentMarginRight = 3, ContentMarginTop = 3, ContentMarginBottom = 3,
        });

        var b = new Button
        {
            Flat = true,
            TooltipText = $"{def.Name}\n{td.Name} · {def.Text} +{def.EffectPerTier * td.Tier * 100f:0}%\n"
                        + (equipped ? "tap to unequip" : "tap to equip"),
        };
        b.Pressed += () =>
        {
            if (equipped) App.Chips.Unequip(def.Id, td.Tier);
            else App.Chips.Equip(def.Id, td.Tier);
            Sentinel.Audio.AudioManager.Instance?.Click();
            Rebuild();
        };
        p.AddChild(b);

        var plate = ChipScreen.ChipPlate(def, td, 74);
        plate.MouseFilter = Control.MouseFilterEnum.Ignore;
        p.AddChild(plate);

        var cnt = new Label
        {
            Text = $"{count}", HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        cnt.AddThemeFontSizeOverride("font_size", 15);
        cnt.AddThemeConstantOverride("outline_size", 5);
        cnt.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        p.AddChild(cnt);
        return p;
    }

    // ------------------------------------------------------------------ modules ----

    private void BuildModules()
    {
        var p = App.Prog;
        _body.AddChild(Dim($"◇ {F(App.Save.ResearchData)} Research Data   ·   "
                         + $"{App.Save.EquippedModules.Count}/{p.ModuleSlots} slots equipped"));

        foreach (var def in App.Cfg.Modules.Modules)
            _body.AddChild(ModuleRow(def, p.ModuleLevel(def.Id), p.IsModuleEquipped(def.Id)));
    }

    private Control ModuleRow(Config.ModuleDef def, int lvl, bool equipped)
    {
        int tier = Sentinel.Meta.Progression.ModuleTier(lvl);
        var accent = TierColor(tier);

        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, equipped ? 0.13f : 0.05f),
            BorderColor = new Color(accent, equipped ? 0.85f : 0.4f),
            BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10,
        });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        p.AddChild(row);

        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 3);
        row.AddChild(col);

        var top = new Label { Text = $"{(tier == 0 ? "—" : $"◈ T{tier}")}   {def.Name.ToUpperInvariant()}   ·   {(lvl > 0 ? $"Lv.{lvl}" : "not installed")}" };
        top.AddThemeFontOverride("font", UiTheme.Display);
        top.AddThemeFontSizeOverride("font_size", 20);
        top.AddThemeColorOverride("font_color", accent.Lightened(0.28f));
        col.AddChild(top);

        var tx = new Label { Text = def.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.65f) };
        tx.AddThemeFontSizeOverride("font_size", 15);
        col.AddChild(tx);

        double cost = App.Prog.ModuleCost(def);
        bool canBuy = cost >= 0 && App.Save.ResearchData >= cost;
        var up = new Button
        {
            Text = cost < 0 ? "MAX" : $"◇ {F(cost)}",
            CustomMinimumSize = new Vector2(126, 62), Disabled = !canBuy,
        };
        up.AddThemeFontSizeOverride("font_size", 17);
        if (canBuy) UiTheme.StylePrimary(up);
        up.Pressed += () =>
        {
            if (App.Prog.BuyModule(def))
            {
                Sentinel.Audio.AudioManager.Instance?.Confirm();
                Rebuild();
            }
        };
        row.AddChild(up);

        var eq = new Button
        {
            Text = equipped ? "Unequip" : "Equip",
            CustomMinimumSize = new Vector2(126, 62),
            Disabled = lvl <= 0 || (!equipped && App.Save.EquippedModules.Count >= App.Prog.ModuleSlots),
        };
        eq.AddThemeFontSizeOverride("font_size", 17);
        eq.Pressed += () =>
        {
            if (equipped) App.Save.EquippedModules.Remove(def.Id);
            else App.Save.EquippedModules.Add(def.Id);
            App.Save.Save();
            Sentinel.Audio.AudioManager.Instance?.Click();
            Rebuild();
        };
        row.AddChild(eq);
        return p;
    }

    // ------------------------------------------------------- shield / ship / research ----

    private void BuildShield()
    {
        _body.AddChild(Dim("The Planet Shield soaks damage before the crust does. Levels are permanent."));
        Jump("Open Sentinels & Planet Shield", () => App.ShowSentinels());
    }

    private void BuildShip()
    {
        _body.AddChild(Dim("The commander's hull, its weapons, and the protocols it carries."));
        Jump("Open Sentinels & ship weapons", () => App.ShowSentinels());
    }

    private void BuildResearch()
    {
        _body.AddChild(Dim("Permanent account-wide upgrades, bought with Research Data."));
        Jump("Open the Research tree", () => App.ShowResearch());
    }

    private static Label Header(string text)
    {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", UiTheme.Display);
        l.AddThemeFontSizeOverride("font_size", 20);
        l.AddThemeColorOverride("font_color", UiTheme.Accent);
        return l;
    }

    private static Label Dim(string text)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.55f) };
        l.AddThemeFontSizeOverride("font_size", 16);
        return l;
    }

    /// <summary>Currency strip, drawn with the real loot sprites.</summary>
    private void BuildWallet()
    {
        foreach (Node c in _walletBar.GetChildren()) c.QueueFree();
        var s = App.Save;
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        row.AddThemeConstantOverride("separation", 16);
        _walletBar.AddChild(row);

        void Cell(string sprite, string text, Color tint)
        {
            var c = new HBoxContainer();
            c.AddThemeConstantOverride("separation", 6);
            var tex = Render.Art.Pdtd(sprite);
            if (tex != null)
                c.AddChild(new TextureRect
                {
                    Texture = tex, CustomMinimumSize = new Vector2(32, 32),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                });
            var l = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
            l.AddThemeFontSizeOverride("font_size", 19);
            l.AddThemeColorOverride("font_color", tint);
            c.AddChild(l);
            row.AddChild(c);
        }

        Cell("loot/research_core", F(s.ResearchData), UiTheme.Accent);
        Cell("loot/techpoint", $"{s.UltimateAlloy}", new Color(0.45f, 0.9f, 0.95f));
        Cell("loot/crystal", $"{s.UnobtainiumAlloy}", new Color(1f, 0.45f, 0.55f));
        Cell("loot/gold_key", $"{s.GoldKeys}", new Color(0.95f, 0.8f, 0.3f));
    }

    /// <summary>A stand-in body for the categories that still open a dedicated screen.
    /// Keeping them reachable from the new layout matters more than inlining every one
    /// of them at once — the sections below are the ones PDTD's reference actually
    /// shows in place.</summary>
    private void Jump(string label, System.Action go)
    {
        var b = new Button { Text = label, CustomMinimumSize = new Vector2(0, 92) };
        b.AddThemeFontSizeOverride("font_size", 21);
        UiTheme.StylePrimary(b);
        b.Pressed += () => { Sentinel.Audio.AudioManager.Instance?.Confirm(); go(); };
        _body.AddChild(b);
    }

    // ---------------------------------------------------------------- Ultimate ----

    private void BuildUltimate()
    {
        var cfg = App.Cfg;
        var save = App.Save;

        foreach (var w in cfg.OrbitalWeapons)
        {
            if (w.UltimateChargeSeconds <= 0f) continue;

            var panel = new PanelContainer();
            var accent = HexColor(w.Accent, UiTheme.Accent);
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(accent, 0.08f), BorderColor = new Color(accent, 0.5f),
                BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
                CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
                ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 12, ContentMarginBottom = 12,
            });
            _body.AddChild(panel);

            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 8);
            panel.AddChild(col);

            // header: the weapon's tile art, name, and what its ultimate does
            var head = new HBoxContainer();
            head.AddThemeConstantOverride("separation", 12);
            col.AddChild(head);

            var art = Render.Art.Pdtd("tiles/" + w.Kind);
            if (art != null)
                head.AddChild(new TextureRect
                {
                    Texture = art, CustomMinimumSize = new Vector2(96, 96),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    ClipContents = true,
                });

            var hc = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            head.AddChild(hc);
            var nm = new Label { Text = w.Name.ToUpperInvariant() };
            nm.AddThemeFontOverride("font", UiTheme.Display);
            nm.AddThemeFontSizeOverride("font_size", 23);
            nm.AddThemeColorOverride("font_color", accent.Lightened(0.25f));
            hc.AddChild(nm);

            float dur = w.UltimateDuration;
            var desc = new Label
            {
                Text = $"Charges in {w.UltimateChargeSeconds:0}s, then fires at +{w.UltimateDamageBonus * 100f:0}% DMG "
                     + $"and {w.UltimateRateMult:0.#}× rate for {dur:0}s.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(1, 1, 1, 0.68f),
            };
            desc.AddThemeFontSizeOverride("font_size", 16);
            hc.AddChild(desc);

            // the four tracks, side by side like PDTD's
            var tracks = new HBoxContainer();
            tracks.AddThemeConstantOverride("separation", 8);
            col.AddChild(tracks);
            foreach (var t in cfg.Ultimates.Tracks)
                tracks.AddChild(TrackCard(w.Id, t));
        }

        if (_body.GetChildCount() == 0)
        {
            var none = new Label
            {
                Text = "No sentinel has an ultimate configured yet.",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(1, 1, 1, 0.5f),
            };
            _body.AddChild(none);
        }
    }

    /// <summary>One upgrade track column: level, current bonus, next step, and the
    /// alloy price — the shape of PDTD's four Ultimate Upgrade cards.</summary>
    private Control TrackCard(string weaponId, Config.UltimateTrackDef t)
    {
        var cfg = App.Cfg.Ultimates;
        var save = App.Save;
        string key = weaponId + ":" + t.Id;
        int lvl = save.UltimateLevels.GetValueOrDefault(key);
        bool maxed = lvl >= cfg.MaxLevel;
        int cost = CostAt(t, lvl);
        bool needsUnob = lvl + 1 >= cfg.UnobtainiumFrom;
        bool afford = !maxed && save.UltimateAlloy >= cost
                      && (!needsUnob || save.UnobtainiumAlloy >= cfg.UnobtainiumCost);

        var p = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.14f, 0.20f, 0.85f),
            BorderColor = new Color(0.45f, 0.62f, 0.8f, maxed ? 0.35f : 0.6f),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 8, ContentMarginBottom = 8,
        });
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 4);
        p.AddChild(col);

        var lv = new Label { Text = $"Lv.{lvl}", HorizontalAlignment = HorizontalAlignment.Center };
        lv.AddThemeFontOverride("font", UiTheme.Display);
        lv.AddThemeFontSizeOverride("font_size", 20);
        col.AddChild(lv);

        var nm = new Label
        {
            Text = t.Name, HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        nm.AddThemeFontSizeOverride("font_size", 15);
        nm.Modulate = new Color(1, 1, 1, 0.8f);
        col.AddChild(nm);

        var cur = new Label
        {
            Text = $"+{t.PerLevel * lvl * 100f:0}%", HorizontalAlignment = HorizontalAlignment.Center,
        };
        cur.AddThemeFontOverride("font", UiTheme.Display);
        cur.AddThemeFontSizeOverride("font_size", 22);
        col.AddChild(cur);

        var step = new Label
        {
            Text = maxed ? "MAX" : $"+{t.PerLevel * 100f:0.#}%",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        step.AddThemeFontSizeOverride("font_size", 16);
        step.AddThemeColorOverride("font_color", new Color(0.45f, 0.95f, 0.6f));
        col.AddChild(step);

        var buy = new Button
        {
            Text = maxed ? "—" : (needsUnob ? $"{cost} + 1◆" : $"{cost}"),
            CustomMinimumSize = new Vector2(0, 54),
            Disabled = !afford,
        };
        buy.AddThemeFontSizeOverride("font_size", 16);
        if (afford) UiTheme.StylePrimary(buy);
        buy.TooltipText = t.Text + (needsUnob ? "\nAlso costs Unobtainium Alloy at this depth." : "");
        buy.Pressed += () =>
        {
            save.UltimateAlloy -= cost;
            if (needsUnob) save.UnobtainiumAlloy -= cfg.UnobtainiumCost;
            save.UltimateLevels[key] = lvl + 1;
            save.Save();
            Sentinel.Audio.AudioManager.Instance?.Confirm();
            Rebuild();
        };
        col.AddChild(buy);
        return p;
    }

    private int CostAt(Config.UltimateTrackDef t, int lvl)
        => Mathf.Max(1, Mathf.RoundToInt(t.Cost * Mathf.Pow(App.Cfg.Ultimates.CostGrowth, lvl)));

    // ------------------------------------------------------------------- Skins ----

    private void BuildSkins()
    {
        var shop = App.Shop;
        foreach (var it in App.Cfg.Shop.Items)
        {
            if (it.Tab != "worlds") continue;
            bool owned = shop.Owned(it.Id);
            bool equipped = shop.Equipped(it);
            string planetId = it.Apply.StartsWith("planet:") ? it.Apply["planet:".Length..] : "earth";

            var p = new PanelContainer();
            p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(UiTheme.Accent, equipped ? 0.12f : 0.05f),
                BorderColor = new Color(UiTheme.Accent, equipped ? 0.8f : 0.35f),
                BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
                CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
                ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 12, ContentMarginBottom = 12,
            });
            _body.AddChild(p);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 14);
            p.AddChild(row);

            // live rotating globe, same renderer the mission uses
            var stage = new Control { CustomMinimumSize = new Vector2(120, 120) };
            row.AddChild(stage);
            var globe = new Render.PlanetView { Skin = planetId, Diameter = 110f };
            stage.AddChild(globe);
            globe.Position = new Vector2(60, 60);
            stage.Resized += () => globe.Position = stage.Size * 0.5f;

            var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            col.AddThemeConstantOverride("separation", 4);
            row.AddChild(col);
            var nm = new Label { Text = it.Name.ToUpperInvariant() };
            nm.AddThemeFontOverride("font", UiTheme.Display);
            nm.AddThemeFontSizeOverride("font_size", 23);
            nm.AddThemeColorOverride("font_color", UiTheme.Accent);
            col.AddChild(nm);
            var ds = new Label { Text = it.Desc, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.66f) };
            ds.AddThemeFontSizeOverride("font_size", 16);
            col.AddChild(ds);

            var act = new Button { CustomMinimumSize = new Vector2(170, 66) };
            act.AddThemeFontSizeOverride("font_size", 18);
            var itc = it;
            if (equipped) { act.Text = "Deployed"; act.Disabled = true; }
            else if (owned) { act.Text = "Deploy"; UiTheme.StylePrimary(act); act.Pressed += () => { App.Shop.Equip(itc); Rebuild(); }; }
            else if (shop.CanBuy(it)) { act.Text = $"✦ {it.Cost}"; UiTheme.StylePrimary(act); act.Pressed += () => { if (App.Shop.Buy(itc)) Rebuild(); }; }
            else { act.Text = $"✦ {it.Cost}"; act.Disabled = true; }
            row.AddChild(act);
        }
    }

    // ------------------------------------------------------------------- Items ----

    private void BuildItems()
    {
        var s = App.Save;
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        _body.AddChild(grid);

        void Item(string sprite, string name, string count)
        {
            var p = new PanelContainer();
            p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.10f, 0.14f, 0.20f, 0.85f),
                BorderColor = new Color(0.45f, 0.62f, 0.8f, 0.45f),
                BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
                ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 8, ContentMarginBottom = 8,
            });
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 2);
            p.AddChild(col);
            var tex = Render.Art.Pdtd(sprite);
            if (tex != null)
                col.AddChild(new TextureRect
                {
                    Texture = tex, CustomMinimumSize = new Vector2(0, 62),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                });
            var c = new Label { Text = count, HorizontalAlignment = HorizontalAlignment.Center };
            c.AddThemeFontOverride("font", UiTheme.Display);
            c.AddThemeFontSizeOverride("font_size", 20);
            col.AddChild(c);
            var n = new Label { Text = name, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            n.AddThemeFontSizeOverride("font_size", 13);
            n.Modulate = new Color(1, 1, 1, 0.6f);
            col.AddChild(n);
            grid.AddChild(p);
        }

        Item("loot/silver_key", "Silver Key", $"{s.SilverKeys}");
        Item("loot/gold_key", "Gold Key", $"{s.GoldKeys}");
        Item("loot/techpoint", "Ultimate Alloy", $"{s.UltimateAlloy}");
        Item("loot/crystal", "Unobtainium", $"{s.UnobtainiumAlloy}");
        Item("loot/research_core", "Research Data", F(s.ResearchData));
        Item("loot/shield_capacitor", "Sentinel Cores", $"{s.SentinelCores}");
        Item("loot/rebuild_core", "Exotic Alloy", F(s.ExoticAlloy));
        Item("loot/medal", "Commendations", $"{App.Shop.Balance}");
    }

    private static Color HexColor(string hex, Color fallback)
    {
        try { return Color.FromHtml(hex); } catch { return fallback; }
    }

    private static string F(double v) => v >= 1000 ? $"{v / 1000.0:0.#}k" : $"{v:0}";
}
