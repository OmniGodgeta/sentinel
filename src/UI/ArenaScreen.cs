using Godot;
using System;
using Sentinel.Sim;

namespace Sentinel.UI;

public partial class ArenaScreen : Control
{
    public Sentinel.Game.AppRoot App { get; set; } = null!;

    [Export] public Control MainContainer;
    [Export] public Label WaveInfoLabel;
    [Export] public Button StartButton;
    [Export] public Label GoldLabel; // New: Displays ArenaGold
    [Export] public Control ShopContainer; // New: Container for upgrade buttons

    public event Action OnStartRequested;

    public override void _Ready()
    {
        Visible = false;
        if (StartButton != null)
        {
            StartButton.Pressed += () => 
            {
                OnStartRequested?.Invoke();
                Visible = false;
            };
        }
    }

    public void UpdateUI(int wave, int gold)
    {
        if (WaveInfoLabel != null)
        {
            WaveInfoLabel.Text = $"Galaxy Arena - Wave {wave}";
        }
        if (GoldLabel != null)
        {
            GoldLabel.Text = $"Gold: {gold}";
        }
    }

    public void Show(int wave = 1, int gold = 0)
    {
        Visible = true;
        UpdateUI(wave, gold);
    }

    public void Hide()
    {
        Visible = false;
    }

    public void ClearShop()
    {
        if (ShopContainer != null)
        {
            foreach (var child in ShopContainer.GetChildren())
            {
                child.QueueFree();
            }
        }
    }
}
