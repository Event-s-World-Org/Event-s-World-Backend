using System.Net;
using EventsWorld.IntegrationTests.Infrastructure;

namespace EventsWorld.IntegrationTests;

public sealed class HealthCheckTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task GetHealth_ShouldBootHostWithAllModules_AndReturnOk()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
