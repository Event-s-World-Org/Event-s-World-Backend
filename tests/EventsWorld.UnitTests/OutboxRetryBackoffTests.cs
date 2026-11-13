using EventsWorld.Modules.Notifications.Infrastructure;

namespace EventsWorld.UnitTests;

public sealed class OutboxRetryBackoffTests
{
    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 30)]
    [InlineData(2, 120)]
    [InlineData(3, 600)]
    [InlineData(4, 1800)]
    [InlineData(5, 3600)]
    [InlineData(10, 3600)]
    public void ComputeBackoff_ScalesExponentiallyAndCapsAtOneHour(int attempt, double expectedSeconds)
    {
        var backoff = OutboxWorker.ComputeBackoff(attempt);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), backoff);
    }
}
