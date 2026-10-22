using LineSight.Simulator;

namespace LineSight.Tests.Simulator;

public class BackoffTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(4, 16)]
    [InlineData(5, 30)]     // 32 would exceed the 30s cap
    [InlineData(50, 30)]    // huge attempt numbers must not overflow
    public void Delay_DoublesEachAttempt_UpToTheCap(int attempt, double expectedSeconds)
    {
        var delay = Backoff.Delay(attempt, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }
}
