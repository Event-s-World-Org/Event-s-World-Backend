using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EventsWorld.IntegrationTests;

public sealed class HealthCheckTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetHealth_ShouldBootHostWithAllModules_AndReturnOk()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
