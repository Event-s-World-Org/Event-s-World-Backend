using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EventsWorld.Api.RateLimiting;

public static class RateLimitingExtensions
{
    public const string AnonymousPolicy = "anonymous";
    public const string ProblemDetailsContentType = "application/problem+json";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var config = context.HttpContext.RequestServices.GetService<IConfiguration>() ?? configuration;
                var windowSeconds = config.GetValue("RateLimiting:AnonymousWindowSeconds", 60);

                var retryAfterSeconds = windowSeconds;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = Math.Max(1, (int)retryAfter.TotalSeconds);
                }

                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                var correlationId = context.HttpContext.Items["CorrelationId"]?.ToString()
                    ?? context.HttpContext.TraceIdentifier;

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Type = "https://datatracker.ietf.org/doc/html/rfc9457",
                    Detail = "Rate limit exceeded. Please try again later.",
                    Instance = context.HttpContext.Request.Path
                };

                problem.Extensions["code"] = "rate_limit.exceeded";
                problem.Extensions["correlationId"] = correlationId;

                await context.HttpContext.Response.WriteAsJsonAsync(
                    problem,
                    options: null,
                    contentType: ProblemDetailsContentType,
                    cancellationToken: token);
            };

            options.AddPolicy(AnonymousPolicy, httpContext =>
            {
                var config = httpContext.RequestServices.GetService<IConfiguration>() ?? configuration;
                var permitLimit = config.GetValue("RateLimiting:AnonymousPermitLimit", 60);
                var windowSeconds = config.GetValue("RateLimiting:AnonymousWindowSeconds", 60);

                var ip = httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                    ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });
        });

        return services;
    }
}
