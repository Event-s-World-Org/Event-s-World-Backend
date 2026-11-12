using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EventsWorld.Api.RateLimiting;

/// <summary>Per-IP limit for the anonymous route groups (NFR-04.2), from <c>RateLimiting</c> settings.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AnonymousPermitLimit { get; set; } = 60;
    public int AnonymousWindowSeconds { get; set; } = 60;
}

public static class RateLimitingExtensions
{
    public const string AnonymousPolicy = "anonymous";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // The client IP comes from the connection. Behind a proxy, UseForwardedHeaders replaces it
            // with the real client IP, but only for proxies listed in ForwardedHeaders settings: an
            // X-Forwarded-For header sent by anyone else is ignored, so it cannot reset the limit.
            options.AddPolicy(AnonymousPolicy, httpContext =>
            {
                var limits = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.AnonymousPermitLimit,
                    Window = TimeSpan.FromSeconds(limits.AnonymousWindowSeconds),
                    QueueLimit = 0,
                });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                var httpContext = context.HttpContext;
                var limits = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

                var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    : limits.AnonymousWindowSeconds;
                httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Detail = "Rate limit exceeded. Please try again later.",
                    },
                });
            };
        });

        return services;
    }
}
