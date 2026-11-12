using System.Net;
using EventsWorld.IntegrationTests.Infrastructure;

namespace EventsWorld.IntegrationTests.Database;

/// <summary>TS-004: <c>/health/ready</c> returns 200 when PostgreSQL is reachable.</summary>
[Collection(DatabaseTestGroup.Name)]
public sealed class HealthReadinessTests(PostgresFixture database)
{
    [Fact]
    public async Task Ready_WithReachableDatabase_ShouldReturn200()
    {
        using var client = database.Api.CreateClient();
        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
