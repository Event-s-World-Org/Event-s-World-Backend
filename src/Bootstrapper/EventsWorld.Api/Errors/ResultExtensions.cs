using EventsWorld.Shared.Kernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace EventsWorld.Api.Errors;

public static class ResultExtensions
{
    public static IResult ToProblemDetails(this Result result, HttpContext httpContext)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert a successful result to ProblemDetails.");
        }

        return ToProblemDetails(result.Error, httpContext);
    }

    public static IResult ToProblemDetails(this Error error, HttpContext httpContext)
    {
        var correlationId = httpContext.Items["CorrelationId"]?.ToString()
            ?? httpContext.TraceIdentifier;

        var (statusCode, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Validation Error"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not Found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            _ => (StatusCodes.Status400BadRequest, "Bad Request")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = "https://datatracker.ietf.org/doc/html/rfc9457",
            Detail = error.Description,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["code"] = error.Code;
        problemDetails.Extensions["correlationId"] = correlationId;

        return Results.Problem(problemDetails);
    }
}
