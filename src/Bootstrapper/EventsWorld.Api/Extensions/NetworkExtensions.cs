using System.Net;
using EventsWorld.Api.Middleware;
using Microsoft.AspNetCore.HttpOverrides;

namespace EventsWorld.Api.Extensions;

/// <summary>CORS (backend.md §5) and the proxy that sits in front of the API in production.</summary>
public static class NetworkExtensions
{
    public const string CorsPolicyName = "Frontend";

    /// <summary>Response headers the frontend must be able to read from JavaScript.</summary>
    private static readonly string[] ExposedHeaders =
    [
        "ETag",
        "Location",
        "Retry-After",
        CorrelationIdMiddleware.HeaderName,
    ];

    /// <summary>
    /// Only the frontend origins in <c>Cors:AllowedOrigins</c> (development origins are in
    /// <c>appsettings.Development.json</c>). With no origins configured, no browser origin is allowed.
    /// The API uses bearer tokens, not cookies, so credentials are not allowed.
    /// </summary>
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders(ExposedHeaders)
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

        return services;
    }

    /// <summary>
    /// Trusts <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> only from the proxies in
    /// <c>ForwardedHeaders:KnownProxies</c> (IP addresses) and <c>ForwardedHeaders:KnownNetworks</c>
    /// (CIDR ranges) of the hosting platform (D-02), plus loopback.
    /// </summary>
    public static IServiceCollection AddTrustedProxies(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("ForwardedHeaders");
        var knownProxies = section.GetSection("KnownProxies").Get<string[]>() ?? [];
        var knownNetworks = section.GetSection("KnownNetworks").Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            foreach (var proxy in knownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in knownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }
}
