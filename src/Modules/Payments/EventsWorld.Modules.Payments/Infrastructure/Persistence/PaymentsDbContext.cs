using Microsoft.EntityFrameworkCore;

namespace EventsWorld.Modules.Payments.Infrastructure.Persistence;

/// <summary>Data of the Payments module, in the <c>payments</c> schema (database.md §2).</summary>
internal sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    internal const string Schema = "payments";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);
    }
}
