using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace Sentinel.Config;

/// <summary>
/// Loads and holds every data file. One instance, built at boot. Systems read
/// defs by id; nothing here changes at runtime.
/// </summary>
public sealed class ConfigDb
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public BalanceDef Balance { get; private set; } = new();
    public HeroDef Hero { get; private set; } = new();
    public ArcDef Arc { get; private set; } = new();

    private readonly Dictionary<string, TurretDef> _turrets = new();
    private readonly Dictionary<string, EnemyDef> _enemies = new();
    private readonly Dictionary<string, AbilityDef> _abilities = new();
    private readonly Dictionary<string, CodexEntry> _codex = new();
    private readonly List<string> _turretOrder = new();
    private readonly List<string> _abilityOrder = new();

    public IReadOnlyList<string> TurretOrder => _turretOrder;
    public IReadOnlyList<string> AbilityOrder => _abilityOrder;
    public IReadOnlyDictionary<string, AbilityDef> Abilities => _abilities;

    public TurretDef Turret(string id) => _turrets[id];
    public EnemyDef Enemy(string id) => _enemies[id];
    public AbilityDef Ability(string id) => _abilities[id];
    public bool HasEnemy(string id) => _enemies.ContainsKey(id);
    public bool HasAbility(string id) => _abilities.ContainsKey(id);
    public CodexEntry? Codex(string id) => _codex.TryGetValue(id, out var e) ? e : null;

    public static ConfigDb Load()
    {
        var db = new ConfigDb();
        db.Balance = ReadOne<BalanceDef>("res://data/balance.json") ?? new BalanceDef();
        db.Hero = ReadOne<HeroDef>("res://data/hero.json") ?? new HeroDef();
        db.Arc = ReadOne<ArcDef>("res://data/arc_01.json") ?? new ArcDef();

        foreach (var t in ReadList<TurretDef>("res://data/turrets.json"))
        {
            db._turrets[t.Id] = t;
            db._turretOrder.Add(t.Id);
        }
        foreach (var e in ReadList<EnemyDef>("res://data/enemies.json"))
            db._enemies[e.Id] = e;
        foreach (var a in ReadList<AbilityDef>("res://data/abilities.json"))
        {
            db._abilities[a.Id] = a;
            db._abilityOrder.Add(a.Id);
        }
        foreach (var c in ReadList<CodexEntry>("res://data/codex.json"))
            db._codex[c.Id] = c;
        return db;
    }

    public MissionDef LoadMission(string path)
    {
        var m = ReadOne<MissionDef>(path);
        if (m == null)
            throw new System.IO.FileNotFoundException($"Mission not found or invalid: {path}");
        return m;
    }

    private static string ReadText(string resPath)
    {
        using var f = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
        if (f == null)
            throw new System.IO.FileNotFoundException($"Cannot open {resPath}: {FileAccess.GetOpenError()}");
        return f.GetAsText();
    }

    private static T? ReadOne<T>(string resPath)
    {
        try { return JsonSerializer.Deserialize<T>(ReadText(resPath), JsonOpts); }
        catch (System.Exception ex)
        {
            GD.PushError($"ConfigDb: failed to read {resPath}: {ex.Message}");
            return default;
        }
    }

    private static List<T> ReadList<T>(string resPath)
    {
        try { return JsonSerializer.Deserialize<List<T>>(ReadText(resPath), JsonOpts) ?? new(); }
        catch (System.Exception ex)
        {
            GD.PushError($"ConfigDb: failed to read {resPath}: {ex.Message}");
            return new();
        }
    }
}
