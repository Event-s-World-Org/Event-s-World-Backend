using EventsWorld.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;

namespace EventsWorld.Shared.Infrastructure.Api;

/// <summary>
/// Turns a business error into an RFC 9457 ProblemDetails with its stable <c>code</c>. The host
/// adds the <c>correlationId</c> to every ProblemDetails.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToProblem(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert a successful result to ProblemDetails.");
        }

        return result.Error.ToProblem();
    }

    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return TypedResults.Problem(
            detail: error.Description,
            statusCode: StatusCodeFor(error.Type),
            extensions: new Dictionary<string, object?> { [ProblemCodes.ExtensionName] = error.Code });
    }

    public static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
        _ => StatusCodes.Status500InternalServerError,
    };
}
