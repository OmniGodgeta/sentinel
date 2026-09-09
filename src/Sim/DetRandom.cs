namespace Sentinel.Sim;

/// <summary>
/// Deterministic PRNG (PCG-XSH-RR 64/32). The simulation must never touch
/// <see cref="System.Random"/> or wall-clock time — every random draw goes
/// through an instance of this, seeded from the mission seed. Same seed +
/// same build + same input stream => identical run (replays, repro bugs).
/// </summary>
public struct DetRandom
{
    private ulong _state;
    private readonly ulong _inc;

    public DetRandom(ulong seed, ulong sequence = 0xda3e39cb94b95bdbUL)
    {
        _state = 0UL;
        _inc = (sequence << 1) | 1UL;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    public uint NextUInt()
    {
        ulong old = _state;
        _state = old * 6364136223846793005UL + _inc;
        uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
    }

    /// <summary>Uniform in [0, maxExclusive).</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 1) return 0;
        // Lemire's debiased bounded method.
        uint bound = (uint)maxExclusive;
        uint threshold = (uint)(-bound) % bound;
        while (true)
        {
            uint r = NextUInt();
            if (r >= threshold) return (int)(r % bound);
        }
    }

    /// <summary>Uniform in [min, maxExclusive).</summary>
    public int NextInt(int min, int maxExclusive) => min + NextInt(maxExclusive - min);

    /// <summary>Uniform float in [0, 1).</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1.0f / 16777216.0f);

    /// <summary>Uniform float in [min, max).</summary>
    public float NextFloat(float min, float max) => min + NextFloat() * (max - min);

    /// <summary>Uniform angle in radians [0, 2π).</summary>
    public float NextAngle() => NextFloat() * 6.2831853071795862f;

    public bool Chance(float p) => NextFloat() < p;
}
