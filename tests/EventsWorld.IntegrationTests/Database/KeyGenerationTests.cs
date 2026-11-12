using EventsWorld.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventsWorld.IntegrationTests.Database;

/// <summary>
/// Primary keys are time-ordered UUID v7 (database.md §1). Domain code creates them with
/// <c>Guid.CreateVersion7()</c>; when EF Core generates a Guid key, it must be v7 as well.
/// </summary>
public sealed class KeyGenerationTests
{
    [Fact]
    public void GuidKeysGeneratedByEfCore_AreUuidVersion7()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddModuleDbContext<KeyProbeDbContext>("key_probe");

        using var provider = services.BuildServiceProvider();
        using var context = provider.GetRequiredService<KeyProbeDbContext>();

        var first = new KeyProbe();
        var second = new KeyProbe();
        context.AddRange(first, second);

        Assert.Equal(7, first.Id.Version);
        Assert.Equal(7, second.Id.Version);
        Assert.True(first.Id.CompareTo(second.Id) < 0, "UUID v7 keys must be time-ordered.");
    }
}

internal sealed class KeyProbe
{
    public Guid Id { get; set; }
}

internal sealed class KeyProbeDbContext(DbContextOptions<KeyProbeDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<KeyProbe>();
}
