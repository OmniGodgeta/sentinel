using System.Collections.Generic;
using Godot;

namespace Sentinel.Sim;

public enum SimEventKind : byte
{
    EnemyHit,
    EnemyKilled,
    VolleyLaunched,
    MissileImpact,
    TurretFired,      // Pos = turret, PosB = target, I = slot
    BeamTick,         // Pos = turret, PosB = target
    ChainArc,         // Pos = from, PosB = to
    AbilityCast,      // Pos = reticle, I = slot (-3 = card pick, -1/-2 = enemy fx)
    BarrageTick,
    NovaPulse,
    PlanetHit,
    HeroHit,
    HeroDown,
    EnemySpawned,
    WaveCleared,
    MissionWon,
    MissionLost,
}

public struct SimEvent
{
    public SimEventKind Kind;
    public Vector2 Pos;
    public Vector2 PosB;
    public float A;     // magnitude / radius
    public int I;        // slot / index
}

/// <summary>
/// The sim writes presentation-only events here (sparks, booms, haptics cues).
/// The renderer drains it once per frame *after* stepping. Never read back into
/// the sim — it must not affect determinism.
/// </summary>
public sealed class SimEventBuffer
{
    private readonly List<SimEvent> _events = new(512);
    public IReadOnlyList<SimEvent> Events => _events;

    public void Push(SimEventKind kind, Vector2 pos, float a = 0f, int i = 0)
        => _events.Add(new SimEvent { Kind = kind, Pos = pos, A = a, I = i });

    public void PushLine(SimEventKind kind, Vector2 from, Vector2 to, float a = 0f, int i = 0)
        => _events.Add(new SimEvent { Kind = kind, Pos = from, PosB = to, A = a, I = i });

    public void Clear() => _events.Clear();
}
