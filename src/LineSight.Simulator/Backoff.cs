namespace LineSight.Simulator;

/// <summary>Exponential backoff: initial, 2x, 4x, ... capped at max.</summary>
public static class Backoff
{
    public static TimeSpan Delay(int attempt, TimeSpan initial, TimeSpan max) =>
        throw new NotImplementedException();
}
