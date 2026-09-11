using Godot;

namespace Sentinel.Sim;

// Plain data. No Godot Nodes per entity — the renderer draws these with a
// MultiMesh pass later. Pools are fixed arrays with an alive flag + free list,
// so handles (index+generation) stay valid across a whole wave.

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
    public int DefIndex;
    public Vector2 Pos;
    public Vector2 Vel;
    public float Hp;
    public float MaxHp;
    public float Shield;
    public float MaxShield;
    public float ShieldRegenTimer;   // counts up; regen once past def delay
    public float Armor;
    public float Radius;
    public float ContactDamage;
    public int Bounty;
    public float DistToCenter;
    public float BaseSpeed;

    // status
    public float SlowFactor;         // 1 = normal, <1 = slowed (this tick, cleared+reapplied each tick)
    public float PullX, PullY;       // accumulated pull impulse this tick
    public float StunLeft;           // >0 = held in place, cannot move/attack (orbital lightning / shock orb)
    public float BurnLeft;           // >0 = taking BurnDps damage-over-time (orbital laser / radiation)
    public float BurnDps;

    // behaviour timers
    public float AttackTimer;        // bombard ranged / boss
    public float SpawnTimer;         // carrier
    public float BlinkTimer;         // phase runner
    public float MechanicTimer;      // boss phase
    public bool MechanicActive;      // boss invulnerable-shell etc.
    public float MechanicPhase;      // 0..1 within the mechanic window
    public int LeechSlot;            // -1 none, else the turret slot this leech is riding
    public bool Standoff;            // bombard has stopped to shell

    public readonly bool IsShielded => Shield > 0.01f;
}

public struct Projectile
{
    public bool Alive;
    public byte Kind;           // 0 = turret projectile, 1 = hero missile, 2 = enemy shell
    public Vector2 Pos;
    public Vector2 Vel;
    public float Damage;
    public float SplashRadius;
    public float ArmorPen;
    public float ShieldMult;
    public int PierceLeft;
    public float Life;
    public float Age;           // seconds since launch — drives the guidance ramp and the render trail
    public EnemyHandle Target;
    public byte SourceTurret;   // 255 = hero, 254 = enemy
    public float Slow;          // graviton projector: applies slow on hit (0 = none)
    // guided-missile flight model (0 = legacy constant-speed dumb projectile)
    public float SpeedMax;      // cruise speed the motor accelerates toward
    public float Accel;         // units/s^2 while below SpeedMax
    public float Agility;       // turn rate, rad/s (0 = use the default)
}

public struct Turret
{
    public bool Built;
    public int DefIndex;
    public int Slot;
    public int Level;           // 1..3
    public int Fork;            // -1 = not chosen, else index into def.Forks
    public float Angle;
    public Vector2 Pos;
    public float CooldownLeft;
    public EnemyHandle Target;
    public float RampStacks;    // laser lattice
    public float RampGraceLeft; // seconds ramp persists after losing contact
    public float DisabledLeft;  // leech / EMP
    public float DamageDealt;   // attribution
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
    public float OverdriveLeft;
    public float OverdriveVolleyMult;
    public float ShieldPool;        // from Aegis "overflow to hero shield" branch etc. (reserved)
}

public struct AbilitySlot
{
    public int DefIndex;       // -1 if empty
    public float EffMult;      // effect multiplier from level + Potency picks (default 1)
    public float CdMult;       // cooldown multiplier from Tempo picks (default 1)
    public float CooldownLeft;
    public float ActiveLeft;
    public float P0, P1, P2, P3;
    public float TickAccum;
    public Vector2 Anchor;     // reticle position for zone abilities
    public EnemyHandle Tether; // gravity snare focus, etc.
}
