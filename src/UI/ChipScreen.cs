using Godot;
using Sentinel.Config;
using Sentinel.Game;

namespace Sentinel.UI;

/// <summary>
/// ARMORY — open Silver/Gold Key chests for chips, then equip up to
/// <see cref="ChipsDb.EquipSlots"/> of them (each boosts every sentinel/planet/hero stat
/// its <c>EffectKey</c> names, scaled by tier) and merge duplicates up to a higher tier.
/// Keys are earned only by playing (see AppRoot.OnMissionEnded) — CLAUDE.md's 2026-09-16
/// amendment: no real money in this loop, ever, only in-game currency.
/// </summary>
public sealed partial class ChipScreen : CanvasLayer
{
    public AppRoot App = null!;

    private Control _keyBar = null!;
    private HBoxContainer _slots = null!;
    private VBoxContainer _list = null!;

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.5f, PlanetScale = 0.55f, NebulaAlpha = 0.10f });

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -500, OffsetRight = 500, OffsetTop = 34, OffsetBottom = -12,
        };
        root.AddThemeConstantOverride("separation", 10);
        root.Theme = UiTheme.Instance;
        AddChild(MenuFrame.Around(root));
        AddChild(root);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(210, 84) };
        back.Pressed += () => App.ShowMenu();
        head.AddChild(back);
        var title = new Label { Text = "  ARMORY", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontOverride("font", UiTheme.Display);
        title.AddThemeFontSizeOverride("font_size", 34);
        head.AddChild(title);

        _keyBar = new VBoxContainer();
        root.AddChild(_keyBar);

        _slots = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _slots.AddThemeConstantOverride("separation", 8);
        root.AddChild(_slots);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_list);

        Rebuild();
    }

    /// <summary>The key counters, drawn with PDTD's own key sprites rather than the emoji
    /// the first pass used — 🔑 rendered as a gold key next to the *silver* count and 🔶 as
    /// an orange diamond next to the gold one, so both read as the wrong currency.</summary>
    private void BuildKeyBar(Control parent)
    {
        foreach (Node c in parent.GetChildren()) c.QueueFree();
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 26);
        parent.AddChild(row);

        void Key(string sprite, int n, Color tint)
        {
            var cell = new HBoxContainer();
            cell.AddThemeConstantOverride("separation", 8);
            var tex = Render.Art.Pdtd("loot/" + sprite);
            if (tex != null)
            {
                cell.AddChild(new TextureRect
                {
                    Texture = tex,
                    CustomMinimumSize = new Vector2(44, 44),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                });
            }
            var l = new Label { Text = n.ToString(), VerticalAlignment = VerticalAlignment.Center };
            l.AddThemeFontOverride("font", UiTheme.Display);
            l.AddThemeFontSizeOverride("font_size", 26);
            l.AddThemeColorOverride("font_color", tint);
            cell.AddChild(l);
            row.AddChild(cell);
        }

        Key("silver_key", App.Save.SilverKeys, new Color(0.80f, 0.86f, 0.95f));
        Key("gold_key", App.Save.GoldKeys, new Color(0.98f, 0.82f, 0.32f));

        var note = new Label { Text = "earned by playing — never for sale", VerticalAlignment = VerticalAlignment.Center };
        note.AddThemeFontSizeOverride("font_size", 15);
        note.Modulate = new Color(1, 1, 1, 0.45f);
        row.AddChild(note);
    }

    private void Rebuild()
    {
        var vault = App.Chips;
        BuildKeyBar(_keyBar);

        // ---- equipped slots ----
        foreach (Node c in _slots.GetChildren()) c.QueueFree();
        int slotCount = App.Cfg.Chips.EquipSlots;
        for (int i = 0; i < slotCount; i++)
        {
            string? k = i < App.Save.EquippedChips.Count ? App.Save.EquippedChips[i] : null;
            var b = new Button { CustomMinimumSize = new Vector2(105, 56) };
            b.AddThemeFontSizeOverride("font_size", 15);
            if (k != null)
            {
                var parts = k.Split(':');
                var def = App.Cfg.Chips.Chips.Find(x => x.Id == parts[0]);
                int tier = int.Parse(parts[1]);
                b.Text = $"{IconGlyph(def?.Icon ?? "")} T{tier}";
                b.TooltipText = $"{def?.Name}\ntap to unequip";
                b.Pressed += () => { vault.Unequip(parts[0], tier); Click(); Rebuild(); };
            }
            else
            {
                b.Text = "— empty —";
                b.Disabled = true;
                b.Modulate = new Color(1, 1, 1, 0.4f);
            }
            _slots.AddChild(b);
        }

        foreach (Node c in _list.GetChildren()) c.QueueFree();

        // ---- chests: PDTD's shop lays these out side by side as big art cards ----
        _list.AddChild(SectionHeader("SHOP"));
        var chestRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        chestRow.AddThemeConstantOverride("separation", 16);
        _list.AddChild(chestRow);
        chestRow.AddChild(ChestCard("Silver Chest", "silver", "chest_silver", "silver_key",
            App.Save.SilverKeys, new Color(0.68f, 0.74f, 0.86f)));
        chestRow.AddChild(ChestCard("Gold Chest", "gold", "chest_gold", "gold_key",
            App.Save.GoldKeys, new Color(0.95f, 0.78f, 0.25f)));

        // ---- chips ----
        var mergeRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _list.AddChild(mergeRow);
        var mergeHdr = SectionHeader("CHIPS");
        mergeHdr.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        mergeRow.AddChild(mergeHdr);
        var quickMerge = new Button { Text = "⚡ Quick Merge", CustomMinimumSize = new Vector2(196, 63) };
        quickMerge.AddThemeFontSizeOverride("font_size", 18);
        quickMerge.Pressed += () => { int n = vault.QuickMergeAll(); Click(); Rebuild(); ShowToast(n > 0 ? $"Fused {n}×" : "Nothing to merge"); };
        mergeRow.AddChild(quickMerge);

        // Per-tier fuse bar — merging pools every archetype of a tier and returns a
        // random chip one tier up, so the action belongs to the tier, not to one chip.
        foreach (var td in vault.Tiers)
        {
            int held = vault.TierCount(td.Tier);
            if (held <= 0 || td.MergeCount <= 0) continue;
            _list.AddChild(FuseRow(td, held));
        }

        bool anyOwned = false;
        foreach (var def in vault.Archetypes)
        {
            foreach (var td in vault.Tiers)
            {
                int count = vault.Count(def.Id, td.Tier);
                if (count <= 0) continue;
                anyOwned = true;
                _list.AddChild(ChipCard(def, td, count));
            }
        }
        if (!anyOwned)
        {
            var none = new Label
            {
                Text = "No chips yet — open a chest above.",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(1, 1, 1, 0.5f),
            };
            none.AddThemeFontSizeOverride("font_size", 18);
            _list.AddChild(none);
        }
    }

    private static Label SectionHeader(string text)
    {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", UiTheme.Display);
        l.AddThemeFontSizeOverride("font_size", 21);
        l.AddThemeColorOverride("font_color", UiTheme.Accent);
        return l;
    }

    /// <summary>A shop chest: PDTD stacks the art over the name over the Open buttons,
    /// with the key cost shown as the key sprite rather than a word.</summary>
    private Control ChestCard(string name, string kind, string art, string keySprite, int keys, Color accent)
    {
        var p = new PanelContainer { CustomMinimumSize = new Vector2(300, 0) };
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, 0.09f), BorderColor = new Color(accent, 0.55f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 12, ContentMarginBottom = 12,
        });
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        p.AddChild(col);

        var tex = Render.Art.Pdtd("chest/" + art);
        if (tex != null)
        {
            col.AddChild(new TextureRect
            {
                Texture = tex,
                CustomMinimumSize = new Vector2(0, 132),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });
        }

        var nm = new Label { Text = name.ToUpperInvariant(), HorizontalAlignment = HorizontalAlignment.Center };
        nm.AddThemeFontOverride("font", UiTheme.Display);
        nm.AddThemeFontSizeOverride("font_size", 22);
        nm.AddThemeColorOverride("font_color", accent.Lightened(0.25f));
        col.AddChild(nm);

        var cost = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        cost.AddThemeConstantOverride("separation", 6);
        col.AddChild(cost);
        var ktex = Render.Art.Pdtd("loot/" + keySprite);
        if (ktex != null)
        {
            cost.AddChild(new TextureRect
            {
                Texture = ktex, CustomMinimumSize = new Vector2(26, 26),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });
        }
        var have = new Label { Text = $"1 per chest   ·   you have {keys}", VerticalAlignment = VerticalAlignment.Center };
        have.AddThemeFontSizeOverride("font_size", 15);
        have.Modulate = new Color(1, 1, 1, 0.62f);
        cost.AddChild(have);

        var btns = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btns.AddThemeConstantOverride("separation", 10);
        col.AddChild(btns);

        var open1 = new Button { Text = "Open 1", CustomMinimumSize = new Vector2(122, 66), Disabled = keys < 1 };
        open1.AddThemeFontSizeOverride("font_size", 18);
        open1.Pressed += () => { var got = App.Chips.OpenChests(kind, 1); Click(); Rebuild(); ShowDrops(got); };
        btns.AddChild(open1);

        var open10 = new Button { Text = "Open 10", CustomMinimumSize = new Vector2(122, 66), Disabled = keys < 10 };
        open10.AddThemeFontSizeOverride("font_size", 18);
        open10.Pressed += () => { var got = App.Chips.OpenChests(kind, 10); Click(); Rebuild(); ShowDrops(got); };
        btns.AddChild(open10);

        return p;
    }

    /// <summary>"Fuse 3 Common → 1 random Fine" — one row per tier you hold enough of.</summary>
    private Control FuseRow(ChipTierDef td, int held)
    {
        var accent = Color.FromHtml(td.Color);
        var next = App.Chips.TierDef(td.Tier + 1);
        bool can = held >= td.MergeCount;

        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, can ? 0.10f : 0.04f), BorderColor = new Color(accent, can ? 0.55f : 0.22f),
            BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10,
        });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        p.AddChild(row);

        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(col);
        var nm = new Label { Text = $"FUSE {td.MergeCount}× {td.Name.ToUpperInvariant()}   →   1 RANDOM {next?.Name.ToUpperInvariant() ?? "?"}" };
        nm.AddThemeFontOverride("font", UiTheme.Display);
        nm.AddThemeFontSizeOverride("font_size", 19);
        nm.AddThemeColorOverride("font_color", accent.Lightened(0.25f));
        col.AddChild(nm);
        var tx = new Label
        {
            Text = $"you hold {held} — any archetypes, the result is rolled",
            Modulate = new Color(1, 1, 1, 0.6f),
        };
        tx.AddThemeFontSizeOverride("font_size", 15);
        col.AddChild(tx);

        var b = new Button { Text = "Fuse", CustomMinimumSize = new Vector2(150, 62), Disabled = !can };
        b.AddThemeFontSizeOverride("font_size", 18);
        b.Pressed += () =>
        {
            string? got = App.Chips.MergeTier(td.Tier);
            App.Save.Save();
            Click(); Rebuild();
            if (got != null)
            {
                var def = App.Cfg.Chips.Chips.Find(x => x.Id == got);
                ShowToast($"Fused → {def?.Name ?? got} ({next?.Name})");
            }
        };
        row.AddChild(b);
        return p;
    }

    private Control ChipCard(ChipDef def, ChipTierDef td, int count)
    {
        var accent = Color.FromHtml(td.Color);
        bool equipped = App.Chips.IsEquipped(def.Id, td.Tier);
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, equipped ? 0.14f : 0.07f), BorderColor = new Color(accent, equipped ? 0.9f : 0.5f),
            BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 12, ContentMarginBottom = 12,
        });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        p.AddChild(row);

        row.AddChild(ChipPlate(def, td, 64));

        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 3);
        row.AddChild(col);
        var nm = new Label { Text = $"{def.Name.ToUpperInvariant()}    ·    {td.Name}    ·    ×{count}" + (equipped ? "   ✓ EQUIPPED" : "") };
        nm.AddThemeFontOverride("font", UiTheme.Display);
        nm.AddThemeFontSizeOverride("font_size", 21);
        nm.AddThemeColorOverride("font_color", accent.Lightened(0.25f));
        col.AddChild(nm);
        var tx = new Label { Text = def.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.7f) };
        tx.AddThemeFontSizeOverride("font_size", 17);
        col.AddChild(tx);

        var btnCol = new VBoxContainer();
        btnCol.AddThemeConstantOverride("separation", 6);
        row.AddChild(btnCol);

        // No per-chip merge button any more: fusing pools a whole tier and returns a
        // random archetype, so it lives on the FuseRow above rather than on each card.

        var equipBtn = new Button
        {
            Text = equipped ? "Unequip" : "Equip",
            CustomMinimumSize = new Vector2(154, 63),
            Disabled = !equipped && App.Save.EquippedChips.Count >= App.Cfg.Chips.EquipSlots,
        };
        equipBtn.AddThemeFontSizeOverride("font_size", 17);
        equipBtn.Pressed += () =>
        {
            if (equipped) App.Chips.Unequip(def.Id, td.Tier); else App.Chips.Equip(def.Id, td.Tier);
            Click(); Rebuild();
        };
        btnCol.AddChild(equipBtn);

        return p;
    }

    /// <summary>A chip as PDTD draws it: the tier's rarity-coloured plate with the chip's
    /// own glyph on top. Falls back to the plain glyph, then to a text symbol, so a
    /// missing sprite degrades instead of blanking the row.</summary>
    private static Control ChipPlate(ChipDef def, ChipTierDef td, int size)
    {
        var holder = new Control { CustomMinimumSize = new Vector2(size, size) };

        var plate = string.IsNullOrEmpty(td.Plate) ? null : Render.Art.Pdtd("chip/" + td.Plate);
        if (plate != null)
        {
            var pr = new TextureRect
            {
                Texture = plate,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            pr.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            holder.AddChild(pr);
        }

        var glyph = string.IsNullOrEmpty(def.Glyph) ? null : Render.Art.Pdtd("chip/" + def.Glyph);
        if (glyph != null)
        {
            // inset so the glyph sits inside the plate's bezel rather than over it
            var gr = new TextureRect
            {
                Texture = glyph,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = new Color(1, 1, 1, 0.92f),
                AnchorLeft = 0.24f, AnchorRight = 0.76f,
                AnchorTop = 0.20f, AnchorBottom = 0.72f,
            };
            holder.AddChild(gr);
        }
        else if (plate == null)
        {
            var l = new Label
            {
                Text = IconGlyph(def.Icon),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            l.AddThemeFontSizeOverride("font_size", (int)(size * 0.5f));
            l.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            holder.AddChild(l);
        }
        return holder;
    }

    private static string IconGlyph(string icon) => icon switch
    {
        "damage" => "⚔",
        "cooldown" => "❄",
        "radius" => "◎",
        "shield" => "🛡",
        "hull" => "⛨",
        "salvage" => "◇",
        _ => "◆",
    };

    private void ShowDrops(System.Collections.Generic.List<(string chipId, int tier)> got)
    {
        if (got.Count == 0) return;
        var parts = new System.Collections.Generic.List<string>();
        foreach (var (chipId, tier) in got)
        {
            var def = App.Cfg.Chips.Chips.Find(x => x.Id == chipId);
            parts.Add($"{IconGlyph(def?.Icon ?? "")} {def?.Name ?? chipId} T{tier}");
        }
        ShowToast("Got: " + string.Join("   ", parts));
    }

    private void ShowToast(string text)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.7f) };
        l.AddThemeFontSizeOverride("font_size", 15);
        _list.AddChild(l);
        _list.MoveChild(l, 0);
    }

    private static void Click() => Sentinel.Audio.AudioManager.Instance?.Click();
}
