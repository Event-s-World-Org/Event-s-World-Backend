using Microsoft.EntityFrameworkCore;

namespace EventsWorld.Modules.Accounts.Infrastructure.Persistence;

/// <summary>Data of the Accounts module, in the <c>accounts</c> schema (database.md §2).</summary>
internal sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options) : DbContext(options)
{
    internal const string Schema = "accounts";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountsDbContext).Assembly);
    }
}
