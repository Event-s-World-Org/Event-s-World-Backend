using Microsoft.EntityFrameworkCore;

namespace EventsWorld.Modules.Legal.Infrastructure.Persistence;

/// <summary>Data of the Legal module, in the <c>legal</c> schema (database.md §2).</summary>
internal sealed class LegalDbContext(DbContextOptions<LegalDbContext> options) : DbContext(options)
{
    internal const string Schema = "legal";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LegalDbContext).Assembly);
    }
}
