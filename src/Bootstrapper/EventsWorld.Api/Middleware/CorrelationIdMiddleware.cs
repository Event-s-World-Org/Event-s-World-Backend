using System.Text.RegularExpressions;
using Serilog.Context;

namespace EventsWorld.Api.Middleware;

/// <summary>
/// Gives every request a correlation ID: the caller's <c>X-Correlation-ID</c> when it is short and
/// safe (letters, digits and hyphens, up to 64 characters), otherwise a new one. It is returned in
/// the response header, added to every log line and to every ProblemDetails.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string LogPropertyName = "CorrelationId";

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
    private static partial Regex SafeCorrelationId();

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var received = context.Request.Headers[HeaderName].ToString();
        var correlationId = SafeCorrelationId().IsMatch(received) ? received : Guid.NewGuid().ToString("N");

        context.Items[typeof(CorrelationIdMiddleware)] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(LogPropertyName, correlationId))
        {
            await next(context);
        }
    }
}

public static class CorrelationIdExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();

    /// <summary>The request's correlation ID (falls back to the ASP.NET Core trace identifier).</summary>
    public static string GetCorrelationId(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items[typeof(CorrelationIdMiddleware)] as string ?? context.TraceIdentifier;
    }
}
