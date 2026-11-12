using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EventsWorld.Api.Errors;

public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public const string ProblemDetailsContentType = "application/problem+json";

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception occurred while processing request. CorrelationId: {CorrelationId}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string correlationId);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items["CorrelationId"]?.ToString()
            ?? httpContext.TraceIdentifier;

        LogUnhandledException(logger, exception, correlationId);

        var (statusCode, problemDetails) = CreateProblemDetails(httpContext, exception, correlationId);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: ProblemDetailsContentType,
            cancellationToken: cancellationToken);
        return true;
    }

    private static (int StatusCode, ProblemDetails Details) CreateProblemDetails(
        HttpContext httpContext,
        Exception exception,
        string correlationId)
    {
        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            var validationProblem = new HttpValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457",
                Detail = "The request failed validation checks.",
                Instance = httpContext.Request.Path
            };

            validationProblem.Extensions["code"] = "validation.failed";
            validationProblem.Extensions["correlationId"] = correlationId;

            return (StatusCodes.Status400BadRequest, validationProblem);
        }

        var internalProblem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Type = "https://datatracker.ietf.org/doc/html/rfc9457",
            Detail = "An unexpected error occurred. Please contact support referencing the correlation ID.",
            Instance = httpContext.Request.Path
        };

        internalProblem.Extensions["code"] = "internal.server_error";
        internalProblem.Extensions["correlationId"] = correlationId;

        return (StatusCodes.Status500InternalServerError, internalProblem);
    }
}
