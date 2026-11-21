using EventsWorld.Shared.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public void NotificationsDbContext_HasNoPendingModelChanges()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(config);
        EventsWorld.Modules.Notifications.NotificationsModule.AddNotificationsModule(services, config);

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventsWorld.Modules.Notifications.Infrastructure.Persistence.NotificationsDbContext>();

        Assert.False(Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.HasPendingModelChanges(dbContext.Database));
    }
}
