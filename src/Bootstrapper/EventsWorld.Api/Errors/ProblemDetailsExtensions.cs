using EventsWorld.Api.Middleware;
using EventsWorld.Shared.Infrastructure.Api;

namespace EventsWorld.Api.Errors;

public static class ProblemDetailsExtensions
{
    public const string CorrelationIdExtension = "correlationId";

    /// <summary>
    /// One place for what every error response carries (RFC 9457): the request path as
    /// <c>instance</c>, the <c>correlationId</c> and a stable <c>code</c> (by status when the
    /// error did not set one). It applies to the exception handler, validation, the rate limiter,
    /// <c>Result</c> errors and empty 4xx/5xx responses (status code pages).
    /// </summary>
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            var status = problem.Status ?? context.HttpContext.Response.StatusCode;

            problem.Instance ??= context.HttpContext.Request.Path;
            problem.Extensions[CorrelationIdExtension] = context.HttpContext.GetCorrelationId();
            problem.Extensions.TryAdd(ProblemCodes.ExtensionName, ProblemCodes.ForStatus(status));
        });

        // Bad requests (malformed JSON, missing body) throw in every environment, so they always
        // reach the exception handler and get the same 400 ProblemDetails.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}
