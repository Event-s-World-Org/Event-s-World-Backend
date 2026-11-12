using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventsWorld.Api.Endpoints;
using EventsWorld.Api.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EventsWorld.IntegrationTests;

public sealed class ApiCrossCuttingConcernsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UnhandledException_ShouldReturn500ProblemDetails_WithoutStackTrace_AndWithCorrelationId()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/api/v1/public/crash", UriKind.Relative));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc9457", root.GetProperty("type").GetString());
        Assert.Equal("internal.server_error", root.GetProperty("code").GetString());

        var correlationId = root.GetProperty("correlationId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));

        // Must NOT expose exception message or stack trace
        Assert.False(root.TryGetProperty("stackTrace", out _));
        Assert.False(root.TryGetProperty("exception", out _));
        Assert.DoesNotContain("Simulated crash", json, StringComparison.OrdinalIgnoreCase);

        // Header must be returned
        Assert.True(response.Headers.Contains(CorrelationIdMiddleware.CorrelationIdHeader));
    }

    [Fact]
    public async Task ValidationError_ShouldReturn400ProblemDetails_WithFailingFields_AndCorrelationId()
    {
        using var client = factory.CreateClient();
        var invalidPayload = new PingRequest(Name: "", Email: "not-an-email");

        using var response = await client.PostAsJsonAsync(new Uri("/api/v1/public/validate", UriKind.Relative), invalidPayload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("validation.failed", root.GetProperty("code").GetString());
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("Email", out _));

        var correlationId = root.GetProperty("correlationId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }

    [Fact]
    public async Task OpenApiAndScalar_ShouldBeAvailableInDevelopment()
    {
        using var client = factory.CreateClient();

        // 1. OpenAPI Document
        using var openApiResponse = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
        var openApiJson = await openApiResponse.Content.ReadAsStringAsync();
        Assert.Contains("openapi", openApiJson, StringComparison.OrdinalIgnoreCase);

        // 2. Scalar UI
        using var scalarResponse = await client.GetAsync(new Uri("/scalar/v1", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, scalarResponse.StatusCode);
        var scalarHtml = await scalarResponse.Content.ReadAsStringAsync();
        Assert.Contains("scalar", scalarHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SecurityHeaders_ShouldBePresentInResponses()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/api/v1/public/ping", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff));
        Assert.Contains("nosniff", nosniff);

        Assert.True(response.Headers.TryGetValues("Referrer-Policy", out var referrerPolicy));
        Assert.Contains("no-referrer", referrerPolicy);

        Assert.True(response.Headers.TryGetValues("X-Frame-Options", out var frameOptions));
        Assert.Contains("DENY", frameOptions);
    }

    [Fact]
    public async Task Cors_ShouldAllowConfiguredOrigins()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/public/ping", UriKind.Relative));
        request.Headers.Add("Origin", "http://localhost:4200");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigin));
        Assert.Contains("http://localhost:4200", allowedOrigin);
    }

    [Fact]
    public async Task HealthLive_ShouldReturn200Ok_AndHealthReady_WhenDbUnreachable_ShouldReturn503()
    {
        using var client = factory.CreateClient();

        // /health/live checks host is running
        using var liveResponse = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);

        // /health/ready checks database connection. In this test environment DB is unreachable without connection string -> 503
        using var readyResponse = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);
    }

    [Fact]
    public async Task RateLimiter_ExceedingLimit_ShouldReturn429_WithRetryAfter_AndProblemDetails()
    {
        // Custom factory with tight limit to test rate limiting
        using var rateLimitedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:AnonymousPermitLimit"] = "2",
                    ["RateLimiting:AnonymousWindowSeconds"] = "10"
                });
            });
        });

        using var client = rateLimitedFactory.CreateClient();

        // 1st request -> 200
        using var res1 = await client.GetAsync(new Uri("/api/v1/public/ping", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // 2nd request -> 200
        using var res2 = await client.GetAsync(new Uri("/api/v1/public/ping", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);

        // 3rd request -> 429 Too Many Requests
        using var res3 = await client.GetAsync(new Uri("/api/v1/public/ping", UriKind.Relative));
        Assert.Equal((HttpStatusCode)429, res3.StatusCode);

        // Must include Retry-After header
        Assert.NotNull(res3.Headers.RetryAfter);

        var json = await res3.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(429, root.GetProperty("status").GetInt32());
        Assert.Equal("rate_limit.exceeded", root.GetProperty("code").GetString());
    }
}
