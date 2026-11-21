using EventsWorld.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace EventsWorld.Modules.Notifications.Infrastructure.Persistence;

internal sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : DbContext(options)
{
    public const string Schema = "notifications";

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    public DbSet<ScheduledJobRun> ScheduledJobRuns => Set<ScheduledJobRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
