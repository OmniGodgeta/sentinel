namespace Sentinel.Sim;

/// <summary>
/// Fixed-timestep driver. The sim always advances in whole ticks of
/// <see cref="TickDelta"/> seconds of *game* time. Rendering calls
/// <see cref="Advance"/> once per frame with the real frame delta; the speed
/// multiplier scales how much game-time that frame buys, so 4x runs 4x the sim
/// ticks per rendered frame — it is not an animation speed hack.
/// </summary>
public sealed class SimClock
{
    public const int TickRate = 60;
    public const float TickDelta = 1.0f / TickRate;

    /// <summary>Hard ceiling on ticks stepped in one frame, so a hitch or a
    /// breakpoint can't trigger a death-spiral. At 4x on a 30fps frame this is
    /// 4*(1/30)/(1/60) ≈ 8 ticks; 16 leaves headroom.</summary>
    public const int MaxTicksPerFrame = 16;

    public long Tick { get; private set; }
    public int Speed { get; private set; } = 1;

    private float _accumulator;

    public float GameTime => Tick * TickDelta;

    public void SetSpeed(int speed)
    {
        Speed = System.Math.Clamp(speed, 1, 4);
    }

    public void Reset()
    {
        Tick = 0;
        _accumulator = 0f;
    }

    /// <summary>
    /// Feeds real frame time in, invokes <paramref name="step"/> for each whole
    /// game-time tick that accrued. Returns the number of ticks stepped.
    /// </summary>
    public int Advance(float realDelta, System.Action step)
    {
        _accumulator += realDelta * Speed;
        int stepped = 0;
        while (_accumulator >= TickDelta && stepped < MaxTicksPerFrame)
        {
            step();
            Tick++;
            _accumulator -= TickDelta;
            stepped++;
        }
        // If we hit the ceiling, drop the backlog rather than carry it forward.
        if (stepped >= MaxTicksPerFrame && _accumulator > TickDelta)
            _accumulator = 0f;
        return stepped;
    }
}
