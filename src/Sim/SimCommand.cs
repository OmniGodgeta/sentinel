using Godot;

namespace Sentinel.Sim;

public enum CommandType
{
    None,
    SetHeroTarget,
    FireVolley,
    CastAbility,
    BuildTurret,
    RotateTurret,
    SellTurret,
    UpgradeTurret,
    ForkTurret,
    PickCard,
    StartWave,
}

/// <summary>
/// One queued player intent. Every input becomes one of these and is applied at
/// the top of a sim tick — nothing reads live input mid-tick, which is what makes
/// 4x tapping safe and the run reproducible.
/// </summary>
public struct SimCommand
{
    public CommandType Type;
    public Vector2 Pos;
    public int IntA;
    public int IntB;
    public float FloatA;
    public string? StrA;

    public static SimCommand HeroTarget(Vector2 p) => new() { Type = CommandType.SetHeroTarget, Pos = p };
    public static SimCommand Volley(Vector2 p) => new() { Type = CommandType.FireVolley, Pos = p };
    public static SimCommand Cast(int slot, Vector2 p) => new() { Type = CommandType.CastAbility, IntA = slot, Pos = p };
    public static SimCommand Build(int slot, string id) => new() { Type = CommandType.BuildTurret, IntA = slot, StrA = id };
    public static SimCommand Rotate(int slot, float ang) => new() { Type = CommandType.RotateTurret, IntA = slot, FloatA = ang };
    public static SimCommand Sell(int slot) => new() { Type = CommandType.SellTurret, IntA = slot };
    public static SimCommand Upgrade(int slot) => new() { Type = CommandType.UpgradeTurret, IntA = slot };
    public static SimCommand Fork(int slot, int fork) => new() { Type = CommandType.ForkTurret, IntA = slot, IntB = fork };
    public static SimCommand Card(int cardIndex) => new() { Type = CommandType.PickCard, IntA = cardIndex };
    public static SimCommand Wave() => new() { Type = CommandType.StartWave };
}
