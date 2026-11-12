using Microsoft.EntityFrameworkCore;

namespace EventsWorld.Modules.Notifications.Infrastructure.Persistence;

/// <summary>Data of the Notifications module, in the <c>notifications</c> schema (database.md §2).</summary>
internal sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    internal const string Schema = "notifications";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
