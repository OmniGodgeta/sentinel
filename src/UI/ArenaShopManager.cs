using Godot;
using Sentinel.Sim;

namespace Sentinel.UI;

/// <summary>
/// Turret-upgrade shop shown during a Galaxy Arena intermission (see
/// <c>GameRoot</c>'s arena overlay, the only place this is instantiated). Builds
/// its own <see cref="Button"/> rows in code — no exported prefab scene needed.
/// </summary>
public partial class ArenaShopManager : Node
{
    private Control _container = null!;
    private SimWorld _world = null!;

    public void Initialize(SimWorld world, Control container)
    {
        _world = world;
        _container = container;
    }

    public void RefreshShop()
    {
        if (_container == null || _world == null) return;

        foreach (var child in _container.GetChildren())
            child.QueueFree();

        // Planet repair: the gold sink once turrets are maxed (balance.json arena_repair_*).
        int pct = Mathf.RoundToInt(100f * _world.PlanetIntegrity / Mathf.Max(1f, _world.PlanetIntegrityMax));
        int repairCost = _world.ArenaRepairCost;
        bool damaged = _world.PlanetIntegrity < _world.PlanetIntegrityMax;
        var repair = new Button
        {
            Text = damaged
                ? $"Repair planet  ({pct}% → +{Mathf.RoundToInt(_world.B.ArenaRepairFrac * 100f)}%)  ·  {repairCost}G"
                : "Planet at full integrity",
            Disabled = !damaged || _world.ArenaGold < repairCost,
            CustomMinimumSize = new Vector2(0, 72),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        repair.AddThemeFontSizeOverride("font_size", 20);
        repair.Pressed += OnRepairPressed;
        _container.AddChild(repair);

        bool any = false;
        for (int i = 0; i < _world.Turrets.Length; i++)
        {
            var turret = _world.Turrets[i];
            if (!turret.Built) continue;

            int cost = _world.TurretUpgradeCost(i);
            if (cost < 0 || turret.Level >= 3) continue;

            any = true;
            int slot = i;
            var btn = new Button
            {
                Text = $"Upgrade Turret {i + 1}  (Lv {turret.Level} → {turret.Level + 1})  ·  {cost}G",
                CustomMinimumSize = new Vector2(0, 72),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            btn.AddThemeFontSizeOverride("font_size", 20);
            btn.Pressed += () => OnUpgradePressed(slot);
            _container.AddChild(btn);
        }

        if (!any)
        {
            _container.AddChild(new Label
            {
                Text = "No turrets to upgrade: build one during the next wave, or they're all at Lv 3.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(1, 1, 1, 0.55f),
            });
        }
    }

    private void OnRepairPressed()
    {
        if (_world.TryArenaRepair(out _))
            Sentinel.Audio.AudioManager.Instance?.Confirm();
        else
            Sentinel.Audio.AudioManager.Instance?.Click();
        RefreshShop();
    }

    private void OnUpgradePressed(int slot)
    {
        if (_world.TryUpgradeArenaTurret(slot, out int cost))
        {
            Sentinel.Audio.AudioManager.Instance?.Confirm();
            RefreshShop();
        }
        else
        {
            Sentinel.Audio.AudioManager.Instance?.Click();
        }
    }
}
