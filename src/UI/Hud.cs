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
    private Control _ctlRow = null!;
    private Label _status = null!;
    private ProgressBar _integrity = null!;
    private ProgressBar _xpBar = null!;
    private float _dpsSmooth;

    private static StyleBoxFlat Filled(Color c, int radius) => new()
    {
        BgColor = c,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
    };
    private Button[] _speed = new Button[4];
    private Button _pause = null!;
    private Button _menuOpen = null!;
    private Button _buildToggle = null!;

    /// <summary>Build/upgrade panel is showing over the play field (real-time management).</summary>
    public bool BuildOpen { get; private set; }
    private PanelContainer _pauseMenu = null!;
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
    private VirtualJoystick _joystick = null!;
    private HBoxContainer _weaponRow = null!;
    private Button _autoBtn = null!;

    private ColorRect _draftDim = null!;
    private PanelContainer _draftPanel = null!;
    private HBoxContainer _draftCards = null!;
    private Label _draftHeader = null!;
    private readonly System.Collections.Generic.Dictionary<string, Texture2D> _cardTex = new();

    private ColorRect _endDim = null!;
    private PanelContainer _endCard = null!;
    private VBoxContainer _endBody = null!;
    private bool _endBuilt;

    private ProgressBar _bossBar = null!;
    private Label _banner = null!;
    private float _bannerTime;

    private int _selectedSlot = -1;

    public override void _Ready()
    {
        Layer = 10;

        var top = new VBoxContainer { AnchorRight = 1f, OffsetLeft = 10, OffsetTop = 8, OffsetRight = -10 };
        _topBox = top;
        top.AddThemeConstantOverride("separation", 3);
        AddChild(top);

        // planet integrity — the primary bar, with the value drawn on it
        _integrity = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 22) };
        _integrity.AddThemeStyleboxOverride("fill", Filled(new Color(0.30f, 0.85f, 0.55f), 5));
        _integrity.AddThemeStyleboxOverride("background", Filled(new Color(0.05f, 0.03f, 0.04f, 0.85f), 5));
        top.AddChild(_integrity);

        // commander level + XP bar (fills each level, pops an upgrade card)
        _xpBar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 0, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 9) };
        _xpBar.AddThemeStyleboxOverride("fill", Filled(UiTheme.Accent, 4));
        _xpBar.AddThemeStyleboxOverride("background", Filled(new Color(0.03f, 0.04f, 0.07f, 0.85f), 4));
        top.AddChild(_xpBar);

        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _status.AddThemeFontSizeOverride("font_size", 18);
        top.AddChild(_status);

        // speed + pause + leave + build row
        var ctl = new HBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -256, OffsetTop = 96, OffsetRight = 256,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        _ctlRow = ctl;
        ctl.AddThemeConstantOverride("separation", 7);
        AddChild(ctl);
        _menuOpen = new Button { Text = "☰", CustomMinimumSize = new Vector2(64, 56) };
        _menuOpen.AddThemeFontSizeOverride("font_size", 22);
        _menuOpen.Pressed += () => { if (!Root.IsPaused) { Root.TogglePause(); _pause.Text = "▶"; } };
        ctl.AddChild(_menuOpen);
        _pause = new Button { Text = "❚❚", CustomMinimumSize = new Vector2(64, 56) };
        _pause.AddThemeFontSizeOverride("font_size", 20);
        _pause.Pressed += () => { Root.TogglePause(); _pause.Text = Root.IsPaused ? "▶" : "❚❚"; };
        ctl.AddChild(_pause);
        for (int i = 0; i < 4; i++)
        {
            int mult = i + 1;
            var btn = new Button { Text = $"{mult}x", CustomMinimumSize = new Vector2(72, 56), ToggleMode = true };
            btn.AddThemeFontSizeOverride("font_size", 19);
            btn.Pressed += () => { Root.SetSpeed(mult); UpdateSpeedButtons(mult); };
            ctl.AddChild(btn);
            _speed[i] = btn;
        }
        UpdateSpeedButtons(1);
        _buildToggle = new Button { Text = "⚒", CustomMinimumSize = new Vector2(64, 56), ToggleMode = true };
        _buildToggle.AddThemeFontSizeOverride("font_size", 22);
        _buildToggle.TooltipText = "Build / upgrade turrets";
        ctl.AddChild(_buildToggle);
        _autoBtn = new Button { Text = "AUTO", CustomMinimumSize = new Vector2(74, 56), ToggleMode = true, ButtonPressed = true };
        _autoBtn.AddThemeFontSizeOverride("font_size", 15);
        _autoBtn.TooltipText = "Ship weapons auto-fire — tap to fire them by hand instead";
        _autoBtn.Pressed += () => Root.RequestToggleAutoFire();
        ctl.AddChild(_autoBtn);

        // boss bar
        _bossBar = new ProgressBar
        {
            AnchorLeft = 0.1f, AnchorRight = 0.9f, OffsetTop = 138, CustomMinimumSize = new Vector2(0, 16),
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
        _wavePreview.AddThemeFontSizeOverride("font_size", 17);
        bv.AddChild(_wavePreview);
        _slotLabel = new Label { Text = "① tap an empty slot around the planet", HorizontalAlignment = HorizontalAlignment.Center };
        _slotLabel.AddThemeFontSizeOverride("font_size", 17);
        bv.AddChild(_slotLabel);
        _turretRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _turretRow.AddThemeConstantOverride("separation", 8);
        bv.AddChild(_turretRow);
        _upgradeRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _upgradeRow.AddThemeConstantOverride("separation", 8);
        bv.AddChild(_upgradeRow);
        _launch = new Button { Text = "▶   BEGIN DEFENSE", CustomMinimumSize = new Vector2(0, 62) };
        _launch.AddThemeFontSizeOverride("font_size", 22);
        _launch.AddThemeColorOverride("font_color", new Color(0.6f, 1f, 0.7f));
        _launch.Pressed += () => Root.RequestLaunchWave();
        bv.AddChild(_launch);

        // ---- wave panel ----
        _wavePanel = MakeBottomPanel();
        _wavePanel.OffsetTop = -244;   // weapon row + ability bar + hint
        AddChild(_wavePanel);
        var wv = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _wavePanel.AddChild(wv);
        _weaponRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _weaponRow.AddThemeConstantOverride("separation", 6);
        wv.AddChild(_weaponRow);
        var abRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        abRow.AddThemeConstantOverride("separation", 10);
        wv.AddChild(abRow);
        for (int i = 0; i < _abilityBtns.Length; i++)
        {
            int slot = i;
            var btn = new AbilityButton { Visible = false };
            btn.OnPress = () => Root.RequestAbility(slot);
            abRow.AddChild(btn);
            _abilityBtns[i] = btn;
        }
        var hint = new Label { Text = "joystick: fly the ship   ·   tap an enemy: focus fire   ·   ⚒ : build", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.45f) };
        hint.AddThemeFontSizeOverride("font_size", 14);
        wv.AddChild(hint);

        // transparent movement joystick — the whole left play area is the touch zone
        _joystick = new VirtualJoystick
        {
            AnchorLeft = 0f, AnchorRight = 0.55f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetTop = 150, OffsetBottom = -210,
        };
        _joystick.OnMove = d => Root.HeroJoystick(d);
        AddChild(_joystick);

        // big centred targeting prompt (over the play area)
        _reticlePrompt = new Label
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.5f,
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1f, 0.92f, 0.4f), Text = "",
        };
        _reticlePrompt.AddThemeFontSizeOverride("font_size", 26);
        AddChild(_reticlePrompt);

        // ---- weapon upgrade draft (centred popup with card art) ----
        _draftDim = new ColorRect { Color = new Color(0.01f, 0.02f, 0.04f, 0.72f), Visible = false };
        _draftDim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _draftDim.MouseFilter = Control.MouseFilterEnum.Stop;   // eat taps behind the popup
        AddChild(_draftDim);

        _draftPanel = new PanelContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = 14, OffsetRight = -14, OffsetTop = -258, OffsetBottom = 258, Visible = false,
        };
        AddChild(_draftPanel);
        var dv = new VBoxContainer();
        dv.AddThemeConstantOverride("separation", 12);
        _draftPanel.AddChild(dv);
        _draftHeader = new Label { Text = "WEAPONS UPGRADE", HorizontalAlignment = HorizontalAlignment.Center };
        _draftHeader.AddThemeFontOverride("font", UiTheme.Display);
        _draftHeader.AddThemeFontSizeOverride("font_size", 22);
        _draftHeader.AddThemeColorOverride("font_color", UiTheme.Accent);
        dv.AddChild(_draftHeader);
        _draftCards = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _draftCards.AddThemeConstantOverride("separation", 14);
        dv.AddChild(_draftCards);

        // ---- end / reward card (centred) ----
        _endDim = new ColorRect { Color = new Color(0.01f, 0.02f, 0.04f, 0.8f), Visible = false };
        _endDim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _endDim.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(_endDim);

        _endCard = new PanelContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = 22, OffsetRight = -22, OffsetTop = -260, OffsetBottom = 260, Visible = false,
        };
        AddChild(_endCard);
        _endBody = new VBoxContainer();
        _endBody.AddThemeConstantOverride("separation", 12);
        _endCard.AddChild(_endBody);

        // ---- banner ----
        _banner = new Label { AnchorRight = 1f, OffsetTop = 156, HorizontalAlignment = HorizontalAlignment.Center };
        _banner.AddThemeFontSizeOverride("font_size", 32);
        AddChild(_banner);

        // ---- pause / leave menu ----
        _pauseMenu = new PanelContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -200, OffsetRight = 200, OffsetTop = -150, OffsetBottom = 150, Visible = false,
        };
        AddChild(_pauseMenu);
        var pm = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        pm.AddThemeConstantOverride("separation", 16);
        _pauseMenu.AddChild(pm);
        var pmTitle = new Label { Text = "PAUSED", HorizontalAlignment = HorizontalAlignment.Center };
        pmTitle.AddThemeFontSizeOverride("font_size", 26);
        pm.AddChild(pmTitle);
        var pmResume = new Button { Text = "▶   Resume", CustomMinimumSize = new Vector2(320, 64), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        pmResume.AddThemeFontSizeOverride("font_size", 21);
        pmResume.Pressed += () => { if (Root.IsPaused) { Root.TogglePause(); _pause.Text = "❚❚"; } };
        pm.AddChild(pmResume);
        var pmLeave = new Button { Text = "◄   Leave to Main Menu", CustomMinimumSize = new Vector2(320, 64), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        pmLeave.AddThemeFontSizeOverride("font_size", 19);
        pmLeave.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.55f));
        pmLeave.Pressed += () => Root.GoToMenu();
        pm.AddChild(pmLeave);
        var pmHint = new Label { Text = "leaving forfeits this run's rewards", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.45f) };
        pmHint.AddThemeFontSizeOverride("font_size", 12);
        pm.AddChild(pmHint);

        foreach (var n in new Control[] { _topBox, _buildPanel, _wavePanel, _draftPanel, _endCard, _pauseMenu })
            n.Theme = UiTheme.Instance;

        RebuildTurretButtons();
    }

    private PanelContainer MakeBottomPanel()
    {
        var p = new PanelContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = 8, OffsetRight = -8, OffsetTop = -298, OffsetBottom = -8,
        };
        _panels.Add(p);
        return p;
    }

    /// <summary>Keep the HUD inside the centred portrait column on wide windows,
    /// and clear of the display's top safe area (notch / punch-hole).</summary>
    public void SetDesignWidth(float designW, Vector2 vp)
    {
        float m = Mathf.Max(0f, (vp.X - designW) * 0.5f);
        float top = Root?.SafeTopInset ?? 0f;
        if (_topBox != null) { _topBox.OffsetLeft = m + 10; _topBox.OffsetRight = -(m + 10); _topBox.OffsetTop = top + 8; }
        if (_ctlRow != null) _ctlRow.OffsetTop = top + 112;
        foreach (var p in _panels) { p.OffsetLeft = m + 8; p.OffsetRight = -(m + 8); }
        if (_bossBar != null)
        {
            _bossBar.AnchorLeft = 0f; _bossBar.AnchorRight = 1f;
            _bossBar.OffsetLeft = m + 40; _bossBar.OffsetRight = -(m + 40);
            _bossBar.OffsetTop = top + 174;
        }
        if (_banner != null) _banner.OffsetTop = top + 194;
    }

    private void RebuildTurretButtons()
    {
        foreach (Node c in _turretRow.GetChildren()) c.QueueFree();
        var unlocked = Root.World.Mods.UnlockedTurrets;
        foreach (var id in Root.World.Cfg.TurretOrder)
        {
            if (unlocked.Count > 0 && !unlocked.Contains(id)) continue;
            var def = Root.World.Cfg.Turret(id);
            var btn = new Button { Text = $"{def.Name}\n${def.Cost}", CustomMinimumSize = new Vector2(124, 76) };
            btn.AddThemeFontSizeOverride("font_size", 15);
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
        _pauseMenu.Visible = Root.IsPaused && !Root.DraftPause && Root.World.Phase is not (SimPhase.Won or SimPhase.Lost);

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
        float hpFraction = w.PlanetIntegrityMax > 0 ? w.PlanetIntegrity / w.PlanetIntegrityMax : 0;
        _integrity.Value = hpFraction;
        var hpCol = hpFraction > 0.5f ? new Color(0.30f, 0.85f, 0.55f)
                  : hpFraction > 0.25f ? new Color(0.95f, 0.75f, 0.30f)
                  : new Color(0.95f, 0.35f, 0.35f);
        _integrity.AddThemeStyleboxOverride("fill", Filled(hpCol, 5));

        // commander XP bar
        float xpFloor = w.RunXpFloor, xpCeil = w.RunXpCeil;
        _xpBar.Value = xpCeil > xpFloor ? Mathf.Clamp((w.RunXp - xpFloor) / (xpCeil - xpFloor), 0f, 1f) : 0f;
        _xpBar.Visible = w.IsSurvival;

        // live DPS (smoothed)
        float elapsed = Mathf.Max(1f, w.PhaseTimer);
        float dps = w.Stats.TotalDamage / elapsed;
        _dpsSmooth = Mathf.Lerp(_dpsSmooth, dps, 0.1f);

        bool ended = w.Phase is SimPhase.Won or SimPhase.Lost;
        bool draft = w.HasPendingDraft && !ended;
        bool prep = w.Phase == SimPhase.Build && !draft;
        bool fighting = w.Phase == SimPhase.Wave && !ended;
        if (prep || draft) _buildToggle.ButtonPressed = false;
        BuildOpen = !ended && !draft && (prep || (fighting && _buildToggle.ButtonPressed));

        string phaseStr;
        if (w.IsSurvival && fighting)
        {
            float shown = w.Mission.Duration > 0f ? w.SurvivalTimeLeft : w.PhaseTimer;
            phaseStr = $"⧗ {Mathf.FloorToInt(shown / 60f)}:{Mathf.PosMod(Mathf.FloorToInt(shown), 60):00}"
                       + (w.Mission.Duration > 0f ? "  HOLD" : "  ENDLESS");
        }
        else if (prep) phaseStr = w.IsSurvival ? "PREP" : "BUILD";
        else if (w.IsEndless) phaseStr = $"WAVE {w.WaveIndex + 1}  ·  ENDLESS";
        else phaseStr = $"WAVE {Mathf.Min(w.WaveIndex + 1, w.WaveCount)}/{w.WaveCount}";

        string lvl = w.IsSurvival ? $"LV {w.RunLevel}" : "";
        string l1 = $"◈ {Mathf.CeilToInt(w.PlanetIntegrity)}    ⬡ {w.Credits}    {phaseStr}    {lvl}";
        string l2 = fighting
            ? $"enemies {w.EnemiesAlive}    ⚔ {Mathf.RoundToInt(_dpsSmooth)} dps    RD {Mathf.FloorToInt(w.ResearchDataEarned)}"
            : $"RD {Mathf.FloorToInt(w.ResearchDataEarned)}    XP {Mathf.FloorToInt(w.XpEarned)}    Cores {w.CoresEarned}";
        _status.Text = l1 + "\n" + l2;

        _buildPanel.Visible = BuildOpen;
        _wavePanel.Visible = fighting && !BuildOpen && !draft;
        _endCard.Visible = ended;
        _endDim.Visible = ended;
        _draftPanel.Visible = draft;
        _draftDim.Visible = draft;
        _buildToggle.Visible = fighting && !draft;
        _autoBtn.Visible = fighting && !draft && w.HeroWeaponCount > 0;
        if (_autoBtn.ButtonPressed != w.HeroAutoFire) _autoBtn.ButtonPressed = w.HeroAutoFire;
        _autoBtn.Text = w.HeroAutoFire ? "AUTO" : "MANUAL";
        _joystick.Visible = fighting && !BuildOpen && !draft;
        if (draft) RefreshDraft(w);
        if (fighting && !BuildOpen && !draft) RefreshWeaponRow(w);

        if (w.TryGetBoss(out _, out float hpFrac, out _))
        { _bossBar.Visible = true; _bossBar.Value = hpFrac; }
        else _bossBar.Visible = false;

        if (BuildOpen)
        {
            _launch.Visible = prep;
            _launch.Disabled = false;
            _launch.Text = w.IsSurvival ? "▶   BEGIN DEFENSE" : $"▶  LAUNCH WAVE {w.WaveIndex + 1}";
            _wavePreview.Text = w.IsSurvival
                ? (prep ? "Place your turrets — the hold begins when you're ready." : "")
                : "NEXT: " + w.NextWavePreview();
            RefreshSlotPanel(w);
        }

        if (fighting && !BuildOpen && !draft)
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

        if (!ended) _endBuilt = false;
        else if (!_endBuilt) { _endBuilt = true; BuildEndCard(w); }
    }

    private void BuildEndCard(SimWorld w)
    {
        bool won = w.Phase == SimPhase.Won;
        var accent = won ? new Color(0.35f, 0.9f, 0.55f) : new Color(0.95f, 0.42f, 0.4f);
        _endCard.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.06f, 0.10f, 0.99f),
            BorderColor = accent,
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 20, ContentMarginBottom = 20,
        });

        foreach (Node c in _endBody.GetChildren()) c.QueueFree();

        var h = new Label { Text = won ? "PLANET SECURED" : "PLANET LOST", HorizontalAlignment = HorizontalAlignment.Center };
        h.AddThemeFontOverride("font", UiTheme.Display);
        h.AddThemeFontSizeOverride("font_size", 26);
        h.AddThemeColorOverride("font_color", accent);
        _endBody.AddChild(h);

        int secs = Mathf.FloorToInt(w.PhaseTimer);
        string result = won
            ? $"Held the full {Mathf.FloorToInt(w.Mission.Duration) / 60}:00."
            : $"Fell at {secs / 60}:{secs % 60:00}.";
        var sub = new Label { Text = result, HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.8f) };
        sub.AddThemeFontSizeOverride("font_size", 15);
        _endBody.AddChild(sub);

        var rHdr = new Label { Text = won ? "REWARDS   ·   ×2 VICTORY BONUS" : "REWARDS", HorizontalAlignment = HorizontalAlignment.Center };
        rHdr.AddThemeFontSizeOverride("font_size", 13);
        rHdr.AddThemeColorOverride("font_color", won ? new Color(1f, 0.9f, 0.5f) : new Color(1, 1, 1, 0.55f));
        _endBody.AddChild(rHdr);

        void Reward(string label, string val, Color col)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 16);
            var a = new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            a.AddThemeFontSizeOverride("font_size", 17);
            var b = new Label { Text = val, HorizontalAlignment = HorizontalAlignment.Right };
            b.AddThemeFontOverride("font", UiTheme.Display);
            b.AddThemeFontSizeOverride("font_size", 20);
            b.AddThemeColorOverride("font_color", col);
            row.AddChild(a); row.AddChild(b);
            _endBody.AddChild(row);
        }
        Reward("✦  Experience", $"+{Mathf.FloorToInt(w.XpEarned)}", new Color(0.85f, 0.9f, 1f));
        Reward("◇  Research Data", $"+{Mathf.FloorToInt(w.ResearchDataEarned)}", UiTheme.Accent);
        Reward("✷  Sentinel Cores", $"+{w.CoresEarned}", new Color(0.72f, 0.86f, 1f));
        if (w.AlloyEarned > 0) Reward("❖  Exotic Alloy", $"+{w.AlloyEarned}", new Color(0.95f, 0.78f, 0.42f));

        var s = w.Stats;
        float tot = Mathf.Max(1f, s.TotalDamage);
        var dmg = new Label
        {
            Text = $"Kills {s.EnemiesKilled}   ·   turrets {Pct(s.DamageByTurrets, tot)}  ship {Pct(s.DamageByHero, tot)}  orbital {Pct(s.DamageByOrbital, tot)}  abilities {Pct(s.DamageByAbilities, tot)}",
            HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.5f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        dmg.AddThemeFontSizeOverride("font_size", 12);
        _endBody.AddChild(dmg);

        var btns = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btns.AddThemeConstantOverride("separation", 14);
        _endBody.AddChild(btns);
        var retry = new Button { Text = "↻  Retry", CustomMinimumSize = new Vector2(180, 62) };
        retry.AddThemeFontSizeOverride("font_size", 18);
        retry.Pressed += () => Root.RestartMission();
        btns.AddChild(retry);
        var menu = new Button { Text = won ? "Continue ▸" : "Menu", CustomMinimumSize = new Vector2(180, 62) };
        menu.AddThemeFontSizeOverride("font_size", 18);
        if (won) UiTheme.StylePrimary(menu);
        menu.Pressed += () => Root.GoToMenu();
        btns.AddChild(menu);
    }

    private Texture2D? CardTexture(string id)
    {
        if (_cardTex.TryGetValue(id, out var t)) return t;
        t = GD.Load<Texture2D>($"res://assets/game/cards/{id}.jpg");
        _cardTex[id] = t;
        return t;
    }

    private static Color HexColor(string hex, Color fallback)
    {
        try { return new Color(hex); } catch { return fallback; }
    }

    private int _draftShownHash = -1;
    private void RefreshDraft(SimWorld w)
    {
        _draftHeader.Text = $"WEAPONS UPGRADE   ·   COMMANDER LEVEL {w.RunLevel}";

        int hash = 17;
        foreach (int i in w.DraftOptionIndices) hash = hash * 31 + i;
        hash = hash * 31 + w.RunCards.Count;
        if (hash == _draftShownHash) return;
        _draftShownHash = hash;

        foreach (Node c in _draftCards.GetChildren()) c.QueueFree();
        foreach (int idx in w.DraftOptionIndices)
        {
            bool orbital = w.IsOrbitalCard(idx);
            int wi = w.CardWeaponIndex(idx);
            string cardId, cardName, accent;
            int lvl;
            string subtitle;
            if (orbital)
            {
                var o = w.Cfg.OrbitalWeapons[wi];
                cardId = o.Id; cardName = o.Name; accent = o.Accent;
                lvl = w.OrbitalWeaponLevel(wi);
                subtitle = "ORBITAL WEAPON";
            }
            else
            {
                var hh = w.Cfg.HeroWeapons[wi];
                cardId = hh.Id; cardName = hh.Name; accent = hh.Accent;
                lvl = w.HeroWeaponLevel(wi);
                subtitle = "SHIP WEAPON";
            }
            var col = HexColor(accent, UiTheme.Accent);
            int i2 = idx;

            var btn = new Button
            {
                CustomMinimumSize = new Vector2(140, 440),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                ClipContents = true,
            };
            btn.AddThemeStyleboxOverride("normal", CardBox(col, 0.12f));
            btn.AddThemeStyleboxOverride("hover", CardBox(col, 0.30f));
            btn.AddThemeStyleboxOverride("pressed", CardBox(col, 0.40f));
            btn.AddThemeStyleboxOverride("focus", CardBox(col, 0.30f));
            btn.Pressed += () => { Root.RequestPickCard(i2); _draftShownHash = -1; };

            var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            v.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            v.OffsetLeft = 6; v.OffsetRight = -6; v.OffsetTop = 6; v.OffsetBottom = -6;
            v.AddThemeConstantOverride("separation", 6);
            btn.AddChild(v);

            var art = new TextureRect
            {
                Texture = CardTexture(cardId),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                CustomMinimumSize = new Vector2(0, 292),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ClipContents = true,
            };
            v.AddChild(art);

            var nm = new Label { Text = cardName.ToUpperInvariant(), HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            nm.AddThemeFontOverride("font", UiTheme.Display);
            nm.AddThemeFontSizeOverride("font_size", 15);
            nm.AddThemeColorOverride("font_color", col.Lightened(0.3f));
            v.AddChild(nm);

            var tag = new Label { Text = subtitle, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            tag.AddThemeFontSizeOverride("font_size", 10);
            tag.AddThemeColorOverride("font_color", new Color(col, 0.7f));
            v.AddChild(tag);

            var lv = new Label
            {
                Text = lvl == 0 ? "UNLOCK  ·  NEW SYSTEM" : $"LEVEL {lvl}  →  {lvl + 1}",
                HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            lv.AddThemeFontSizeOverride("font_size", 13);
            lv.AddThemeColorOverride("font_color", lvl == 0 ? new Color(1f, 0.9f, 0.5f) : new Color(1, 1, 1, 0.8f));
            v.AddChild(lv);

            _draftCards.AddChild(btn);
        }
    }

    private void RefreshWeaponRow(SimWorld w)
    {
        int n = w.HeroWeaponCount;
        // (re)build the buttons only when the set of unlocked weapons changes
        int sig = 0;
        for (int i = 0; i < n; i++) sig = sig * 7 + (w.HeroWeaponLevel(i) > 0 ? 1 : 0);
        if (sig != _weaponRowSig)
        {
            _weaponRowSig = sig;
            foreach (Node c in _weaponRow.GetChildren()) c.QueueFree();
            _weaponBtns.Clear();
            for (int i = 0; i < n; i++)
            {
                if (w.HeroWeaponLevel(i) <= 0) continue;
                var def = w.Cfg.HeroWeapons[i];
                var col = HexColor(def.Accent, UiTheme.Accent);
                int i2 = i;
                var b = new Button { CustomMinimumSize = new Vector2(96, 52) };
                b.AddThemeFontSizeOverride("font_size", 12);
                b.AddThemeStyleboxOverride("normal", CardBox(col, 0.16f));
                b.AddThemeStyleboxOverride("hover", CardBox(col, 0.30f));
                b.AddThemeStyleboxOverride("pressed", CardBox(col, 0.40f));
                b.AddThemeColorOverride("font_color", col.Lightened(0.35f));
                b.TooltipText = def.Name;
                if (def.AlwaysOn) b.Disabled = true;   // Plasma Field — passive
                else b.Pressed += () => Root.RequestFireWeapon(i2);
                _weaponRow.AddChild(b);
                _weaponBtns.Add((i, b));
            }
        }

        foreach (var (i, b) in _weaponBtns)
        {
            var def = w.Cfg.HeroWeapons[i];
            int lvl = w.HeroWeaponLevel(i);
            float cd = w.HeroWeaponCooldownLeft(i);
            string tag = ShortName(def.Name);
            if (def.AlwaysOn) b.Text = $"{tag}\nLv{lvl} · ON";
            else if (cd > 0.05f) b.Text = $"{tag}\n{cd:0.0}s";
            else b.Text = $"{tag}\nLv{lvl}";
            b.Modulate = (cd > 0.05f && !def.AlwaysOn) ? new Color(1, 1, 1, 0.45f) : Colors.White;
        }
    }

    private int _weaponRowSig = -1;
    private readonly System.Collections.Generic.List<(int idx, Button btn)> _weaponBtns = new();
    private static string ShortName(string n)
    {
        int sp = n.IndexOf(' ');
        return sp > 0 ? n[..sp].ToUpperInvariant() : n.ToUpperInvariant();
    }

    private static StyleBoxFlat CardBox(Color accent, float bgA) => new()
    {
        BgColor = new Color(accent, bgA * 0.5f + 0.04f),
        BorderColor = new Color(accent, 0.55f + bgA),
        BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
        ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 4, ContentMarginBottom = 4,
    };

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
            var up = new Button { Text = $"Upgrade → L{t.Level + 1}\n${cost}", CustomMinimumSize = new Vector2(164, 66) };
            up.AddThemeFontSizeOverride("font_size", 16);
            up.Disabled = cost < 0 || w.Credits < cost;
            up.Pressed += () => Root.RequestUpgrade(s);
            _upgradeRow.AddChild(up);
        }
        else if (t.Fork < 0 && def.Forks.Count >= 2)
        {
            for (int f = 0; f < def.Forks.Count; f++)
            {
                int fi = f;
                var fb = new Button { Text = def.Forks[f].Name, CustomMinimumSize = new Vector2(164, 66) };
                fb.AddThemeFontSizeOverride("font_size", 16);
                fb.Pressed += () => Root.RequestFork(s, fi);
                _upgradeRow.AddChild(fb);
            }
        }
        var sell = new Button { Text = "Sell", CustomMinimumSize = new Vector2(92, 60) };
        sell.Pressed += () => Root.RequestSell(s);
        _upgradeRow.AddChild(sell);
    }

    private static string Pct(float v, float tot) => $"{Mathf.RoundToInt(v / tot * 100f)}%";
    private void UpdateSpeedButtons(int active) { for (int i = 0; i < 4; i++) _speed[i].ButtonPressed = (i + 1) == active; }

    private SimRenderer GetRenderer()
    {
        foreach (Node c in Root.GetChildren()) if (c is SimRenderer r) return r;
        return null!;
    }
}
