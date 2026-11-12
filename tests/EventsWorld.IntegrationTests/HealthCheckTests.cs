using System.Net;
using EventsWorld.IntegrationTests.Infrastructure;

namespace EventsWorld.IntegrationTests;

public sealed class HealthCheckTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Live_ShouldBootHostWithAllModules_AndReturnOk()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_WithoutDatabase_ShouldReturn503()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
