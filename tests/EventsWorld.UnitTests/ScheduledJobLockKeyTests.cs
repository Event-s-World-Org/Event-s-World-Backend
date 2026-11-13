using EventsWorld.Shared.Infrastructure.Jobs;

namespace EventsWorld.UnitTests;

public sealed class ScheduledJobLockKeyTests
{
    [Fact]
    public void ComputeLockId_ReturnsDeterministicValueForSameJobName()
    {
        var id1 = ScheduledJob.ComputeLockId("NotificationsOutbox");
        var id2 = ScheduledJob.ComputeLockId("NotificationsOutbox");

        Assert.Equal(id1, id2);
        Assert.NotEqual(0L, id1);
    }

    [Fact]
    public void ComputeLockId_ReturnsDifferentValuesForDifferentJobNames()
    {
        var id1 = ScheduledJob.ComputeLockId("NotificationsOutbox");
        var id2 = ScheduledJob.ComputeLockId("CloseRegistrationsJob");
        var id3 = ScheduledJob.ComputeLockId("FileRetentionJob");

        Assert.NotEqual(id1, id2);
        Assert.NotEqual(id2, id3);
        Assert.NotEqual(id1, id3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ComputeLockId_ThrowsArgumentException_WhenNameIsBlank(string invalidName)
    {
        Assert.Throws<ArgumentException>(() => ScheduledJob.ComputeLockId(invalidName));
    }
}
