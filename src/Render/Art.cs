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
    /// <summary>Hex-grid shield surface texture (extracted from Planet Defense TD's own
    /// force-shield material) — tiled across the Planet Shield dome.</summary>
    public static Texture2D ShieldHex => Tex("res://assets/game/shield/hex_pattern.png");

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

    // ---- real Planet Defense: Space TD art (assets/game/pdtd/, see CREDITS.txt) ----

    private static readonly Dictionary<string, Texture2D> _sentinel = new();
    /// <summary>The sentinel platform's own model art, keyed by the weapon's
    /// <c>Kind</c> (data/orbital_weapons.json). Null when a kind has no PDTD
    /// counterpart — callers fall back to drawing the platform procedurally.</summary>
    public static Texture2D? SentinelArt(string kind)
    {
        if (_sentinel.TryGetValue(kind, out var t)) return t;
        string p = $"res://assets/game/pdtd/icons/{kind}.png";
        t = ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : null!;
        _sentinel[kind] = t;
        return t;
    }

    private static readonly Dictionary<string, Texture2D> _vfx = new();
    /// <summary>A PDTD VFX texture by short name (beam01, aqua_bullet, energyball, ...).</summary>
    public static Texture2D? Vfx(string name)
    {
        if (_vfx.TryGetValue(name, out var t)) return t;
        string p = $"res://assets/game/pdtd/vfx/{name}.png";
        t = ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : null!;
        _vfx[name] = t;
        return t;
    }

    private static readonly Dictionary<string, Texture2D> _pdtd = new();
    /// <summary>Any PDTD UI sprite by "folder/name", e.g. "loot/silver_key",
    /// "rarity/epic", "cardui/card_front_normal", "techpoint/waterdrop".
    /// Null when that sprite hasn't been extracted (callers fall back to drawn art).</summary>
    public static Texture2D? Pdtd(string path)
    {
        if (_pdtd.TryGetValue(path, out var t)) return t;
        string p = $"res://assets/game/pdtd/{path}.png";
        t = ResourceLoader.Exists(p) ? GD.Load<Texture2D>(p) : null!;
        _pdtd[path] = t;
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
