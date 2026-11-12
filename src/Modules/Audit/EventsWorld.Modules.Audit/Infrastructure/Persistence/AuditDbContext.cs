using Microsoft.EntityFrameworkCore;

namespace EventsWorld.Modules.Audit.Infrastructure.Persistence;

/// <summary>Data of the Audit module, in the <c>audit</c> schema (database.md §2).</summary>
internal sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    internal const string Schema = "audit";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
    }
}
