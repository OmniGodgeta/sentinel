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
    private AbilityButton[] _abilityBtns = new AbilityButton[8];
    private readonly bool[] _configured = new bool[8];
    private Label _reticlePrompt = null!;

    private PanelContainer _draftPanel = null!;
    private VBoxContainer _draftCards = null!;

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
        _slotLabel = new Label { Text = "① tap an empty slot around the planet", HorizontalAlignment = HorizontalAlignment.Center };
        _slotLabel.AddThemeFontSizeOverride("font_size", 12);
        bv.AddChild(_slotLabel);
        _turretRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _turretRow.AddThemeConstantOverride("separation", 6);
        bv.AddChild(_turretRow);
        _upgradeRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        bv.AddChild(_upgradeRow);
        _launch = new Button { Text = "▶   LAUNCH WAVE", CustomMinimumSize = new Vector2(0, 56) };
        _launch.AddThemeFontSizeOverride("font_size", 20);
        _launch.AddThemeColorOverride("font_color", new Color(0.6f, 1f, 0.7f));
        _launch.Pressed += () => Root.RequestLaunchWave();
        bv.AddChild(_launch);

        // ---- wave panel ----
        _wavePanel = MakeBottomPanel();
        _wavePanel.OffsetTop = -136;   // just the ability bar + hint
        AddChild(_wavePanel);
        var wv = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _wavePanel.AddChild(wv);
        var abRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        abRow.AddThemeConstantOverride("separation", 8);
        wv.AddChild(abRow);
        for (int i = 0; i < _abilityBtns.Length; i++)
        {
            int slot = i;
            var btn = new AbilityButton { Visible = false };
            btn.OnPress = () => Root.RequestAbility(slot);
            abRow.AddChild(btn);
            _abilityBtns[i] = btn;
        }
        var hint = new Label { Text = "drag: move ship    ·    tap play area: missile volley", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.45f) };
        hint.AddThemeFontSizeOverride("font_size", 11);
        wv.AddChild(hint);

        // big centred targeting prompt (over the play area)
        _reticlePrompt = new Label
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.5f,
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1f, 0.92f, 0.4f), Text = "",
        };
        _reticlePrompt.AddThemeFontSizeOverride("font_size", 22);
        AddChild(_reticlePrompt);

        // ---- card draft ----
        _draftPanel = MakeBottomPanel();
        _draftPanel.Visible = false;
        AddChild(_draftPanel);
        var dv = new VBoxContainer();
        _draftPanel.AddChild(dv);
        var dh = new Label { Text = "CHOOSE A CARD", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1f, 0.9f, 0.5f) };
        dh.AddThemeFontSizeOverride("font_size", 14);
        dv.AddChild(dh);
        _draftCards = new VBoxContainer();
        _draftCards.AddThemeConstantOverride("separation", 5);
        dv.AddChild(_draftCards);

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

        foreach (var n in new Control[] { _topBox, _buildPanel, _wavePanel, _draftPanel, _endCard })
            n.Theme = UiTheme.Instance;

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

    public void ShowReticlePrompt(string abilityName) => _reticlePrompt.Text = $"▽  TAP A TARGET  ▽\n{abilityName}";
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
        string waveStr = w.IsEndless ? $"WAVE {w.WaveIndex + 1}  ·  ENDLESS"
                                     : $"WAVE {Mathf.Min(w.WaveIndex + 1, w.WaveCount)}/{w.WaveCount}";
        _status.Text = $"◈ {Mathf.CeilToInt(w.PlanetIntegrity)}/{Mathf.CeilToInt(w.PlanetIntegrityMax)}     ⬡ {w.Credits}     {waveStr}\n{l2}";

        bool draft = w.Phase == SimPhase.Build && w.HasPendingDraft;
        bool build = w.Phase == SimPhase.Build && !draft;
        bool wave = w.Phase == SimPhase.Wave;
        bool ended = w.Phase is SimPhase.Won or SimPhase.Lost;
        _buildPanel.Visible = build;
        _wavePanel.Visible = wave;
        _endCard.Visible = ended;
        _draftPanel.Visible = draft;
        if (draft) RefreshDraft(w);

        if (w.TryGetBoss(out _, out float hpFrac, out _))
        { _bossBar.Visible = true; _bossBar.Value = hpFrac; }
        else _bossBar.Visible = false;

        if (build)
        {
            bool noMore = !w.IsEndless && w.WaveIndex >= w.WaveCount;
            _launch.Disabled = noMore;
            _launch.Text = noMore ? "— last wave cleared —" : $"▶  LAUNCH WAVE {w.WaveIndex + 1}";
            _wavePreview.Text = noMore ? "" : "NEXT: " + w.NextWavePreview();
            RefreshSlotPanel(w);
        }

        if (wave)
        {
            var abil = w.AbilityView;
            int armed = Root.PendingReticleSlot;
            for (int i = 0; i < _abilityBtns.Length; i++)
            {
                var btn = _abilityBtns[i];
                if (i >= abil.Length || abil[i].DefIndex < 0) { btn.Visible = false; continue; }
                btn.Visible = true;
                var def = w.AbilityDefs[abil[i].DefIndex];
                if (!_configured[i]) { btn.Configure(def); _configured[i] = true; }
                btn.SetState(abil[i].CooldownLeft, def.Cooldown, abil[i].ActiveLeft, armed == i);
            }
        }

        if (ended)
        {
            _endText.Text = BuildEndReport(w);
            _menuBtn.Text = w.Phase == SimPhase.Won ? "Menu ▸" : "Menu";
        }
    }

    private int _draftShownHash = -1;
    private void RefreshDraft(SimWorld w)
    {
        int hash = 17;
        foreach (int i in w.DraftOptionIndices) hash = hash * 31 + i;
        hash = hash * 31 + w.RunCards.Count;
        if (hash == _draftShownHash) return;
        _draftShownHash = hash;

        foreach (Node c in _draftCards.GetChildren()) c.QueueFree();
        foreach (int idx in w.DraftOptionIndices)
        {
            var card = Root.World.Cfg.Cards[idx];
            var col = card.Rarity switch
            {
                "epic" => new Color(1f, 0.55f, 0.9f),
                "rare" => new Color(0.5f, 0.8f, 1f),
                _ => new Color(0.85f, 0.85f, 0.85f),
            };
            var btn = new Button
            {
                Text = $"{card.Name}\n{card.Text}",
                CustomMinimumSize = new Vector2(0, 56),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Modulate = col,
            };
            btn.AddThemeFontSizeOverride("font_size", 11);
            btn.Pressed += () => { Root.RequestPickCard(idx); _draftShownHash = -1; };
            _draftCards.AddChild(btn);
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
        sb.Append(w.IsEndless ? $"Reached wave {w.WavesCleared + 1}.\n"
                 : w.Phase == SimPhase.Won ? $"Cleared all {w.WaveCount} waves.\n"
                 : $"Held {w.WavesCleared}/{w.WaveCount} waves.\n");
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
