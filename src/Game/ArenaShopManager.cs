using Godot;
using System;
using Sentinel.Render;
using Sentinel.Sim;

namespace Sentinel.Game;

/// <summary>
/// Arena intermission shop — lists the last four turrets in active-use, shows
/// each one's upgrade button if affordable, and triggers <see cref="SimWorld.TryUpgradeArenaTurret"/>
/// when tapped. Built once in GameRoot and reused each time the intermission
/// overlay appears.
/// </summary>
public sealed partial class ArenaShopManager : CanvasLayer
{
    public SimWorld World = null!;
    
    // the upgrade buttons for the last four turrets (or fewer if fewer exist or some aren't upgradeable)
    private VBoxContainer? _buttonList;
    private Button?[]? _upgradeButtons = null; // sparse array indexed by turret slot (or -1)
    
    public override void _Ready()
    {
        Layer = 2; // below GameRoot UI, above scene
    }

    /// <summary>
    /// Initialize with world and the target parent for our shop UI.
    /// </summary>
    /// <param name="world">The running simulation — provides turret view & AddGold.</param>
    /// <param name="parent">VBoxContainer that will own our shop elements.</param>
    public void Initialize(SimWorld world, VBoxContainer parent)
    {
        World = world;
        _buttonList?.QueueFree();
        _buttonList = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -180, OffsetRight = 180, OffsetTop = 42, OffsetBottom = -52,
            OffsetBottom = -52,
            Alignment = new Vector2(0, 0.5f),
        };
        _buttonList.AddThemeConstantOverride("separation", 12);
        _buttonList.Theme = UiTheme.Instance;
        parent.AddChild(_buttonList);

        RefreshShop();
    }

    /// <summary>
    /// Rebuilds the shop UI to reflect new turret state.
    /// Called each frame during intermission but we debounce via visible check.
    /// </summary>
    public void RefreshShop()
    {
        var turrets = World.TurretView;
        var upgradeable = new (string Name, string DefName, int Cost, string? Icon)[]?;

        // Gather the last four turrets that exist and are upgradeable
        // Iterate backwards from the end and skip any turret that can't be upgraded (brand-new or last tier)
        int count = 0;
        for (int i = turrets.Length - 1; i >= 0 && count < 4; i--)
        {
            var turret = turrets[i];
            if (turret.Upgradable && turret.CanUpgradeAndAfford(World.AddGold))
            {
                var def = World.AbilityDefs[turret.DefIdx];
                var name = def.Name;
                var icon = def.Icon;
                
                upgradeable[count] = (name, def.Name, def.UpgradeCost[def.Tier], icon);
                count++;
            }
        }
        
        _upgradeButtons = null; // release previous
        _buttonList?.QueueFree();

        if (count <= 0)
        {
            var label = new Label
            {
                Text = "No upgrades available.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Modulate = new Color(0.6f, 0.6f, 0.6f, 1f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            label.AddThemeFontSizeOverride("font_size", 20);
            _buttonList.AddChild(label);
            return;
        }

        _upgradeButtons = new Button?[count];
        for (int j = 0; j < count; j++)
        {
            var tuple = upgradeable[j];
            _upgradeButtons[j] = CreateUpgradeButton(turret, tuple, j);
            _buttonList.AddChild(_upgradeButtons[j]);
        }
    }

    private Button CreateUpgradeButton(Turret turret, (string Name, string DefName, int Cost, string? Icon) info, int index)
    {
        var btn = new Button
        {
            CustomMinimumSize = new Vector2(336, 124),
            Set("theme_override/theme_override/corners", new Vector2(10, 10, 10, 10)),
            Set("theme_override/theme_override/corners", new Vector2(10, 10, 10, 10)),
            Set("theme_override/theme_override/theme_override/corners", new Vector2(10, 10, 10, 10)),
        };
        btn.Pressed += () => { World.TryUpgradeArenaTurret(); };

        var header = new HBoxContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, OffsetBottom = -440,
            Size = new Vector2(0, 72),
        };
        
        var name = new Label { Text = info.Name, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        name.AddThemeColorOverride("font_color", UiTheme.Accent2);
        name.AddThemeFontSizeOverride("font_size", 18);
        var slash = new Label { Text = "/", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        slash.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f, 1f));
        name.AddThemeFontSizeOverride("font_size", 18);
        header.AddChild(name);
        header.AddChild(slash);
        var tier = new Label
        {
            Text = $"T{info.DefName}", // actually showing tier level, not full ref-name
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = new Color(0.5f, 0.5f, 0.5f, 1f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        tier.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1f));
        tier.AddThemeFontSizeOverride("font_size", 16);
        header.AddChild(tier);
        header.AddThemeConstantOverride("separation", 6);
        var left = header.RectGetChild(0);
        var right = header.RectGetChild(2);
        header.Left = left; header.Right = right;
        var iconRect = new TextureRect { Size = new Vector2(60, 32), Stretch = Texture.StretchFlags.KeepAspect, Texture = null };
        btn.AddChild(header);
        
        // left-side description (name + tier)
        var blurbName = new Label { Text = info.Name, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        blurbName.AddThemeFontSizeOverride("font_size", 24);
        blurbName.AddThemeFontOverride("font", UiTheme.Display);
        blurbName.AddThemeColorOverride("font_color", UiTheme.Accent2);
        left.AddChild(blurbName);

        var blurbSlash = new Label { Text = "/" };
        left.AddThemeConstantOverride("separation", 10);
        left.AddChild(blurbSlash);

        var blurbTier = new Label
        {
            Text = $"T{info.DefName}", // actually showing tier level, not full ref-name
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = new Color(0.5f, 0.5f, 0.5f, 1f),
        };
        blurbTier.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1f));
        left.AddThemeConstantOverride("separation", 20);
        left.AddChild(blurbTier);

        header.AddThemeConstantOverride("separation", 4);
        left.AddChild(header);

        // right-side: cost + icon
        var costRow = new Label { Text = $"COST {info.Cos}", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        costRow.AddThemeColorOverride("font_color", UiTheme.Accent);
        costRow.AddThemeFontSizeOverride("font_size", 34);
        costRow.AddThemeFontOverride("font", UiTheme.Display);
        right.AddChild(costRow);

        var icon = new TextureRect { Size = new Vector2(56, 56), Stretch = Texture.StretchFlags.KeepAspectCover };
        btn.AddChild(icon);

        left.AddThemeConstantOverride("separation", 2);
        right.AddChild(right);

        btn.Set("theme_override/theme_override/corners", new Vector2(10, 10, 10, 10));
        btn.Set("theme_override/theme_override/theme_override/corners", new Vector2(10, 10, 10, 10));
        btn.Set("theme_override/theme_override/theme_override/corners", new Vector2(10, 10, 10, 10));
        _upgradeButtons![index] = btn;
        return btn;
    }
}
