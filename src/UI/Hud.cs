using System.Text;
using Godot;
using Sentinel.Game;
using Sentinel.Sim;
using Sentinel.Render;

namespace Sentinel.UI;

/// <summary>
/// Screen-space HUD, built in code (greybox). Top status strip + speed + pause,
/// a bottom panel that swaps between build tools (with wave preview and per-turret
/// upgrade) and the ability bar, a boss health bar, and the end card.
/// </summary>
public sealed partial class Hud : CanvasLayer
{
    public GameRoot Root = null!;

    private Control _topBox = null!;
    private Label _status = null!;
    private ProgressBar _integrity = null!;
    private Button[] _speed = new Button[4];
    private Button _pause = null!;
    private readonly System.Collections.Generic.List<PanelContainer> _panels = new();

    private PanelContainer _buildPanel = null!;
    private Label _slotLabel = null!;
    private Label _wavePreview = null!;
    private HBoxContainer _turretRow = null!;
    private HBoxContainer _upgradeRow = null!;
    private Button _launch = null!;

    private PanelContainer _wavePanel = null!;
    private Button[] _abilityBtns = new Button[8];
    private Label _reticlePrompt = null!;

    private PanelContainer _endCard = null!;
    private Label _endText = null!;
    private Button _retryBtn = null!;
    private Button _menuBtn = null!;

    private ProgressBar _bossBar = null!;
    private Label _banner = null!;
    private float _bannerTime;

    private int _selectedSlot = -1;

    public override void _Ready()
    {
        Layer = 10;

        var top = new VBoxContainer { AnchorRight = 1f, OffsetLeft = 10, OffsetTop = 6, OffsetRight = -10 };
        _topBox = top;
        AddChild(top);
        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _status.AddThemeFontSizeOverride("font_size", 13);
        top.AddChild(_status);
        _integrity = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) };
        top.AddChild(_integrity);

        // speed + pause row
        var ctl = new HBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -140, OffsetTop = 56, OffsetRight = 140,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        AddChild(ctl);
        _pause = new Button { Text = "❚❚", CustomMinimumSize = new Vector2(42, 30) };
        _pause.Pressed += () => { Root.TogglePause(); _pause.Text = Root.IsPaused ? "▶" : "❚❚"; };
        ctl.AddChild(_pause);
        for (int i = 0; i < 4; i++)
        {
            int mult = i + 1;
            var btn = new Button { Text = $"{mult}x", CustomMinimumSize = new Vector2(48, 30), ToggleMode = true };
            btn.AddThemeFontSizeOverride("font_size", 13);
            btn.Pressed += () => { Root.SetSpeed(mult); UpdateSpeedButtons(mult); };
            ctl.AddChild(btn);
            _speed[i] = btn;
        }
        UpdateSpeedButtons(1);

        // boss bar
        _bossBar = new ProgressBar
        {
            AnchorLeft = 0.1f, AnchorRight = 0.9f, OffsetTop = 92, CustomMinimumSize = new Vector2(0, 12),
            MinValue = 0, MaxValue = 1, Value = 1, ShowPercentage = false, Visible = false,
        };
        _bossBar.AddThemeColorOverride("font_color", new Color(1, 0.4f, 0.4f));
        AddChild(_bossBar);

        // ---- build panel ----
        _buildPanel = MakeBottomPanel();
        AddChild(_buildPanel);
        var bv = new VBoxContainer();
        _buildPanel.AddChild(bv);
        _wavePreview = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1f, 0.85f, 0.5f) };
        _wavePreview.AddThemeFontSizeOverride("font_size", 12);
        bv.AddChild(_wavePreview);
        _slotLabel = new Label { Text = "Tap a turret slot", HorizontalAlignment = HorizontalAlignment.Center };
        _slotLabel.AddThemeFontSizeOverride("font_size", 12);
        bv.AddChild(_slotLabel);
        _turretRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        bv.AddChild(_turretRow);
        _upgradeRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        bv.AddChild(_upgradeRow);
        _launch = new Button { Text = "▶  LAUNCH WAVE", CustomMinimumSize = new Vector2(0, 46) };
        _launch.AddThemeFontSizeOverride("font_size", 17);
        _launch.Pressed += () => Root.RequestLaunchWave();
        bv.AddChild(_launch);

        // ---- wave panel ----
        _wavePanel = MakeBottomPanel();
        AddChild(_wavePanel);
        var wv = new VBoxContainer();
        _wavePanel.AddChild(wv);
        _reticlePrompt = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1f, 0.9f, 0.4f) };
        wv.AddChild(_reticlePrompt);
        var abRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        wv.AddChild(abRow);
        for (int i = 0; i < _abilityBtns.Length; i++)
        {
            int slot = i;
            var btn = new Button { CustomMinimumSize = new Vector2(92, 58), Visible = false };
            btn.AddThemeFontSizeOverride("font_size", 11);
            btn.Pressed += () => Root.RequestAbility(slot);
            abRow.AddChild(btn);
            _abilityBtns[i] = btn;
        }
        var hint = new Label { Text = "drag: move ship   ·   tap: missile volley", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.45f) };
        hint.AddThemeFontSizeOverride("font_size", 11);
        wv.AddChild(hint);

        // ---- end card ----
        _endCard = MakeBottomPanel();
        _endCard.Visible = false;
        AddChild(_endCard);
        var ev = new VBoxContainer();
        _endCard.AddChild(ev);
        _endText = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _endText.AddThemeFontSizeOverride("font_size", 13);
        ev.AddChild(_endText);
        var endBtns = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        ev.AddChild(endBtns);
        _retryBtn = new Button { Text = "Retry", CustomMinimumSize = new Vector2(130, 42) };
        _retryBtn.Pressed += () => Root.RestartMission();
        endBtns.AddChild(_retryBtn);
        _menuBtn = new Button { Text = "Menu", CustomMinimumSize = new Vector2(130, 42) };
        _menuBtn.Pressed += () => Root.GoToMenu();
        endBtns.AddChild(_menuBtn);

        // ---- banner ----
        _banner = new Label { AnchorRight = 1f, OffsetTop = 112, HorizontalAlignment = HorizontalAlignment.Center };
        _banner.AddThemeFontSizeOverride("font_size", 28);
        AddChild(_banner);

        RebuildTurretButtons();
    }

    private PanelContainer MakeBottomPanel()
    {
        var p = new PanelContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = 8, OffsetRight = -8, OffsetTop = -244, OffsetBottom = -8,
        };
        _panels.Add(p);
        return p;
    }

    /// <summary>Keep the HUD inside the centred portrait column on wide windows.</summary>
    public void SetDesignWidth(float designW, Vector2 vp)
    {
        float m = Mathf.Max(0f, (vp.X - designW) * 0.5f);
        if (_topBox != null) { _topBox.OffsetLeft = m + 10; _topBox.OffsetRight = -(m + 10); }
        foreach (var p in _panels) { p.OffsetLeft = m + 8; p.OffsetRight = -(m + 8); }
        if (_bossBar != null)
        {
            _bossBar.AnchorLeft = 0f; _bossBar.AnchorRight = 1f;
            _bossBar.OffsetLeft = m + 40; _bossBar.OffsetRight = -(m + 40);
        }
    }

    private void RebuildTurretButtons()
    {
        foreach (Node c in _turretRow.GetChildren()) c.QueueFree();
        foreach (var id in Root.World.Cfg.TurretOrder)
        {
            var def = Root.World.Cfg.Turret(id);
            var btn = new Button { Text = $"{def.Name}\n${def.Cost}", CustomMinimumSize = new Vector2(88, 50) };
            btn.AddThemeFontSizeOverride("font_size", 10);
            btn.Pressed += () => { if (_selectedSlot >= 0) Root.RequestBuild(_selectedSlot, id); };
            _turretRow.AddChild(btn);
        }
    }

    // ---- called by GameRoot ----
    public void SyncSpeed(int s) => UpdateSpeedButtons(s);

    public void SelectSlot(int slot)
    {
        _selectedSlot = slot;
        GetRenderer().SelectedSlot = slot;
    }

    public void ShowReticlePrompt(string abilityName) => _reticlePrompt.Text = $"tap a target for {abilityName}";
    public void ClearReticlePrompt() => _reticlePrompt.Text = "";

    public void FlashBanner(SimEventKind kind)
    {
        string t = kind switch
        {
            SimEventKind.WaveCleared => "WAVE CLEARED",
            SimEventKind.MissionWon => "PLANET SECURED",
            SimEventKind.MissionLost => "PLANET LOST",
            _ => "",
        };
        if (t == "") return;
        _banner.Text = t;
        _banner.Modulate = kind == SimEventKind.MissionLost ? new Color(1f, 0.4f, 0.4f) : new Color(0.6f, 1f, 0.7f);
        _bannerTime = 2.2f;
    }

    public override void _Process(double delta)
    {
        if (_bannerTime > 0f)
        {
            _bannerTime -= (float)delta;
            _banner.Modulate = new Color(_banner.Modulate, Mathf.Clamp(_bannerTime, 0f, 1f));
            if (_bannerTime <= 0f) _banner.Text = "";
        }
    }

    public void Refresh()
    {
        var w = Root.World;
        _integrity.Value = w.PlanetIntegrityMax > 0 ? w.PlanetIntegrity / w.PlanetIntegrityMax : 0;

        string l2 = w.Phase == SimPhase.Wave
            ? $"enemies {w.EnemiesAlive}   ·   incoming {w.SpawnsRemaining}   ·   RD {Mathf.FloorToInt(w.ResearchDataEarned)}"
            : $"RD {Mathf.FloorToInt(w.ResearchDataEarned)}   ·   XP {Mathf.FloorToInt(w.XpEarned)}   ·   Cores {w.CoresEarned}";
        _status.Text = $"◈ {Mathf.CeilToInt(w.PlanetIntegrity)}/{Mathf.CeilToInt(w.PlanetIntegrityMax)}     ⬡ {w.Credits}     WAVE {Mathf.Min(w.WaveIndex + 1, w.WaveCount)}/{w.WaveCount}\n{l2}";

        bool build = w.Phase == SimPhase.Build;
        bool wave = w.Phase == SimPhase.Wave;
        bool ended = w.Phase is SimPhase.Won or SimPhase.Lost;
        _buildPanel.Visible = build;
        _wavePanel.Visible = wave;
        _endCard.Visible = ended;

        if (w.TryGetBoss(out _, out float hpFrac, out _))
        { _bossBar.Visible = true; _bossBar.Value = hpFrac; }
        else _bossBar.Visible = false;

        if (build)
        {
            _launch.Disabled = w.WaveIndex >= w.WaveCount;
            _launch.Text = w.WaveIndex >= w.WaveCount ? "— last wave cleared —" : $"▶  LAUNCH WAVE {w.WaveIndex + 1}";
            _wavePreview.Text = w.WaveIndex < w.WaveCount ? "NEXT: " + w.NextWavePreview() : "";
            RefreshSlotPanel(w);
        }

        if (wave)
        {
            var abil = w.AbilityView;
            for (int i = 0; i < _abilityBtns.Length; i++)
            {
                var btn = _abilityBtns[i];
                if (i >= abil.Length || abil[i].DefIndex < 0) { btn.Visible = false; continue; }
                btn.Visible = true;
                var def = w.AbilityDefs[abil[i].DefIndex];
                float cd = abil[i].CooldownLeft;
                btn.Disabled = cd > 0f;
                btn.Text = cd > 0f ? $"{def.Name}\n{Mathf.CeilToInt(cd)}s"
                         : abil[i].ActiveLeft > 0f ? $"{def.Name}\n● {Mathf.CeilToInt(abil[i].ActiveLeft)}s"
                         : $"{def.Name}\nREADY";
            }
        }

        if (ended)
        {
            _endText.Text = BuildEndReport(w);
            _menuBtn.Text = w.Phase == SimPhase.Won ? "Menu ▸" : "Menu";
        }
    }

    private void RefreshSlotPanel(SimWorld w)
    {
        foreach (Node c in _upgradeRow.GetChildren()) c.QueueFree();
        int s = _selectedSlot;
        if (s < 0 || s >= w.TurretView.Length) { _slotLabel.Text = "Tap a turret slot"; return; }

        var t = w.TurretView[s];
        if (!t.Built) { _slotLabel.Text = $"Slot {s + 1}: empty — pick a turret"; return; }

        var def = w.TurretDefs[t.DefIndex];
        _slotLabel.Text = $"Slot {s + 1}: {def.Name}  L{t.Level}" + (t.Fork >= 0 ? $" · {def.Forks[t.Fork].Name}" : "");

        if (t.Level < 3)
        {
            int cost = w.TurretUpgradeCost(s);
            var up = new Button { Text = $"Upgrade → L{t.Level + 1}\n${cost}", CustomMinimumSize = new Vector2(120, 46) };
            up.AddThemeFontSizeOverride("font_size", 11);
            up.Disabled = cost < 0 || w.Credits < cost;
            up.Pressed += () => Root.RequestUpgrade(s);
            _upgradeRow.AddChild(up);
        }
        else if (t.Fork < 0 && def.Forks.Count >= 2)
        {
            for (int f = 0; f < def.Forks.Count; f++)
            {
                int fi = f;
                var fb = new Button { Text = def.Forks[f].Name, CustomMinimumSize = new Vector2(120, 46) };
                fb.AddThemeFontSizeOverride("font_size", 11);
                fb.Pressed += () => Root.RequestFork(s, fi);
                _upgradeRow.AddChild(fb);
            }
        }
        var sell = new Button { Text = "Sell", CustomMinimumSize = new Vector2(64, 46) };
        sell.Pressed += () => Root.RequestSell(s);
        _upgradeRow.AddChild(sell);
    }

    private static string BuildEndReport(SimWorld w)
    {
        var s = w.Stats;
        float tot = Mathf.Max(1f, s.TotalDamage);
        var sb = new StringBuilder();
        sb.Append(w.Phase == SimPhase.Won ? $"Cleared all {w.WaveCount} waves.\n" : $"Held {w.WavesCleared}/{w.WaveCount} waves.\n");
        sb.Append($"Research Data {Mathf.FloorToInt(w.ResearchDataEarned)}   XP {Mathf.FloorToInt(w.XpEarned)}   Cores {w.CoresEarned}\n");
        sb.Append($"Kills {s.EnemiesKilled}   ·   Leaked {s.EnemiesLeaked}\n");
        sb.Append($"Damage — turrets {Pct(s.DamageByTurrets, tot)}  hero {Pct(s.DamageByHero, tot)}  abilities {Pct(s.DamageByAbilities, tot)}");
        return sb.ToString();
    }

    private static string Pct(float v, float tot) => $"{Mathf.RoundToInt(v / tot * 100f)}%";
    private void UpdateSpeedButtons(int active) { for (int i = 0; i < 4; i++) _speed[i].ButtonPressed = (i + 1) == active; }

    private SimRenderer GetRenderer()
    {
        foreach (Node c in Root.GetChildren()) if (c is SimRenderer r) return r;
        return null!;
    }
}
