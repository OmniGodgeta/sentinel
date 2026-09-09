using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Sentinel.Meta;

public sealed class ResearchNode
{
    public string Id { get; set; } = "";
    public string Branch { get; set; } = "";
    public int Tier { get; set; } = 1;
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public int RankCount { get; set; } = 1;
    public double CostBase { get; set; } = 50;       // Research Data cost of rank 1
    public string Currency { get; set; } = "rd";     // rd | alloy
    public double AlloyCost { get; set; } = 0;
    public int CommanderGate { get; set; } = 1;
    public bool IsCapstone { get; set; }
    public string CapstoneGroup { get; set; } = "";  // shared by the pair

    /// <summary>effect key -> value per rank.</summary>
    public Dictionary<string, float> Effects { get; set; } = new();

    [JsonIgnore] public IEnumerable<(string, float)> EffectsPairs
    {
        get { foreach (var kv in Effects) yield return (kv.Key, kv.Value); }
    }
}

public sealed class ResearchDb
{
    private readonly List<ResearchNode> _all = new();
    private readonly Dictionary<string, ResearchNode> _byId = new();

    public IReadOnlyList<ResearchNode> AllNodes => _all;
    public ResearchNode? Node(string id) => _byId.TryGetValue(id, out var n) ? n : null;

    public IEnumerable<ResearchNode> NodesIn(string branch, int tier)
    {
        foreach (var n in _all)
            if (n.Branch == branch && n.Tier == tier) yield return n;
    }

    public static readonly string[] Branches = { "armaments", "fortification", "fleet", "sentinel", "logistics" };
    public static string BranchName(string b) => b switch
    {
        "armaments" => "Armaments",
        "fortification" => "Fortification",
        "fleet" => "Fleet Command",
        "sentinel" => "Sentinel Protocols",
        "logistics" => "Logistics",
        _ => b,
    };

    public static ResearchDb Load()
    {
        var db = new ResearchDb();
        try
        {
            using var f = FileAccess.Open("res://data/research.json", FileAccess.ModeFlags.Read);
            var opts = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            var nodes = JsonSerializer.Deserialize<List<ResearchNode>>(f.GetAsText(), opts) ?? new();
            foreach (var n in nodes) { db._all.Add(n); db._byId[n.Id] = n; }
        }
        catch (System.Exception ex)
        {
            GD.PushError($"ResearchDb: failed to load research.json: {ex.Message}");
        }
        return db;
    }
}
