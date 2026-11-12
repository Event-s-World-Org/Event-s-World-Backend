using Microsoft.EntityFrameworkCore;

namespace EventsWorld.Modules.Events.Infrastructure.Persistence;

/// <summary>Data of the Events module, in the <c>events</c> schema (database.md §2).</summary>
internal sealed class EventsDbContext(DbContextOptions<EventsDbContext> options) : DbContext(options)
{
    internal const string Schema = "events";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventsDbContext).Assembly);
    }
}
