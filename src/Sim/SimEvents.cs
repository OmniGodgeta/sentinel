using System.Collections.Generic;
using Godot;

namespace Sentinel.Sim;

public enum SimEventKind : byte
{
    EnemyHit,
    EnemyKilled,
    VolleyLaunched,
    MissileImpact,
    TurretFired,
    AbilityCast,
    BarrageTick,
    NovaPulse,       // reserved
    PlanetHit,
    HeroHit,
    HeroDown,
    WaveCleared,
    MissionWon,
    MissionLost,
}

public struct SimEvent
{
    public SimEventKind Kind;
    public Vector2 Pos;
    public float A;     // magnitude / radius depending on kind
    public int I;       // slot / index depending on kind
}

/// <summary>
/// The sim writes presentation-only events here (sparks, booms, haptics cues).
/// The renderer drains it once per frame *after* stepping. Never read back into
/// the sim — it must not affect determinism.
/// </summary>
public sealed class SimEventBuffer
{
    private readonly List<SimEvent> _events = new(256);
    public IReadOnlyList<SimEvent> Events => _events;

    public void Push(SimEventKind kind, Vector2 pos, float a = 0f, int i = 0)
        => _events.Add(new SimEvent { Kind = kind, Pos = pos, A = a, I = i });

    public void Clear() => _events.Clear();
}
