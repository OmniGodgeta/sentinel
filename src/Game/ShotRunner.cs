using Godot;
using Sentinel.Sim;

namespace Sentinel.Game;

/// <summary>
/// Dev-only: instances the real game, scripts a short demo, and writes a few
/// screenshots to user:// so the greybox can be eyeballed without a device.
///   godot scenes/Shots.tscn
/// Currently pointed at reproducing the level-up weapon-card draft.
/// </summary>
public sealed partial class ShotRunner : Node2D
{
    private GameRoot _game = null!;
    private double _t;
    private int _shot;

    [Export] public string Mission = "res://data/missions/endless.json";

    public override void _Ready()
    {
        var win = GetWindow();
        win.Mode = Window.ModeEnum.Windowed;
        win.Size = new Vector2I(560, 1000);
        _game = new GameRoot { MissionPath = Mission };
        AddChild(_game);

        GD.Print("=== card textures ===");
        foreach (var d in _game.World.Cfg.HeroWeapons)
        {
            var path = $"res://assets/game/cards/{d.Id}.jpg";
            try
            {
                var tex = GD.Load<Texture2D>(path);
                GD.Print($"  {d.Id}: {(tex == null ? "NULL" : tex.GetType().Name + " " + tex.GetSize())}");
            }
            catch (System.Exception ex) { GD.Print($"  {d.Id}: THREW {ex.GetType().Name}: {ex.Message}"); }
        }
        GD.Print($"HeroWeapons.Count = {_game.World.Cfg.HeroWeapons.Count}");
    }

    private bool _launched, _grabWave, _grabDraft, _picked, _xpDone;

    public override void _Process(double delta)
    {
        _t += delta;
        var w = _game.World;

        if (w.Phase == SimPhase.Build)
        {
            for (int s = 0; s < w.TurretView.Length; s++)
                if (!w.TurretView[s].Built)
                    w.Enqueue(SimCommand.Build(s, s % 3 == 0 ? "flak" : "autocannon"));
            if (!_launched && _t > 1.0) { w.Enqueue(SimCommand.Wave()); _launched = true; }
            return;
        }

        if (w.Phase == SimPhase.Wave)
        {
            // fly the ship at the nearest threat so the new hull shows in the shot
            if (((int)(_t * 4)) % 2 == 0) w.Enqueue(SimCommand.HeroMove(NearestThreatDir()));

            if (!_grabWave && _t > 4.0)
            {
                Grab("wave_ship");
                _grabWave = true;
            }

            // one modest XP injection to force ~4 level-ups, then let it play
            if (!_xpDone && _t > 4.5) { w.GainRunXp(700f); _xpDone = true; }

            if (w.HasPendingDraft && !_grabDraft && _t > 5.0)
            {
                GD.Print($"=== DRAFT: pending, options = [{string.Join(",", w.DraftOptionIndices)}]  runLevel={w.RunLevel}");
                foreach (int idx in w.DraftOptionIndices)
                    GD.Print($"   option {idx} -> {(idx >= 0 && idx < w.Cfg.HeroWeapons.Count ? w.Cfg.HeroWeapons[idx].Id : "OUT OF RANGE")}");
                CallDeferred(nameof(GrabDraft));
                _grabDraft = true;
            }
        }

        if (_t > 30) { GD.Print("=== timeout, quitting"); GetTree().Quit(); }
    }

    private void GrabDraft()
    {
        GetTree().CreateTimer(0.6).Timeout += () =>
        {
            Grab("draft");
            var w = _game.World;
            // drain every pending draft, preferring orbital cards so they show on the field
            int guard = 0;
            while (w.HasPendingDraft && w.DraftOptionIndices.Count > 0 && guard++ < 20)
            {
                int pick = w.DraftOptionIndices[0];
                foreach (int o in w.DraftOptionIndices) if (w.IsOrbitalCard(o)) { pick = o; break; }
                _game.RequestPickCard(pick);
            }
            _game.SetSpeed(2);
            GetTree().CreateTimer(10.0).Timeout += () => { Grab("orbital_field"); GetTree().Quit(); };
        };
    }

    private Vector2 NearestThreatDir()
    {
        var w = _game.World;
        var en = w.EnemyView;
        Vector2 best = Vector2.Up; float bestD = float.MaxValue;
        for (int i = 0; i < en.Length; i++)
        {
            if (!en[i].Alive) continue;
            float d = en[i].Pos.LengthSquared();
            if (d < bestD) { bestD = d; best = en[i].Pos.Normalized(); }
        }
        return best;
    }

    private void Grab(string label)
    {
        var img = GetViewport().GetTexture().GetImage();
        string path = $"user://shot_{_shot++}_{label}.png";
        img.SavePng(path);
        GD.Print($"wrote {ProjectSettings.GlobalizePath(path)}");
    }
}
