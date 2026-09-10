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

    private static readonly Dictionary<string, Texture2D> _hull = new();
    /// <summary>Hero hull cosmetic (Shop / Fleet Requisition). "standard" = the default ship.</summary>
    public static Texture2D Hull(string id)
    {
        if (string.IsNullOrEmpty(id) || id == "standard") return Ship;
        if (_hull.TryGetValue(id, out var t)) return t;
        string p = $"res://assets/game/hulls/{id}.png";
        t = ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : Ship;
        _hull[id] = t;
        return t;
    }

    /// <summary>Ordnance-palette tint (Shop / Ordnance Palettes) for the hero volley + missile FX.</summary>
    public static (Color muzzle, Color trail, Color bloom) Ordnance(string id) => id switch
    {
        "ion" => (new Color(0.6f, 0.85f, 1f), new Color(0.5f, 0.8f, 1f, 0.9f), new Color(0.45f, 0.75f, 1f)),
        "plasma" => (new Color(1f, 0.7f, 1f), new Color(1f, 0.55f, 0.95f, 0.9f), new Color(1f, 0.5f, 0.9f)),
        "void" => (new Color(0.75f, 0.5f, 1f), new Color(0.6f, 0.35f, 1f, 0.9f), new Color(0.7f, 0.45f, 1f)),
        "mono" => (new Color(1f, 1f, 1f), new Color(1f, 1f, 1f, 0.85f), new Color(0.95f, 0.97f, 1f)),
        _ => (new Color(1f, 0.8f, 0.5f), new Color(1f, 0.6f, 0.3f, 0.9f), new Color(1f, 0.6f, 0.25f)),  // ember
    };
    public static Texture2D Missile => Tex("res://assets/game/missile.png");
    public static Texture2D Bullet => Tex("res://assets/game/bullet.png");
    public static Texture2D Turret => Tex("res://assets/game/turret.png");

    // turret art — a shared tintable base platform + a per-type gun head (Kenney TD, CC0)
    public static Texture2D TurretBase => Tex("res://assets/game/turrets/base.png");
    public static Texture2D TurretMuzzle => Tex("res://assets/game/turrets/muzzle.png");
    private static readonly Dictionary<string, Texture2D> _gun = new();
    public static Texture2D TurretGun(string id)
    {
        if (_gun.TryGetValue(id, out var t)) return t;
        string p = $"res://assets/game/turrets/{id}.png";
        t = ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : Tex("res://assets/game/turrets/autocannon.png");
        _gun[id] = t;
        return t;
    }
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
