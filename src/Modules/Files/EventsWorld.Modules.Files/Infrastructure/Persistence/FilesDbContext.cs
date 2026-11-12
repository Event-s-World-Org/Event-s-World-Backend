using Microsoft.EntityFrameworkCore;

namespace EventsWorld.Modules.Files.Infrastructure.Persistence;

/// <summary>Data of the Files module, in the <c>files</c> schema (database.md §2).</summary>
internal sealed class FilesDbContext(DbContextOptions<FilesDbContext> options) : DbContext(options)
{
    internal const string Schema = "files";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FilesDbContext).Assembly);
    }
}
