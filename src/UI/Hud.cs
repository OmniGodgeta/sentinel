using System.Text;
using Godot;
using Sentinel.Game;
using Sentinel.Sim;
using Sentinel.Render;

namespace Sentinel.UI;

/// <summary>
/// Screen-space HUD, built in code (greybox). Top status strip, speed control,
/// and a bottom panel that swaps between build tools and the ability bar.
/// </summary>
public sealed partial class Hud : CanvasLayer
{
    public GameRoot Root = null!;

    private Label _status = null!;
    private ProgressBar _integrity = null!;
    private Button[] _speed = new Button[4];

    private PanelContainer _buildPanel = null!;
    private Label _slotLabel = null!;
    private HBoxContainer _turretRow = null!;
    private Button _launch = null!;

    private PanelContainer _wavePanel = null!;
    private Button[] _abilityBtns = new Button[8];
    private Label _reticlePrompt = null!;

    private PanelContainer _endCard = null!;
    private Label _endText = null!;
    private Button _endButton = null!;

    private Label _banner = null!;
    private float _bannerTime;

    private int _selectedSlot = -1;

    public override void _Ready()
    {
        Layer = 10;

        // ---------- top status ----------
        var top = new VBoxContainer
        {
            AnchorRight = 1f,
            OffsetLeft = 10, OffsetTop = 6, OffsetRight = -10,
        };
        AddChild(top);

        _status = new Label { Text = "—", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.Off };
        _status.AddThemeFontSizeOverride("font_size", 13);
        top.AddChild(_status);

        _integrity = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) };
        top.AddChild(_integrity);

        // ---------- speed control (its own row under the status block) ----------
        var speedBox = new HBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f,
            OffsetLeft = -108, OffsetTop = 58, OffsetRight = 108,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        AddChild(speedBox);
        for (int i = 0; i < 4; i++)
        {
            int mult = i + 1;
            var btn = new Button { Text = $"{mult}x", CustomMinimumSize = new Vector2(50, 30), ToggleMode = true };
            btn.AddThemeFontSizeOverride("font_size", 13);
            btn.Pressed += () => { Root.SetSpeed(mult); UpdateSpeedButtons(mult); };
            speedBox.AddChild(btn);
            _speed[i] = btn;
        }
        UpdateSpeedButtons(1);

        // ---------- bottom: build panel ----------
        _buildPanel = MakeBottomPanel();
        AddChild(_buildPanel);
        var bv = new VBoxContainer();
        _buildPanel.AddChild(bv);

        _slotLabel = new Label { Text = "Tap a turret slot", HorizontalAlignment = HorizontalAlignment.Center };
        bv.AddChild(_slotLabel);

        _turretRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        bv.AddChild(_turretRow);

        _launch = new Button { Text = "▶  LAUNCH WAVE", CustomMinimumSize = new Vector2(0, 48) };
        _launch.AddThemeFontSizeOverride("font_size", 18);
        _launch.Pressed += () => Root.RequestLaunchWave();
        bv.AddChild(_launch);

        // ---------- bottom: wave panel ----------
        _wavePanel = MakeBottomPanel();
        AddChild(_wavePanel);
        var wv = new VBoxContainer();
        _wavePanel.AddChild(wv);

        _reticlePrompt = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1f, 0.9f, 0.4f) };
        wv.AddChild(_reticlePrompt);

        var abRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        wv.AddChild(abRow);
        for (int i = 0; i < _abilityBtns.Length; i++)
        {
            int slot = i;
            var btn = new Button { CustomMinimumSize = new Vector2(96, 60), Visible = false };
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.Pressed += () => Root.RequestAbility(slot);
            abRow.AddChild(btn);
            _abilityBtns[i] = btn;
        }
        var hint = new Label
        {
            Text = "drag: move ship   ·   tap: missile volley",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1, 1, 1, 0.5f),
        };
        hint.AddThemeFontSizeOverride("font_size", 11);
        wv.AddChild(hint);

        // ---------- end card ----------
        _endCard = MakeBottomPanel();
        _endCard.Visible = false;
        AddChild(_endCard);
        var ev = new VBoxContainer();
        _endCard.AddChild(ev);
        _endText = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _endText.AddThemeFontSizeOverride("font_size", 13);
        ev.AddChild(_endText);
        _endButton = new Button { Text = "Continue", CustomMinimumSize = new Vector2(0, 44) };
        _endButton.Pressed += () => Root.RestartMission();
        ev.AddChild(_endButton);

        // ---------- banner ----------
        _banner = new Label
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 0f,
            OffsetTop = 100,
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = "",
        };
        _banner.AddThemeFontSizeOverride("font_size", 30);
        AddChild(_banner);

        RebuildTurretButtons();
    }

    private PanelContainer MakeBottomPanel()
    {
        var p = new PanelContainer
        {
            AnchorLeft = 0f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = 8, OffsetRight = -8, OffsetTop = -230, OffsetBottom = -8,
        };
        return p;
    }

    private void RebuildTurretButtons()
    {
        foreach (Node c in _turretRow.GetChildren()) c.QueueFree();
        var w = Root.World;
        foreach (var id in w.Cfg.TurretOrder)
        {
            var def = w.Cfg.Turret(id);
            var btn = new Button
            {
                Text = $"{def.Name}\n${def.Cost}",
                CustomMinimumSize = new Vector2(104, 56),
            };
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.Pressed += () =>
            {
                if (_selectedSlot >= 0) Root.RequestBuild(_selectedSlot, id);
            };
            _turretRow.AddChild(btn);
        }
        var sell = new Button { Text = "Sell", CustomMinimumSize = new Vector2(64, 56) };
        sell.Pressed += () => { if (_selectedSlot >= 0) Root.RequestSell(_selectedSlot); };
        _turretRow.AddChild(sell);
    }

    // ---------- called by GameRoot ----------
    public void SelectSlot(int slot)
    {
        _selectedSlot = slot;
        GetRenderer().SelectedSlot = slot;
        var w = Root.World;
        if (slot >= 0 && slot < w.TurretView.Length)
        {
            var t = w.TurretView[slot];
            _slotLabel.Text = t.Built
                ? $"Slot {slot + 1}: {w.TurretDefs[t.DefIndex].Name}  (drag ship-side to rotate; Sell to refund)"
                : $"Slot {slot + 1}: empty";
        }
    }

    public void ShowReticlePrompt(string abilityName) => _reticlePrompt.Text = $"tap a target for {abilityName}";
    public void ClearReticlePrompt() => _reticlePrompt.Text = "";

    public void FlashBanner(SimEventKind kind)
    {
        _banner.Text = kind switch
        {
            SimEventKind.WaveCleared => "WAVE CLEARED",
            SimEventKind.MissionWon => "PLANET SECURED",
            SimEventKind.MissionLost => "PLANET LOST",
            _ => "",
        };
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
        string line2 = w.Phase == SimPhase.Wave
            ? $"enemies {w.EnemiesAlive}   ·   incoming {w.SpawnsRemaining}   ·   RD {Mathf.FloorToInt(w.ResearchDataEarned)}"
            : $"RD earned {Mathf.FloorToInt(w.ResearchDataEarned)}   ·   XP {Mathf.FloorToInt(w.XpEarned)}";
        _status.Text =
            $"◈ {Mathf.CeilToInt(w.PlanetIntegrity)}/{Mathf.CeilToInt(w.PlanetIntegrityMax)}     " +
            $"⬡ {w.Credits}     WAVE {Mathf.Min(w.WaveIndex + 1, w.WaveCount)}/{w.WaveCount}\n{line2}";

        bool build = w.Phase == SimPhase.Build;
        bool wave = w.Phase == SimPhase.Wave;
        bool ended = w.Phase is SimPhase.Won or SimPhase.Lost;

        _buildPanel.Visible = build;
        _wavePanel.Visible = wave;
        _endCard.Visible = ended;

        if (build)
        {
            _launch.Disabled = w.WaveIndex >= w.WaveCount;
            _launch.Text = w.WaveIndex >= w.WaveCount ? "— no more waves —" : $"▶  LAUNCH WAVE {w.WaveIndex + 1}";
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
                btn.Text = cd > 0f ? $"{def.Name}\n{Mathf.CeilToInt(cd)}s" : $"{def.Name}\nREADY";
            }
        }

        if (ended)
        {
            _endText.Text = BuildEndReport(w);
            _endButton.Text = w.Phase == SimPhase.Won ? "Play again" : "Retry";
        }
    }

    private static string BuildEndReport(SimWorld w)
    {
        var s = w.Stats;
        float tot = Mathf.Max(1f, s.TotalDamage);
        var sb = new StringBuilder();
        sb.AppendLine(w.Phase == SimPhase.Won
            ? $"Cleared all {w.WaveCount} waves."
            : $"Held {w.WavesCleared}/{w.WaveCount} waves.");
        sb.AppendLine($"Research Data earned: {Mathf.FloorToInt(w.ResearchDataEarned)}    XP: {Mathf.FloorToInt(w.XpEarned)}");
        sb.AppendLine($"Kills {s.EnemiesKilled}   ·   Leaked {s.EnemiesLeaked}");
        sb.AppendLine($"Damage — turrets {Pct(s.DamageByTurrets, tot)}  hero {Pct(s.DamageByHero, tot)}  abilities {Pct(s.DamageByAbilities, tot)}");
        return sb.ToString();
    }

    private static string Pct(float v, float tot) => $"{Mathf.RoundToInt(v / tot * 100f)}%";

    private void UpdateSpeedButtons(int active)
    {
        for (int i = 0; i < 4; i++) _speed[i].ButtonPressed = (i + 1) == active;
    }

    private SimRenderer GetRenderer()
    {
        foreach (Node c in Root.GetChildren())
            if (c is SimRenderer r) return r;
        return null!;
    }
}
