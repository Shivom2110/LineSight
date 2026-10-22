namespace LineSight.Simulator;

/// <summary>
/// Exponential backoff: initial, 2x, 4x, ... capped at max. Spreads out retries so a restarting
/// API isn't hammered. (A production version would add random jitter so many PLC gateways
/// don't retry in lock-step.)
/// </summary>
public static class Backoff
{
    public static TimeSpan Delay(int attempt, TimeSpan initial, TimeSpan max)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attempt);

        // Done in double so large attempt numbers can't overflow before the cap is applied.
        var ticks = initial.Ticks * Math.Pow(2, attempt);
        return ticks >= max.Ticks ? max : TimeSpan.FromTicks((long)ticks);
    }
}
