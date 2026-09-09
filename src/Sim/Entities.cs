using Godot;

namespace Sentinel.Sim;

// Plain data. No Godot Nodes per entity — the renderer draws these with
// MultiMesh. Pools are fixed-size arrays with an alive flag + free list, so
// handles (index+generation) stay valid across a whole wave.

public enum TargetMode { First, Closest, Strongest }

public struct EnemyHandle
{
    public int Index;
    public uint Gen;
    public static readonly EnemyHandle None = new() { Index = -1, Gen = 0 };
    public readonly bool IsNone => Index < 0;
}

public struct Enemy
{
    public bool Alive;
    public uint Gen;
    public int DefIndex;        // into SimWorld.EnemyDefs
    public Vector2 Pos;
    public Vector2 Vel;
    public float Hp;
    public float MaxHp;
    public float Armor;
    public float Radius;
    public float ContactDamage;
    public int Bounty;
    public float DistToCenter;  // cached each tick, used for "first" targeting
}

public struct Projectile
{
    public bool Alive;
    public byte Kind;           // 0 = turret shot, 1 = hero missile
    public Vector2 Pos;
    public Vector2 Vel;
    public float Damage;
    public float SplashRadius;
    public float ArmorPen;      // unused Phase 1, reserved
    public float Life;          // seconds remaining before self-expire
    public EnemyHandle Target;  // homing missiles only; None => straight shot
    public byte SourceTurret;   // for damage attribution (index, 255 = hero)
}

public struct Turret
{
    public bool Built;
    public int DefIndex;        // into SimWorld.TurretDefs
    public int Slot;            // 0..TurretSlots-1
    public float Angle;         // facing, radians (player can rotate in build phase)
    public Vector2 Pos;         // on the turret ring
    public float CooldownLeft;  // seconds until next shot
    public EnemyHandle Target;
    // attribution
    public float DamageDealt;
}

public struct HeroState
{
    public Vector2 Pos;
    public float OrbitRadius;
    public float Hull;
    public float MaxHull;
    public float VolleyCooldownLeft;
    public bool Alive;
    public float RespawnLeft;
    // Overdrive buff
    public float OverdriveLeft;
    public float OverdriveVolleyMult;   // e.g. 0.4 => cooldown *0.4 while active
}

public struct AbilitySlot
{
    public int DefIndex;        // into SimWorld.AbilityDefs, -1 if empty
    public float CooldownLeft;
    // active-effect bookkeeping (Phase 1: barrage arc + barrier)
    public float ActiveLeft;
    public float P0;            // param cache (e.g. arc center)
    public float P1;            // (e.g. arc half-width)
    public float P2;            // (e.g. absorb pool remaining)
    public float TickAccum;     // for periodic effects like barrage ticks
}
