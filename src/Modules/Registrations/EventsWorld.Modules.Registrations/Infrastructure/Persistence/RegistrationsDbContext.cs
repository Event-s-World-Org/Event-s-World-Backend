using Microsoft.EntityFrameworkCore;

namespace EventsWorld.Modules.Registrations.Infrastructure.Persistence;

/// <summary>Data of the Registrations module, in the <c>registrations</c> schema (database.md §2).</summary>
internal sealed class RegistrationsDbContext(DbContextOptions<RegistrationsDbContext> options) : DbContext(options)
{
    internal const string Schema = "registrations";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RegistrationsDbContext).Assembly);
    }
}
