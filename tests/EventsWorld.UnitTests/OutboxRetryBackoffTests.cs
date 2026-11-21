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
    public void ComputeBackoff_FollowsExponentialScheduleCappedAtOneHour(int attempt, int expectedSeconds)
    {
        var delay = OutboxWorker.ComputeBackoff(attempt);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public void DefaultMaxAttempts_CoversMoreThan24HoursOfRetries()
    {
        var options = new NotificationsOptions();
        var totalWindow = TimeSpan.Zero;

        for (var attempt = 0; attempt < options.MaxAttempts; attempt++)
        {
            totalWindow += OutboxWorker.ComputeBackoff(attempt);
        }

        Assert.True(totalWindow >= TimeSpan.FromHours(24));
    }
}
