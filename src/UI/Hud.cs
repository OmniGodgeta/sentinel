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
    private Button _speedMain = null!;
    private VBoxContainer _speedMenu = null!;
    private Control _speedCatcher = null!;
    private int _speedNow = 1;
    private Button _pause = null!;
    private Button _menuOpen = null!;
    private Button _updateBadge = null!;

    /// <summary>Build/upgrade panel is showing over the play field (real-time management).</summary>
    public bool BuildOpen { get; private set; }
    private PanelContainer _pauseMenu = null!;
    private readonly System.Collections.Generic.List<PanelContainer> _panels = new();

    private PanelContainer _buildPanel = null!;
    private Label _wavePreview = null!;
    private Button _launch = null!;

    private PanelContainer _wavePanel = null!;
    private VBoxContainer _waveBody = null!;
    private AbilityButton[] _abilityBtns = new AbilityButton[8];
    private readonly bool[] _configured = new bool[8];
    private Label _reticlePrompt = null!;
    private VirtualJoystick _joystick = null!;
    private HFlowContainer _weaponRow = null!;
    private Button _autoBtn = null!;
    private bool _lastAutoFire, _lastAutopilotOn;

    private ColorRect _draftDim = null!;
    private CenterContainer _draftCenter = null!;
    private PanelContainer _draftPanel = null!;
    private HBoxContainer _draftCards = null!;
    private Label _draftHeader = null!;
    private readonly System.Collections.Generic.Dictionary<string, Texture2D> _cardTex = new();
    private readonly System.Collections.Generic.Dictionary<string, Texture2D?> _cardArt = new();

    private ColorRect _endDim = null!;
    private PanelContainer _endCard = null!;
    private VBoxContainer _endBody = null!;
    private bool _endBuilt;

    private ProgressBar _bossBar = null!;
    private Label _banner = null!;
    private float _bannerTime;

    private HBoxContainer _dropToast = null!;
    private TextureRect _dropPlate = null!, _dropIcon = null!;
    private Label _dropName = null!, _dropText = null!;
    private int _dropShown = -1;

    public override void _Ready()
    {
        Layer = 10;

        // Movement joystick FIRST — Godot gives input priority to the frontmost
        // (later-added) sibling in an overlap, so every button/panel added below
        // this one correctly steals its own taps instead of the joystick's big
        // touch zone swallowing them (this was the "can't press 1x/etc" bug).
        // Right side, per user preference — thumb rests near the bottom-right.
        _joystick = new VirtualJoystick
        {
            AnchorLeft = 0.45f, AnchorRight = 1f, AnchorTop = 0f, AnchorBottom = 1f,
            // Bottom bound matches GameRoot.BottomReserve exactly (not a separately-tuned
            // number) so the joystick's drag zone can never creep over the weapon/ability
            // card panel again if that panel's size changes — it used to be a hardcoded
            // -300 that quietly assumed a specific card size and broke (visibly overlapping
            // the cards) the moment the cards grew.
            OffsetTop = 230, OffsetBottom = -Sentinel.Game.GameRoot.BottomReserve,
        };
        _joystick.OnMove = d => Root.HeroJoystick(d);
        AddChild(_joystick);

        var top = new VBoxContainer { AnchorRight = 1f, OffsetLeft = 10, OffsetTop = 8, OffsetRight = -10 };
        _topBox = top;
        top.AddThemeConstantOverride("separation", 3);
        AddChild(top);

        // planet integrity — the primary bar, with the value drawn on it
        _integrity = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 44) };
        _integrity.AddThemeStyleboxOverride("fill", Filled(new Color(0.30f, 0.85f, 0.55f), 5));
        _integrity.AddThemeStyleboxOverride("background", Filled(new Color(0.05f, 0.03f, 0.04f, 0.85f), 5));
        top.AddChild(_integrity);

        // commander level + XP bar (fills each level, pops an upgrade card)
        _xpBar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 0, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 18) };
        _xpBar.AddThemeStyleboxOverride("fill", Filled(UiTheme.Accent, 4));
        _xpBar.AddThemeStyleboxOverride("background", Filled(new Color(0.03f, 0.04f, 0.07f, 0.85f), 4));
        top.AddChild(_xpBar);

        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _status.AddThemeFontSizeOverride("font_size", 36);
        top.AddChild(_status);

        // update-available badge — mirrors the menu card's state so a run in progress
        // (survival holds can run long) still surfaces it instead of only at launch
        _updateBadge = new Button
        {
            Text = "⇩", CustomMinimumSize = new Vector2(96, 96),
            AnchorLeft = 1f, AnchorRight = 1f, OffsetLeft = -106, OffsetRight = -10, OffsetTop = 8,
            TooltipText = "Update available — tap to download",
            Visible = Sentinel.Meta.UpdateChecker.Available,
        };
        _updateBadge.AddThemeFontSizeOverride("font_size", 44);
        StyleTopButton(_updateBadge, new Color(1f, 0.75f, 0.3f));
        _updateBadge.Pressed += () => Sentinel.Meta.UpdateChecker.PromptInstall(this);
        AddChild(_updateBadge);

        // speed + pause + leave + build row.
        // Box width must cover the actual summed content width (buttons + separation) —
        // an HBoxContainer isn't clipped to its anchor box, so an undersized box here
        // made the row spill past its right edge and read as off-centre.
        var ctl = new HBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -520, OffsetTop = 96, OffsetRight = 520,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        _ctlRow = ctl;
        ctl.Theme = UiTheme.Instance;
        ctl.AddThemeConstantOverride("separation", 12);
        AddChild(ctl);
        _menuOpen = new Button { Text = "☰", CustomMinimumSize = new Vector2(120, 104) };
        _menuOpen.AddThemeFontSizeOverride("font_size", 40);
        _menuOpen.Pressed += () => { if (!Root.IsPaused) { Root.TogglePause(); _pause.Text = "▶"; } };
        StyleTopButton(_menuOpen, UiTheme.Accent);
        ctl.AddChild(_menuOpen);
        _pause = new Button { Text = "❚❚", CustomMinimumSize = new Vector2(120, 104) };
        _pause.AddThemeFontSizeOverride("font_size", 36);
        _pause.Pressed += () => { Root.TogglePause(); _pause.Text = Root.IsPaused ? "▶" : "❚❚"; };
        StyleTopButton(_pause, UiTheme.Accent);
        ctl.AddChild(_pause);
        // One speed button showing the CURRENT speed; the other three live in a little
        // drop-down under it (tap the caret), which closes when you tap anywhere else.
        // Four always-visible speed buttons were most of why this row was so crowded.
        _speedMain = new Button { Text = "1x  ▾", CustomMinimumSize = new Vector2(150, 104) };
        _speedMain.AddThemeFontSizeOverride("font_size", 33);
        _speedMain.Pressed += () => ToggleSpeedMenu(!_speedMenu.Visible);
        StyleTopButton(_speedMain, UiTheme.Accent);
        ctl.AddChild(_speedMain);
        // Single master AUTO toggle — drives both ship weapon auto-fire AND autopilot
        // movement together (used to be two separate buttons, ✈ + AUTO, which read as
        // unclear/redundant since both are "automate the commander").
        _autoBtn = new Button { Text = "AUTO", CustomMinimumSize = new Vector2(140, 104), ToggleMode = true, ButtonPressed = true };
        _autoBtn.AddThemeFontSizeOverride("font_size", 26);
        _autoBtn.TooltipText = "Auto mode — the ship auto-fires its weapons and autopilots toward threats. Tap to switch to manual (steer with the joystick, tap an enemy to focus-fire).";
        _autoBtn.Pressed += () =>
        {
            bool goingOn = !_lastAutoFire;
            Root.RequestToggleAutoFire();
            if (_lastAutopilotOn != goingOn) Root.RequestToggleAutopilot();
        };
        StyleTopButton(_autoBtn, new Color(0.5f, 0.95f, 0.6f));
        ctl.AddChild(_autoBtn);

        // speed drop-down: a full-screen catcher (so a tap anywhere dismisses it) plus the
        // little column of the other speeds, positioned under the speed button each time
        // it opens.
        _speedCatcher = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _speedCatcher.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _speedCatcher.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true })
                ToggleSpeedMenu(false);
        };
        AddChild(_speedCatcher);

        _speedMenu = new VBoxContainer { Visible = false };
        _speedMenu.AddThemeConstantOverride("separation", 6);
        _speedMenu.Theme = UiTheme.Instance;
        AddChild(_speedMenu);
        for (int i = 0; i < 4; i++)
        {
            int mult = i + 1;
            var btn = new Button { Text = $"{mult}x", CustomMinimumSize = new Vector2(150, 92) };
            btn.AddThemeFontSizeOverride("font_size", 30);
            btn.Pressed += () => { Root.SetSpeed(mult); UpdateSpeedButtons(mult); ToggleSpeedMenu(false); };
            StyleTopButton(btn, UiTheme.Accent);
            _speedMenu.AddChild(btn);
            _speed[i] = btn;
        }
        UpdateSpeedButtons(1);

        // boss bar
        _bossBar = new ProgressBar
        {
            AnchorLeft = 0.1f, AnchorRight = 0.9f, OffsetTop = 138, CustomMinimumSize = new Vector2(0, 32),
            MinValue = 0, MaxValue = 1, Value = 1, ShowPercentage = false, Visible = false,
        };
        _bossBar.AddThemeColorOverride("font_color", new Color(1, 0.4f, 0.4f));
        AddChild(_bossBar);

        // ---- build panel ----
        _buildPanel = MakeBottomPanel();
        _buildPanel.OffsetTop = -300;   // just the prep blurb + BEGIN DEFENSE now
        AddChild(_buildPanel);
        var bv = new VBoxContainer();
        _buildPanel.AddChild(bv);
        _wavePreview = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1f, 0.85f, 0.5f) };
        _wavePreview.AddThemeFontSizeOverride("font_size", 34);
        bv.AddChild(_wavePreview);
        _launch = new Button { Text = "▶   BEGIN DEFENSE", CustomMinimumSize = new Vector2(0, 124) };
        _launch.AddThemeFontSizeOverride("font_size", 44);
        _launch.AddThemeColorOverride("font_color", new Color(0.6f, 1f, 0.7f));
        _launch.Pressed += () => Root.RequestLaunchWave();
        bv.AddChild(_launch);

        // ---- wave panel ----
        _wavePanel = MakeBottomPanel();
        _wavePanel.OffsetTop = -500;   // weapon row + ability bar + hint — cards are 204/219px (1.5x)
        // lighter than the default theme panel — a soft backing, not a solid blue box
        _wavePanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.09f, 0.55f),
            BorderColor = new Color(UiTheme.Accent, 0.10f),
            BorderWidthTop = 1,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
        });
        AddChild(_wavePanel);
        var wv = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _waveBody = wv;
        wv.AddThemeConstantOverride("separation", 10);
        _wavePanel.AddChild(wv);
        // flow (not a plain HBox) so the planet battery + every active sentinel can sit
        // alongside the ship-weapon cards and wrap to a second line instead of overflowing
        _weaponRow = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
        _weaponRow.AddThemeConstantOverride("h_separation", 8);
        _weaponRow.AddThemeConstantOverride("v_separation", 8);
        wv.AddChild(_weaponRow);
        var abRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        abRow.AddThemeConstantOverride("separation", 14);
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
        hint.AddThemeFontSizeOverride("font_size", 28);
        wv.AddChild(hint);

        // big centred targeting prompt (over the play area)
        _reticlePrompt = new Label
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0.5f,
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1f, 0.92f, 0.4f), Text = "",
        };
        _reticlePrompt.AddThemeFontSizeOverride("font_size", 52);
        AddChild(_reticlePrompt);

        // ---- weapon upgrade draft (centred popup with card art) ----
        _draftDim = new ColorRect { Color = new Color(0.01f, 0.02f, 0.04f, 0.72f), Visible = false };
        _draftDim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _draftDim.MouseFilter = Control.MouseFilterEnum.Stop;   // eat taps behind the popup
        AddChild(_draftDim);

        // A CenterContainer over the whole screen keeps the popup centred on both axes
        // whatever size it ends up; the panel itself shrinks to its content rather than
        // being pinned to fixed offsets (which used to push the card row off-screen to
        // the right as soon as the draft offered four options).
        var draftCenter = new CenterContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        draftCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(draftCenter);
        _draftPanel = new PanelContainer();
        draftCenter.AddChild(_draftPanel);
        _draftCenter = draftCenter;
        var dv = new VBoxContainer();
        dv.AddThemeConstantOverride("separation", 12);
        _draftPanel.AddChild(dv);
        _draftHeader = new Label
        {
            Text = "WEAPONS UPGRADE", HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _draftHeader.AddThemeFontOverride("font", UiTheme.Display);
        _draftHeader.AddThemeFontSizeOverride("font_size", 36);
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
            OffsetLeft = 22, OffsetRight = -22, OffsetTop = -520, OffsetBottom = 520, Visible = false,
        };
        AddChild(_endCard);
        _endBody = new VBoxContainer();
        _endBody.AddThemeConstantOverride("separation", 12);
        _endCard.AddChild(_endBody);

        // ---- banner ----
        _banner = new Label { AnchorRight = 1f, OffsetTop = 156, HorizontalAlignment = HorizontalAlignment.Center };
        _banner.AddThemeFontSizeOverride("font_size", 64);
        AddChild(_banner);

        // ---- item drop toast ----
        // Sits just under the banner line. PDTD's own rarity plate is the backdrop and the
        // loot icon rides on top of it, so a drop reads at a glance by colour alone.
        _dropToast = new HBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -300, OffsetRight = 300,
            OffsetTop = 250, Alignment = BoxContainer.AlignmentMode.Center,
            Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _dropToast.AddThemeConstantOverride("separation", 12);
        AddChild(_dropToast);

        _dropPlate = new TextureRect
        {
            CustomMinimumSize = new Vector2(84, 84), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _dropIcon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _dropIcon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _dropIcon.OffsetLeft = 12; _dropIcon.OffsetRight = -12;
        _dropIcon.OffsetTop = 12; _dropIcon.OffsetBottom = -12;
        _dropPlate.AddChild(_dropIcon);
        _dropToast.AddChild(_dropPlate);

        var dropCol = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        dropCol.AddThemeConstantOverride("separation", 0);
        _dropToast.AddChild(dropCol);
        _dropName = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _dropName.AddThemeFontOverride("font", UiTheme.Display);
        _dropName.AddThemeFontSizeOverride("font_size", 32);
        dropCol.AddChild(_dropName);
        _dropText = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _dropText.AddThemeFontSizeOverride("font_size", 24);
        _dropText.Modulate = new Color(1, 1, 1, 0.85f);
        dropCol.AddChild(_dropText);

        // ---- pause / leave menu ----
        _pauseMenu = new PanelContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -400, OffsetRight = 400, OffsetTop = -300, OffsetBottom = 300, Visible = false,
        };
        AddChild(_pauseMenu);
        var pm = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        pm.AddThemeConstantOverride("separation", 16);
        _pauseMenu.AddChild(pm);
        var pmTitle = new Label { Text = "PAUSED", HorizontalAlignment = HorizontalAlignment.Center };
        pmTitle.AddThemeFontSizeOverride("font_size", 52);
        pm.AddChild(pmTitle);
        var pmResume = new Button { Text = "▶   Resume", CustomMinimumSize = new Vector2(640, 128), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        pmResume.AddThemeFontSizeOverride("font_size", 42);
        pmResume.Pressed += () => { if (Root.IsPaused) { Root.TogglePause(); _pause.Text = "❚❚"; } };
        pm.AddChild(pmResume);
        var pmLeave = new Button { Text = "◄   Leave to Main Menu", CustomMinimumSize = new Vector2(640, 128), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        pmLeave.AddThemeFontSizeOverride("font_size", 38);
        pmLeave.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.55f));
        pmLeave.Pressed += () => Root.GoToMenu();
        pm.AddChild(pmLeave);
        var pmHint = new Label { Text = "leaving forfeits this run's rewards", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.45f) };
        pmHint.AddThemeFontSizeOverride("font_size", 24);
        pm.AddChild(pmHint);

        foreach (var n in new Control[] { _topBox, _buildPanel, _wavePanel, _draftPanel, _endCard, _pauseMenu })
            n.Theme = UiTheme.Instance;

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
        if (_ctlRow != null) _ctlRow.OffsetTop = top + 182;   // clears _topBox (44px integrity + 18px xp + a 2-line 36px status)
        foreach (var p in _panels) { p.OffsetLeft = m + 8; p.OffsetRight = -(m + 8); }
        if (_bossBar != null)
        {
            _bossBar.AnchorLeft = 0f; _bossBar.AnchorRight = 1f;
            _bossBar.OffsetLeft = m + 40; _bossBar.OffsetRight = -(m + 40);
            _bossBar.OffsetTop = top + 174;
        }
        if (_banner != null) _banner.OffsetTop = top + 194;
    }


    // ---- called by GameRoot ----
    public void SyncSpeed(int s) => UpdateSpeedButtons(s);

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
        _updateBadge.Visible = Sentinel.Meta.UpdateChecker.Available;

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
        BuildOpen = !ended && !draft && prep;

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
        if (_wavePanel.Visible)
        {
            // hug the content: a short or empty row shouldn't reserve the full panel height
            float need = Mathf.Clamp(_waveBody.GetCombinedMinimumSize().Y + 26f, 120f, GameRoot.BottomReserve);
            _wavePanel.OffsetTop = -need;
            _joystick.OffsetBottom = -need;
        }
        _endCard.Visible = ended;
        _endDim.Visible = ended;
        _draftCenter.Visible = draft;
        _draftDim.Visible = draft;
        _autoBtn.Visible = fighting && !draft;
        // the merged button reflects auto-fire's on/off state; autopilot is tracked
        // alongside it (not shown separately) purely so the Pressed handler above knows
        // whether it also needs to flip autopilot to keep the two in lockstep.
        if (_autoBtn.ButtonPressed != w.HeroAutoFire) _autoBtn.ButtonPressed = w.HeroAutoFire;
        _autoBtn.Text = w.HeroAutoFire ? "AUTO" : "MANUAL";
        _lastAutoFire = w.HeroAutoFire;
        _lastAutopilotOn = w.HeroAutopilotOn;
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
                ? (prep ? "The hold begins when you're ready." : "")
                : "NEXT: " + w.NextWavePreview();
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

        RefreshDropToast(w, fighting && !draft);

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
        h.AddThemeFontSizeOverride("font_size", 52);
        h.AddThemeColorOverride("font_color", accent);
        _endBody.AddChild(h);

        int secs = Mathf.FloorToInt(w.PhaseTimer);
        string result = won
            ? $"Held the full {Mathf.FloorToInt(w.Mission.Duration) / 60}:00."
            : $"Fell at {secs / 60}:{secs % 60:00}.";
        var sub = new Label { Text = result, HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.8f) };
        sub.AddThemeFontSizeOverride("font_size", 30);
        _endBody.AddChild(sub);

        var rHdr = new Label { Text = won ? "REWARDS   ·   ×2 VICTORY BONUS" : "REWARDS", HorizontalAlignment = HorizontalAlignment.Center };
        rHdr.AddThemeFontSizeOverride("font_size", 26);
        rHdr.AddThemeColorOverride("font_color", won ? new Color(1f, 0.9f, 0.5f) : new Color(1, 1, 1, 0.55f));
        _endBody.AddChild(rHdr);

        void Reward(string icon, string label, string val, Color col)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            var chip = new PanelContainer { CustomMinimumSize = new Vector2(76, 76) };
            chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(col, 0.16f), BorderColor = new Color(col, 0.6f),
                BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = 9, CornerRadiusTopRight = 9, CornerRadiusBottomLeft = 9, CornerRadiusBottomRight = 9,
            });
            var ic = new Label { Text = icon, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            ic.AddThemeFontSizeOverride("font_size", 38);
            ic.AddThemeColorOverride("font_color", col);
            chip.AddChild(ic);
            row.AddChild(chip);
            var a = new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
            a.AddThemeFontSizeOverride("font_size", 36);
            var b = new Label { Text = val, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            b.AddThemeFontOverride("font", UiTheme.Display);
            b.AddThemeFontSizeOverride("font_size", 46);
            b.AddThemeColorOverride("font_color", col);
            row.AddChild(a); row.AddChild(b);
            _endBody.AddChild(row);
        }
        Reward("✦", "Experience", $"+{Mathf.FloorToInt(w.XpEarned)}", new Color(0.85f, 0.9f, 1f));
        Reward("◇", "Research Data", $"+{Mathf.FloorToInt(w.ResearchDataEarned)}", UiTheme.Accent);
        Reward("✷", "Sentinel Cores", $"+{w.CoresEarned}", new Color(0.72f, 0.86f, 1f));
        if (w.AlloyEarned > 0) Reward("❖", "Exotic Alloy", $"+{w.AlloyEarned}", new Color(0.95f, 0.78f, 0.42f));

        var s = w.Stats;
        float tot = Mathf.Max(1f, s.TotalDamage);
        var dmg = new Label
        {
            Text = $"Kills {s.EnemiesKilled}   ·   turrets {Pct(s.DamageByTurrets, tot)}  ship {Pct(s.DamageByHero, tot)}  orbital {Pct(s.DamageByOrbital, tot)}  abilities {Pct(s.DamageByAbilities, tot)}",
            HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.5f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        dmg.AddThemeFontSizeOverride("font_size", 24);
        _endBody.AddChild(dmg);

        var btns = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btns.AddThemeConstantOverride("separation", 14);
        _endBody.AddChild(btns);
        var retry = new Button { Text = "↻  Retry", CustomMinimumSize = new Vector2(360, 124) };
        retry.AddThemeFontSizeOverride("font_size", 36);
        retry.Pressed += () => Root.RestartMission();
        btns.AddChild(retry);
        var menu = new Button { Text = won ? "Continue ▸" : "Menu", CustomMinimumSize = new Vector2(360, 124) };
        menu.AddThemeFontSizeOverride("font_size", 36);
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

    /// <summary>PDTD's own illustration for a weapon, where the game ships one. The seven
    /// skill glyphs cover the weapons PDTD and Beyond share by name; the techpoint emblems
    /// cover the rest of the sentinel roster.</summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> PdtdCardArt = new()
    {
        ["radiation_line"] = "skillicon/radiation_line",
        ["waterdrop"] = "skillicon/waterdrop",
        ["laser"] = "skillicon/laser",
        ["beam"] = "skillicon/beam",
        ["space_bomb"] = "skillicon/space_bomb",
        ["missile_barrage"] = "skillicon/missile",
        ["shock_orb"] = "techpoint/balllightning",
        ["orbital_lightning"] = "techpoint/chainlightning",
        ["radiation_zone"] = "techpoint/radiationzone",
        ["force_field"] = "techpoint/gravitynova",
    };

    /// <summary>What goes in a card's art window. PDTD's own art when there is some,
    /// otherwise the illustration cropped out of this project's generated card jpg — the
    /// jpg already carries its own painted frame, so dropping the whole thing into PDTD's
    /// frame would nest one border inside another. These crop bounds match where the
    /// illustration sits in every card in assets/game/cards/.</summary>
    private Texture2D? CardArtTexture(string id)
    {
        if (_cardArt.TryGetValue(id, out var cached)) return cached;

        Texture2D? tex = null;
        if (PdtdCardArt.TryGetValue(id, out string? pdtdPath))
            tex = Render.Art.Pdtd(pdtdPath);

        if (tex == null)
        {
            var full = CardTexture(id);
            if (full != null)
            {
                var sz = full.GetSize();
                tex = new AtlasTexture
                {
                    Atlas = full,
                    Region = new Rect2(sz.X * 0.07f, sz.Y * 0.17f, sz.X * 0.86f, sz.Y * 0.42f),
                };
            }
        }
        _cardArt[id] = tex;
        return tex;
    }

    /// <summary>What taking this Sentinel card actually gives you, worded like PDTD's own
    /// cards. Level 0 is the unlock and gets PDTD's "Release a X Sentinel to deal [Type]
    /// DMG" phrasing; every level after states the real deltas computed off the def, so
    /// the card can never drift out of sync with the numbers in data/.</summary>
    private static string OrbitalUpgradeText(Config.OrbitalWeaponDef d, int lvl)
    {
        if (lvl <= 0)
        {
            string dmgType = d.Kind switch
            {
                "laser" or "beam_laser" => "Energy",
                "rad_line" or "rad_zone" => "Radiation",
                "lightning" or "shock_orb" => "Electric",
                "space_bomb" or "force_field" => "Gravity",
                _ => "Physical",
            };
            return $"Release a {d.Name} Sentinel to deal [{dmgType}] DMG";
        }

        var parts = new System.Collections.Generic.List<string>();

        float cur = d.Damage + d.DamagePerLevel * (lvl - 1);
        if (d.DamagePerLevel != 0f && cur > 0.01f)
            parts.Add($"DMG +{d.DamagePerLevel / cur * 100f:0}%");

        // cooldown_per_level is negative (faster); show it as a cooldown-speed gain, which
        // is how PDTD words it, and respect min_cooldown so a maxed weapon doesn't claim a
        // speed-up it can't actually take.
        float cdCur = Mathf.Max(d.MinCooldown, d.Cooldown + d.CooldownPerLevel * (lvl - 1));
        float cdNext = Mathf.Max(d.MinCooldown, d.Cooldown + d.CooldownPerLevel * lvl);
        if (cdNext < cdCur - 0.001f && cdNext > 0.01f)
            parts.Add($"cooldown speed +{(cdCur / cdNext - 1f) * 100f:0}%");

        if (d.CountPerLevel > 0f)
        {
            int before = Mathf.FloorToInt(d.CountPerLevel * (lvl - 1));
            int after = Mathf.FloorToInt(d.CountPerLevel * lvl);
            if (after > before) parts.Add("+1 target");
        }
        if (d.RadiusPerLevel != 0f && d.Radius > 0.01f)
            parts.Add($"radius +{d.RadiusPerLevel / d.Radius * 100f:0}%");
        if (d.DurationPerLevel != 0f && d.Duration > 0.01f)
            parts.Add($"duration +{d.DurationPerLevel / d.Duration * 100f:0}%");

        return parts.Count == 0 ? $"{d.Name} improves" : string.Join("\n", parts);
    }

    /// <summary>Same idea for the ship's own weapons.</summary>
    private static string HeroUpgradeText(Config.HeroWeaponDef d, int lvl)
    {
        if (lvl <= 0) return $"Equip {d.Name}";

        var parts = new System.Collections.Generic.List<string>();
        float cur = d.Damage + d.DamagePerLevel * (lvl - 1);
        if (d.DamagePerLevel != 0f && cur > 0.01f)
            parts.Add($"DMG +{d.DamagePerLevel / cur * 100f:0}%");

        float cdCur = Mathf.Max(d.MinCooldown, d.Cooldown + d.CooldownPerLevel * (lvl - 1));
        float cdNext = Mathf.Max(d.MinCooldown, d.Cooldown + d.CooldownPerLevel * lvl);
        if (cdNext < cdCur - 0.001f && cdNext > 0.01f)
            parts.Add($"cooldown speed +{(cdCur / cdNext - 1f) * 100f:0}%");

        return parts.Count == 0 ? $"{d.Name} improves" : string.Join("\n", parts);
    }

    private static Color HexColor(string hex, Color fallback)
    {
        try { return new Color(hex); } catch { return fallback; }
    }

    private int _draftShownHash = -1;
    private void RefreshDraft(SimWorld w)
    {
        // A mini-boss payout deals its whole hand face-up and may let you take several,
        // so it gets its own header telling you how many picks are left.
        _draftHeader.Text = w.IsBossReward
            ? $"MINI-BOSS SPOILS  ·  TAKE {w.BossRewardPicksLeft}"
            : $"WEAPONS UPGRADE  ·  LEVEL {w.RunLevel}";
        _draftHeader.AddThemeColorOverride("font_color", w.IsBossReward ? new Color(1f, 0.78f, 0.32f) : UiTheme.Accent);

        int hash = 17;
        foreach (int i in w.DraftOptionIndices) hash = hash * 31 + i;
        hash = hash * 31 + w.RunCards.Count;
        hash = hash * 31 + w.BossRewardPicksLeft;
        if (hash == _draftShownHash) return;
        _draftShownHash = hash;
        Sentinel.Audio.AudioManager.Instance?.Play("card_reveal");

        foreach (Node c in _draftCards.GetChildren()) c.QueueFree();

        // Size the cards to the screen rather than to a fixed 220x690 — with four
        // options that fixed width overflowed a phone's width and the HBox, which can't
        // shrink below its children's minimum size, pushed the right-hand cards off the
        // display entirely. Work out what each card may occupy from the actual viewport.
        int n = Mathf.Max(1, w.DraftOptionIndices.Count);
        var vp = GetViewport().GetVisibleRect().Size;
        // PDTD deals three cards, not four, and they fill most of the screen's width —
        // that's what makes the art readable. Beyond's were capped at 260px and squeezed
        // by a four-wide hand. Tighter margins plus a higher cap get them to PDTD's scale.
        const float sep = 10f, sideMargin = 16f;
        float cardW = Mathf.Clamp((vp.X - sideMargin - sep * (n - 1)) / n, 120f, 340f);
        // keep the card's portrait proportions, but never taller than the space left
        // under the header inside the dimmed screen
        float cardH = Mathf.Min(cardW * 3.14f, vp.Y - 190f);
        float artH = cardH * 0.63f;
        float k = cardW / 220f;   // font scale, so text shrinks with the card

        int cardPos = 0;
        foreach (int idx in w.DraftOptionIndices)
        {
            int myPos = cardPos++;
            bool boost = w.IsBoostCard(idx);
            bool orbital = w.IsOrbitalCard(idx);
            int wi = w.CardWeaponIndex(idx);
            string cardId, cardName, accent;
            int lvl;
            string subtitle;
            string boostText = "";
            if (boost)
            {
                var bc = w.BoostCard(idx);
                cardId = bc?.Id ?? "boost"; cardName = bc?.Name ?? "Boost"; accent = bc?.Accent ?? "#4fd6de";
                lvl = 0;
                subtitle = "BOOST";
                boostText = bc?.Text ?? "";
            }
            else if (orbital)
            {
                var o = w.Cfg.OrbitalWeapons[wi];
                cardId = o.Id; cardName = o.Name; accent = o.Accent;
                lvl = w.OrbitalWeaponLevel(wi);
                subtitle = "SENTINEL";
                boostText = OrbitalUpgradeText(o, lvl);
            }
            else
            {
                var hh = w.Cfg.HeroWeapons[wi];
                cardId = hh.Id; cardName = hh.Name; accent = hh.Accent;
                lvl = w.HeroWeaponLevel(wi);
                subtitle = "SHIP";
                boostText = HeroUpgradeText(hh, lvl);
            }
            var col = HexColor(accent, UiTheme.Accent);
            int i2 = idx;

            var btn = new Button
            {
                CustomMinimumSize = new Vector2(cardW, cardH),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                ClipContents = true,
                PivotOffset = new Vector2(cardW * 0.5f, cardH * 0.5f),
                Scale = new Vector2(0.7f, 0.7f),
                Modulate = new Color(1, 1, 1, 0f),
            };
            // Tier picks which of PDTD's card frames to wear: a plain level-up gets the
            // teal "normal" plate, unlocking a brand-new system gets the purple "super"
            // one, and a boost card gets the gold "ultimate" one — the same escalation
            // PDTD uses for its own skill cards.
            string tier = boost ? "ultimate" : (lvl == 0 ? "super" : "normal");
            var plate = Render.Art.Pdtd("cardui/card_back");
            var front = Render.Art.Pdtd($"cardui/card_front_{tier}");
            var outline = Render.Art.Pdtd($"cardui/card_outline_{tier}");
            bool pdtdFrame = plate != null && front != null && outline != null;

            if (!pdtdFrame)
            {
                // PDTD art not extracted — fall back to the drawn frame
                btn.AddThemeStyleboxOverride("normal", CardBox(col, 0.12f));
                btn.AddThemeStyleboxOverride("hover", CardBox(col, 0.30f));
                btn.AddThemeStyleboxOverride("pressed", CardBox(col, 0.40f));
                btn.AddThemeStyleboxOverride("focus", CardBox(col, 0.30f));
            }
            else
            {
                var blank = new StyleBoxEmpty();
                foreach (string st in new[] { "normal", "hover", "pressed", "focus" })
                    btn.AddThemeStyleboxOverride(st, blank);
            }

            btn.Pressed += () =>
            {
                // a quick confirm punch before the popup clears and the sim resumes
                var t2 = btn.CreateTween();
                t2.TweenProperty(btn, "scale", new Vector2(1.08f, 1.08f), 0.07f);
                Root.RequestPickCard(i2); _draftShownHash = -1;
            };

            // --- layers, back to front: plate, art, frame, outline ---
            // The frame's own art window is transparent, so the illustration is placed
            // where that window sits (12.5%-57.6% of the card's height, measured off
            // card_front_normal's alpha) and simply shows through it.
            if (pdtdFrame)
            {
                var backRect = new TextureRect
                {
                    Texture = plate, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                backRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                btn.AddChild(backRect);

                // The techpoint emblems are badges on transparent backing — cropping them
                // to fill would cut the badge in half. Everything else (PDTD's own
                // full-bleed skill art, and the crops out of this project's card jpgs) is
                // a picture meant to fill the window.
                bool emblem = PdtdCardArt.TryGetValue(cardId, out string? ap) && ap.StartsWith("techpoint/");
                var window = new TextureRect
                {
                    Texture = CardArtTexture(cardId),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = emblem ? TextureRect.StretchModeEnum.KeepAspectCentered
                                         : TextureRect.StretchModeEnum.KeepAspectCovered,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    ClipContents = true,
                    // Inset to the frame's inner window — full-bleed art was spilling a
                    // few pixels past the card's rounded edge onto its neighbour.
                    AnchorLeft = 0.045f, AnchorRight = 0.955f, AnchorTop = 0.125f, AnchorBottom = 0.576f,
                };
                btn.AddChild(window);

                foreach (var lay in new[] { front, outline })
                {
                    var r = new TextureRect
                    {
                        Texture = lay, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.Scale,
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    };
                    r.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                    btn.AddChild(r);
                }
            }

            // --- text, in the frame's lower band ---
            var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            v.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            v.OffsetLeft = 8; v.OffsetRight = -8;
            v.OffsetTop = pdtdFrame ? cardH * 0.60f : 6f;
            v.OffsetBottom = pdtdFrame ? -cardH * 0.05f : -6f;
            v.AddThemeConstantOverride("separation", 6);
            btn.AddChild(v);

            if (!pdtdFrame)
            {
                var art = new TextureRect
                {
                    Texture = CardTexture(cardId),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    CustomMinimumSize = new Vector2(0, artH),
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    ClipContents = true,
                };
                v.AddChild(art);
            }

            var nm = new Label { Text = cardName.ToUpperInvariant(), HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            nm.AddThemeFontOverride("font", UiTheme.Display);
            nm.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(27 * k));
            nm.AddThemeColorOverride("font_color", col.Lightened(0.3f));
            v.AddChild(nm);

            var tag = new Label
            {
                Text = subtitle, HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            tag.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(17 * k));
            tag.AddThemeColorOverride("font_color", new Color(col, 0.7f));
            v.AddChild(tag);

            // Every card says what it DOES, the way PDTD's do ("Radiation Link DMG +60%").
            // "LV 1 → 2" told you a number changed without telling you which one or by how
            // much, which is useless at the moment you have to choose between three cards.
            var lv = new Label
            {
                Text = boostText,
                HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            lv.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(16 * k));
            lv.AddThemeColorOverride("font_color", lvl == 0 && !boost ? new Color(1f, 0.9f, 0.5f) : new Color(1, 1, 1, 0.85f));
            v.AddChild(lv);

            _draftCards.AddChild(btn);

            // staggered pop-in: each card scales up from small/transparent with a
            // slight overshoot, offset a beat after the one before it
            var t = btn.CreateTween();
            t.TweenInterval(myPos * 0.07f);
            t.TweenProperty(btn, "modulate:a", 1f, 0.16f);
            t.Parallel().TweenProperty(btn, "scale", new Vector2(1.06f, 1.06f), 0.16f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            t.TweenProperty(btn, "scale", Vector2.One, 0.08f);
        }
    }

    /// <summary>The bottom card row: the planet's missile battery, every active orbital
    /// sentinel, and the ship's own weapons — all with a live cooldown, PDTD-style. The
    /// battery and the sentinels auto-fire, so they're drawn as compact non-tappable chips;
    /// only the ship weapons are full-size tappable cards.</summary>
    private void RefreshWeaponRow(SimWorld w)
    {
        int n = w.HeroWeaponCount;
        int on = w.OrbitalWeaponCount;
        // (re)build the cards only when the set of unlocked weapons changes
        int sig = 0;
        for (int i = 0; i < n; i++) sig = sig * 7 + (w.HeroWeaponLevel(i) > 0 ? 1 : 0);
        for (int i = 0; i < on; i++) sig = sig * 7 + (w.OrbitalWeaponLevel(i) > 0 ? 1 : 0);
        if (sig != _weaponRowSig)
        {
            _weaponRowSig = sig;
            foreach (Node c in _weaponRow.GetChildren()) c.QueueFree();
            _weaponBtns.Clear();
            _orbitalBtns.Clear();

            // planet missile battery — always there, always firing
            _batteryBtn = new HeroWeaponButton();
            _batteryBtn.Configure("BATTERY", "missiles", new Color(0.95f, 0.72f, 0.35f), alwaysOn: true, compact: true);
            _weaponRow.AddChild(_batteryBtn);

            for (int i = 0; i < on; i++)
            {
                if (w.OrbitalWeaponLevel(i) <= 0) continue;
                var od = w.Cfg.OrbitalWeapons[i];
                var b = new HeroWeaponButton();
                b.Configure(ShortName(od.Name), od.Kind, HexColor(od.Accent, UiTheme.Accent), alwaysOn: true, compact: true);
                _weaponRow.AddChild(b);
                _orbitalBtns.Add((i, b));
            }

            for (int i = 0; i < n; i++)
            {
                if (w.HeroWeaponLevel(i) <= 0) continue;
                var def = w.Cfg.HeroWeapons[i];
                var col = HexColor(def.Accent, UiTheme.Accent);
                int i2 = i;
                var b = new HeroWeaponButton();
                b.Configure(ShortName(def.Name), def.Kind, col, def.AlwaysOn);
                if (!def.AlwaysOn) b.OnPress = () => Root.RequestFireWeapon(i2);
                _weaponRow.AddChild(b);
                _weaponBtns.Add((i, b));
            }
        }

        _batteryBtn?.SetState(1, w.BatteryCooldownLeft, w.BatteryInterval);

        foreach (var (i, b) in _orbitalBtns)
        {
            var od = w.Cfg.OrbitalWeapons[i];
            int lvl = w.OrbitalWeaponLevel(i);
            b.SetState(lvl, w.OrbitalWeaponCooldownLeft(i),
                       Mathf.Max(od.MinCooldown, od.Cooldown + od.CooldownPerLevel * (lvl - 1)));
        }

        foreach (var (i, b) in _weaponBtns)
        {
            var def = w.Cfg.HeroWeapons[i];
            int lvl = w.HeroWeaponLevel(i);
            float cd = w.HeroWeaponCooldownLeft(i);
            float cdMax = def.Cooldown + def.CooldownPerLevel * (lvl - 1);
            b.SetState(lvl, cd, cdMax);
        }
    }

    private int _weaponRowSig = -1;
    private HeroWeaponButton? _batteryBtn;
    private readonly System.Collections.Generic.List<(int idx, HeroWeaponButton btn)> _weaponBtns = new();
    private readonly System.Collections.Generic.List<(int idx, HeroWeaponButton btn)> _orbitalBtns = new();
    private static string ShortName(string n)
    {
        int sp = n.IndexOf(' ');
        return sp > 0 ? n[..sp].ToUpperInvariant() : n.ToUpperInvariant();
    }

    /// <summary>Armoured chip look for the top control row (speed/pause/build/auto) —
    /// same card-frame language as the ability buttons and the weapon-upgrade cards,
    /// so the always-visible battle controls actually read against the starfield.</summary>
    private static void StyleTopButton(Button b, Color accent)
    {
        StyleBoxFlat Box(float bgA, float borderA) => new()
        {
            BgColor = new Color(0.05f, 0.06f, 0.09f, 0.9f + bgA * 0.1f),
            BorderColor = new Color(accent, borderA),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
        };
        b.AddThemeStyleboxOverride("normal", Box(0f, 0.55f));
        b.AddThemeStyleboxOverride("hover", Box(0.15f, 0.85f));
        b.AddThemeStyleboxOverride("focus", Box(0.15f, 0.85f));
        var pressed = Box(0.3f, 1f); pressed.BgColor = new Color(accent, 0.30f);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.92f));
        b.AddThemeColorOverride("font_hover_color", Colors.White);
        b.AddThemeColorOverride("font_pressed_color", Colors.White);
        b.AddThemeColorOverride("font_focus_color", Colors.White);
    }

    private static StyleBoxFlat CardBox(Color accent, float bgA) => new()
    {
        BgColor = new Color(accent, bgA * 0.5f + 0.04f),
        BorderColor = new Color(accent, 0.55f + bgA),
        BorderWidthLeft = 4, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
        ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 4, ContentMarginBottom = 4,
    };


    /// <summary>Show the last item that dropped for a couple of seconds, on PDTD's own
    /// rarity plate. Fades out rather than snapping away.</summary>
    private void RefreshDropToast(SimWorld w, bool showable)
    {
        const float hold = 2.6f, fade = 0.6f;
        int idx = w.LastItemDrop;
        if (!showable || idx < 0 || idx >= w.Cfg.Items.Items.Count || w.LastItemDropAge > hold + fade)
        {
            _dropToast.Visible = false;
            return;
        }

        if (idx != _dropShown)
        {
            _dropShown = idx;
            var item = w.Cfg.Items.Items[idx];
            var rar = w.Cfg.Items.Rarity(item.Rarity);
            _dropPlate.Texture = Render.Art.Pdtd($"rarity/{item.Rarity}");
            _dropIcon.Texture = Render.Art.Pdtd($"loot/{item.Icon}");
            _dropName.Text = item.Name.ToUpperInvariant();
            _dropText.Text = item.Text;
            var col = HexColor(rar?.Color ?? "#9aa4b2", UiTheme.Accent);
            _dropName.AddThemeColorOverride("font_color", col.Lightened(0.25f));
        }

        _dropToast.Visible = true;
        float a = w.LastItemDropAge <= hold ? 1f : 1f - (w.LastItemDropAge - hold) / fade;
        _dropToast.Modulate = new Color(1, 1, 1, Mathf.Clamp(a, 0f, 1f));
    }

    private static string Pct(float v, float tot) => $"{Mathf.RoundToInt(v / tot * 100f)}%";
    private void UpdateSpeedButtons(int active)
    {
        _speedNow = active;
        if (_speedMain != null) _speedMain.Text = $"{active}x  ▾";
        for (int i = 0; i < 4; i++)
            if (_speed[i] != null) _speed[i].Modulate = (i + 1) == active ? Colors.White : new Color(1, 1, 1, 0.55f);
    }

    /// <summary>Show/hide the speed drop-down, parking it right under the speed button.</summary>
    private void ToggleSpeedMenu(bool show)
    {
        if (_speedMenu == null) return;
        if (show)
        {
            var r = _speedMain.GetGlobalRect();
            _speedMenu.Position = new Vector2(r.Position.X, r.End.Y + 8f);
        }
        _speedMenu.Visible = show;
        _speedCatcher.Visible = show;
        if (show) MoveChild(_speedMenu, GetChildCount() - 1);   // above the catcher
    }

    private SimRenderer GetRenderer()
    {
        foreach (Node c in Root.GetChildren()) if (c is SimRenderer r) return r;
        return null!;
    }
}
