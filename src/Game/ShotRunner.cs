using Godot;
using Sentinel.Sim;

namespace Sentinel.Game;

/// <summary>
/// Dev-only: instances the real game, scripts a short demo, and writes a few
/// screenshots to user:// so the greybox can be eyeballed without a device.
///   xvfb-run -a godot scenes/Shots.tscn --rendering-driver opengl3
/// </summary>
public sealed partial class ShotRunner : Node2D
{
    private GameRoot _game = null!;
    private double _t;
    private int _shot;

    public override void _Ready()
    {
        _game = new GameRoot();
        AddChild(_game);
    }

    private bool _grabbed0, _grabbed1, _grabbed2, _grabbed3;
    private double _volleyT, _heroT;

    public override void _Process(double delta)
    {
        _t += delta;
        var w = _game.World;

        // keep buying turrets and abilities running whenever we can
        if (w.Phase == SimPhase.Build)
        {
            for (int s = 0; s < w.TurretView.Length; s++)
                if (!w.TurretView[s].Built)
                    w.Enqueue(SimCommand.Build(s, s % 3 == 0 ? "flak" : "autocannon"));

            if (!_grabbed0 && _t > 1.0) { Grab("build"); _grabbed0 = true; }
            if (_t > 1.4) { w.Enqueue(SimCommand.Wave()); _game.SetSpeed(4); }
        }
        else if (w.Phase == SimPhase.Wave)
        {
            _volleyT += delta; _heroT += delta;
            if (_volleyT > 0.3 && w.HeroView.Alive && w.HeroView.VolleyCooldownLeft <= 0f)
            { w.Enqueue(SimCommand.Volley(NearestThreatDir() * 300f)); _volleyT = 0; }

            var ab = w.AbilityView;
            for (int i = 0; i < ab.Length; i++)
                if (ab[i].DefIndex >= 0 && ab[i].CooldownLeft <= 0f)
                    w.Enqueue(SimCommand.Cast(i, NearestThreatDir() * 400f));

            if (_heroT > 0.5) { w.Enqueue(SimCommand.HeroTarget(NearestThreatDir() * 280f)); _heroT = 0; }

            if (!_grabbed1 && w.WaveIndex == 2) { Grab("wave3"); _grabbed1 = true; }
            if (!_grabbed2 && w.WaveIndex == 6) { Grab("wave7"); _grabbed2 = true; }
            if (!_grabbed3 && w.WaveIndex == 11) { Grab("wave12"); _grabbed3 = true; }
        }

        if (w.Phase is SimPhase.Won or SimPhase.Lost)
        {
            Grab("end");
            GetTree().Quit();
        }
        if (_t > 90) GetTree().Quit();
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
