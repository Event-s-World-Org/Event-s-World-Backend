using EventsWorld.Shared.Infrastructure.Api;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EventsWorld.Api.Errors;

/// <summary>
/// Turns exceptions into RFC 9457 ProblemDetails. The exception message and stack trace never reach
/// the response; the correlation ID lets support find the log entry.
/// </summary>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // The client closed the connection: nothing to answer and not a server error.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogRequestAborted(logger, httpContext.Request.Path);
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        var problem = exception switch
        {
            // Malformed JSON, a missing body or a wrong parameter type (minimal APIs).
            BadHttpRequestException badRequest => new ProblemDetails
            {
                Status = badRequest.StatusCode,
                Detail = "The request could not be read.",
                Extensions = { [ProblemCodes.ExtensionName] = ProblemCodes.InvalidRequest },
            },
            ValidationException validation => new HttpValidationProblemDetails(
                validation.Errors
                    .GroupBy(error => error.PropertyName)
                    .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = "The request failed validation checks.",
                Extensions = { [ProblemCodes.ExtensionName] = ProblemCodes.ValidationFailed },
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Detail = "An unexpected error occurred. Please contact support with the correlation ID.",
                Extensions = { [ProblemCodes.ExtensionName] = ProblemCodes.InternalError },
            },
        };

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, exception);
        }
        else
        {
            LogRejectedRequest(logger, problem.Status ?? 0);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing the request.")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request rejected with {StatusCode}.")]
    private static partial void LogRejectedRequest(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Request to {Path} aborted by the client.")]
    private static partial void LogRequestAborted(ILogger logger, PathString path);
}
