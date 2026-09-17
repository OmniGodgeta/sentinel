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

    private Label _keys = null!;
    private HBoxContainer _slots = null!;
    private VBoxContainer _list = null!;

    public override void _Ready()
    {
        Layer = 6;
        AddChild(new MenuBackground { PlanetY = 0.5f, PlanetScale = 0.55f, NebulaAlpha = 0.10f });

        var root = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -320, OffsetRight = 320, OffsetTop = 34, OffsetBottom = -12,
        };
        root.AddThemeConstantOverride("separation", 10);
        root.Theme = UiTheme.Instance;
        AddChild(root);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        root.AddChild(head);
        var back = new Button { Text = "‹ Back", CustomMinimumSize = new Vector2(300, 120) };
        back.Pressed += () => App.ShowMenu();
        head.AddChild(back);
        var title = new Label { Text = "  ARMORY", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontOverride("font", UiTheme.Display);
        title.AddThemeFontSizeOverride("font_size", 48);
        head.AddChild(title);

        _keys = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _keys.AddThemeFontSizeOverride("font_size", 30);
        _keys.AddThemeColorOverride("font_color", UiTheme.Accent2);
        root.AddChild(_keys);

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

    private void Rebuild()
    {
        var vault = App.Chips;
        _keys.Text = $"🔑 Silver {App.Save.SilverKeys}      🔶 Gold {App.Save.GoldKeys}      —      earned by playing, never for sale";

        // ---- equipped slots ----
        foreach (Node c in _slots.GetChildren()) c.QueueFree();
        int slotCount = App.Cfg.Chips.EquipSlots;
        for (int i = 0; i < slotCount; i++)
        {
            string? k = i < App.Save.EquippedChips.Count ? App.Save.EquippedChips[i] : null;
            var b = new Button { CustomMinimumSize = new Vector2(150, 80) };
            b.AddThemeFontSizeOverride("font_size", 22);
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

        // ---- chests ----
        _list.AddChild(SectionHeader("OPEN CHESTS"));
        _list.AddChild(ChestCard("Silver Chest", "silver", App.Save.SilverKeys, new Color(0.68f, 0.72f, 0.8f)));
        _list.AddChild(ChestCard("Gold Chest", "gold", App.Save.GoldKeys, new Color(0.95f, 0.78f, 0.25f)));

        // ---- chips ----
        var mergeRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _list.AddChild(mergeRow);
        var mergeHdr = SectionHeader("CHIPS");
        mergeHdr.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        mergeRow.AddChild(mergeHdr);
        var quickMerge = new Button { Text = "⚡ Quick Merge", CustomMinimumSize = new Vector2(280, 90) };
        quickMerge.AddThemeFontSizeOverride("font_size", 26);
        quickMerge.Pressed += () => { int n = vault.QuickMergeAll(); Click(); Rebuild(); ShowToast(n > 0 ? $"Merged {n}×" : "Nothing to merge"); };
        mergeRow.AddChild(quickMerge);

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
            none.AddThemeFontSizeOverride("font_size", 26);
            _list.AddChild(none);
        }
    }

    private static Label SectionHeader(string text)
    {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", UiTheme.Display);
        l.AddThemeFontSizeOverride("font_size", 30);
        l.AddThemeColorOverride("font_color", UiTheme.Accent);
        return l;
    }

    private Control ChestCard(string name, string kind, int keys, Color accent)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(accent, 0.10f), BorderColor = new Color(accent, 0.6f),
            BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 12, ContentMarginBottom = 12,
        });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        p.AddChild(row);

        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(col);
        var nm = new Label { Text = $"{name}    ·    have {keys}" };
        nm.AddThemeFontOverride("font", UiTheme.Display);
        nm.AddThemeFontSizeOverride("font_size", 30);
        nm.AddThemeColorOverride("font_color", accent.Lightened(0.2f));
        col.AddChild(nm);
        var tx = new Label { Text = "1 key per chest — always drops one chip.", Modulate = new Color(1, 1, 1, 0.6f) };
        tx.AddThemeFontSizeOverride("font_size", 22);
        col.AddChild(tx);

        var open1 = new Button { Text = "Open 1", CustomMinimumSize = new Vector2(180, 100), Disabled = keys < 1 };
        open1.AddThemeFontSizeOverride("font_size", 26);
        open1.Pressed += () => { var got = App.Chips.OpenChests(kind, 1); Click(); Rebuild(); ShowDrops(got); };
        row.AddChild(open1);

        var open5 = new Button { Text = "Open 5", CustomMinimumSize = new Vector2(180, 100), Disabled = keys < 5 };
        open5.AddThemeFontSizeOverride("font_size", 26);
        open5.Pressed += () => { var got = App.Chips.OpenChests(kind, 5); Click(); Rebuild(); ShowDrops(got); };
        row.AddChild(open5);

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

        var glyph = new Label { Text = IconGlyph(def.Icon), VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(60, 0) };
        glyph.AddThemeFontSizeOverride("font_size", 40);
        row.AddChild(glyph);

        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 3);
        row.AddChild(col);
        var nm = new Label { Text = $"{def.Name.ToUpperInvariant()}    ·    {td.Name}    ·    ×{count}" + (equipped ? "   ✓ EQUIPPED" : "") };
        nm.AddThemeFontOverride("font", UiTheme.Display);
        nm.AddThemeFontSizeOverride("font_size", 30);
        nm.AddThemeColorOverride("font_color", accent.Lightened(0.25f));
        col.AddChild(nm);
        var tx = new Label { Text = def.Text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.7f) };
        tx.AddThemeFontSizeOverride("font_size", 24);
        col.AddChild(tx);

        var btnCol = new VBoxContainer();
        btnCol.AddThemeConstantOverride("separation", 6);
        row.AddChild(btnCol);

        if (App.Chips.CanMerge(def.Id, td.Tier))
        {
            var merge = new Button { Text = $"Merge ×{td.MergeCost}", CustomMinimumSize = new Vector2(220, 90) };
            merge.AddThemeFontSizeOverride("font_size", 24);
            merge.Pressed += () => { App.Chips.Merge(def.Id, td.Tier); Click(); Rebuild(); };
            btnCol.AddChild(merge);
        }

        var equipBtn = new Button
        {
            Text = equipped ? "Unequip" : "Equip",
            CustomMinimumSize = new Vector2(220, 90),
            Disabled = !equipped && App.Save.EquippedChips.Count >= App.Cfg.Chips.EquipSlots,
        };
        equipBtn.AddThemeFontSizeOverride("font_size", 24);
        equipBtn.Pressed += () =>
        {
            if (equipped) App.Chips.Unequip(def.Id, td.Tier); else App.Chips.Equip(def.Id, td.Tier);
            Click(); Rebuild();
        };
        btnCol.AddChild(equipBtn);

        return p;
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
        l.AddThemeFontSizeOverride("font_size", 22);
        _list.AddChild(l);
        _list.MoveChild(l, 0);
    }

    private static void Click() => Sentinel.Audio.AudioManager.Instance?.Click();
}
