using System.Collections.Generic;
using Godot;

namespace Sentinel.Render;

/// <summary>Loads the game's textures once. Kenney CC0 sprites + NASA Blue Marble.</summary>
public static class Art
{
    private static readonly Dictionary<string, Texture2D> _cache = new();

    public static Texture2D Tex(string path)
    {
        if (_cache.TryGetValue(path, out var t)) return t;
        t = GD.Load<Texture2D>(path);
        _cache[path] = t;
        return t;
    }

    public static Texture2D Ship => Tex("res://assets/game/ship.png");
    public static Texture2D Missile => Tex("res://assets/game/missile.png");
    public static Texture2D Bullet => Tex("res://assets/game/bullet.png");
    public static Texture2D Turret => Tex("res://assets/game/turret.png");
    public static Texture2D Smoke => Tex("res://assets/game/fx/smoke.png");
    public static Texture2D Spark => Tex("res://assets/game/fx/spark.png");
    public static Texture2D Flare => Tex("res://assets/game/fx/flare.png");
    public static Texture2D Explosion => Tex("res://assets/game/fx/explosion.png");

    private static readonly Dictionary<string, Texture2D> _enemy = new();
    public static Texture2D Enemy(string id)
    {
        if (_enemy.TryGetValue(id, out var t)) return t;
        string p = $"res://assets/game/enemies/{id}.png";
        t = ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : Tex("res://assets/game/enemies/skiff.png");
        _enemy[id] = t;
        return t;
    }

    public static ShaderMaterial EarthMaterial()
    {
        var m = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/game/earth.gdshader") };
        m.SetShaderParameter("day_tex", Tex("res://assets/game/earth_day.png"));
        m.SetShaderParameter("cloud_tex", Tex("res://assets/game/earth_clouds.png"));
        m.SetShaderParameter("night_tex", Tex("res://assets/game/earth_night.png"));
        return m;
    }
}
