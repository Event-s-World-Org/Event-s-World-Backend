using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventsWorld.Api.Middleware;
using EventsWorld.IntegrationTests.Infrastructure;

namespace EventsWorld.IntegrationTests;

/// <summary>Acceptance criteria of TS-004 and the shared request pipeline (backend.md §3–5).</summary>
public sealed class ApiCrossCuttingConcernsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task UnhandledException_Returns500ProblemDetails_WithoutStackTrace_AndWithCorrelationId()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri(TestEndpoints.Crash, UriKind.Relative));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var (json, problem) = await ReadProblemAsync(response);

        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.Equal("internal.server_error", problem.GetProperty("code").GetString());
        Assert.DoesNotContain(TestEndpoints.CrashMessage, json, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", json, StringComparison.Ordinal);
        AssertCorrelationId(response, problem);
    }

    [Fact]
    public async Task ValidationError_Returns400_WithTheFailingFields()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            new Uri(TestEndpoints.Echo, UriKind.Relative),
            new EchoRequest(Name: "", Email: "not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var (_, problem) = await ReadProblemAsync(response);

        Assert.Equal("validation.failed", problem.GetProperty("code").GetString());
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("Email", out _));
        AssertCorrelationId(response, problem);
    }

    [Fact]
    public async Task MalformedJson_Returns400ProblemDetails_NotAServerError()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(new Uri(TestEndpoints.Echo, UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var (_, problem) = await ReadProblemAsync(response);
        Assert.Equal("request.invalid", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UnknownRoute_Returns404ProblemDetails()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/api/v1/public/does-not-exist", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var (_, problem) = await ReadProblemAsync(response);
        Assert.Equal("request.not_found", problem.GetProperty("code").GetString());
        AssertCorrelationId(response, problem);
    }

    [Fact]
    public async Task TestEndpoints_AreNotPartOfTheApi()
    {
        // The real host (without ApiFactory's test services) has no ping, echo or crash endpoints.
        using var host = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSolutionContentRoot());
        using var client = host.CreateClient();
        using var response = await client.GetAsync(new Uri(TestEndpoints.Crash, UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("4f1c2a9e-1b2c-4d5e-8f90-123456789abc", true)]
    [InlineData("abc 123\nfake log line", false)]
    [InlineData("a-very-long-value-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    public async Task CorrelationId_IsKeptOnlyWhenSafe(string received, bool kept)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TestEndpoints.Ping, UriKind.Relative));
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, received);

        using var response = await client.SendAsync(request);

        var returned = Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.Equal(kept, returned == received);
        Assert.Matches("^[A-Za-z0-9-]{1,64}$", returned);
    }

    [Fact]
    public async Task OpenApiAndScalar_AreAvailableInDevelopment()
    {
        using var client = factory.CreateClient();

        using var openApi = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
        Assert.Contains("\"openapi\"", await openApi.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var scalar = await client.GetAsync(new Uri("/scalar/v1", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
    }

    [Fact]
    public async Task SecurityHeaders_ArePresent()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri(TestEndpoints.Ping, UriKind.Relative));

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
    }

    [Fact]
    public async Task Cors_AllowsTheFrontend_ExposesItsHeaders_AndNoCredentials()
    {
        using var client = factory.CreateClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, new Uri(TestEndpoints.Echo, UriKind.Relative));
        preflight.Headers.Add("Origin", "http://localhost:4200");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization,idempotency-key,if-match");

        using var preflightResponse = await client.SendAsync(preflight);

        Assert.Equal("http://localhost:4200", Assert.Single(preflightResponse.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(preflightResponse.Headers.Contains("Access-Control-Allow-Credentials"));

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TestEndpoints.Ping, UriKind.Relative));
        request.Headers.Add("Origin", "http://localhost:4200");
        using var response = await client.SendAsync(request);

        var exposed = string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers"));
        Assert.Contains("ETag", exposed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Retry-After", exposed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(CorrelationIdMiddleware.HeaderName, exposed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cors_RejectsOtherOrigins()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TestEndpoints.Ping, UriKind.Relative));
        request.Headers.Add("Origin", "https://evil.example");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task RateLimit_Returns429_WithRetryAfter_AndIgnoresForwardedForFromUntrustedClients()
    {
        await using var limited = new ApiFactory()
            .WithSetting("RateLimiting:AnonymousPermitLimit", "2")
            .WithSetting("RateLimiting:AnonymousWindowSeconds", "30");
        using var client = limited.CreateClient();

        // A different X-Forwarded-For on each request must not give a fresh limit.
        for (var i = 1; i <= 2; i++)
        {
            using var allowed = await SendWithForwardedFor(client, $"203.0.113.{i}");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var rejected = await SendWithForwardedFor(client, "203.0.113.3");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        var (_, problem) = await ReadProblemAsync(rejected);
        Assert.Equal("rate_limit.exceeded", problem.GetProperty("code").GetString());
        AssertCorrelationId(rejected, problem);
    }

    [Fact]
    public async Task RateLimit_UsesTheClientIp_FromATrustedProxy()
    {
        // Behind the hosting proxy (listed in ForwardedHeaders:KnownProxies) each client gets its own limit.
        await using var behindProxy = new ApiFactory()
            .WithSetting("RateLimiting:AnonymousPermitLimit", "2")
            .WithSetting("ForwardedHeaders:KnownProxies:0", ClientIpStartupFilter.ClientIp.ToString());
        using var client = behindProxy.CreateClient();

        for (var i = 1; i <= 3; i++)
        {
            using var response = await SendWithForwardedFor(client, $"203.0.113.{i}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static async Task<HttpResponseMessage> SendWithForwardedFor(HttpClient client, string ip)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TestEndpoints.Ping, UriKind.Relative));
        request.Headers.Add("X-Forwarded-For", ip);
        return await client.SendAsync(request);
    }

    private static async Task<(string Json, JsonElement Problem)> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return (json, document.RootElement.Clone());
    }

    private static void AssertCorrelationId(HttpResponseMessage response, JsonElement problem)
    {
        var header = Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.Equal(header, problem.GetProperty("correlationId").GetString());
    }
}
