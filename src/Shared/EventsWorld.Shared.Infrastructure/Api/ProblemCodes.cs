using Microsoft.AspNetCore.Http;

namespace EventsWorld.Shared.Infrastructure.Api;

/// <summary>
/// Stable <c>code</c> values for errors that do not come from a module (<c>module.snake_case</c>).
/// The frontend maps them, or the status, to Spanish messages; it never shows <c>detail</c>.
/// </summary>
public static class ProblemCodes
{
    public const string ExtensionName = "code";

    public const string ValidationFailed = "validation.failed";
    public const string InvalidRequest = "request.invalid";
    public const string NotFound = "request.not_found";
    public const string MethodNotAllowed = "request.method_not_allowed";
    public const string Conflict = "request.conflict";
    public const string PreconditionFailed = "request.precondition_failed";
    public const string PayloadTooLarge = "request.too_large";
    public const string UnsupportedMediaType = "request.unsupported_media_type";
    public const string Unauthorized = "auth.unauthorized";
    public const string Forbidden = "auth.forbidden";
    public const string RateLimitExceeded = "rate_limit.exceeded";
    public const string InternalError = "internal.server_error";

    /// <summary>The default code for a status when nothing more specific is known.</summary>
    public static string ForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => Unauthorized,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
        StatusCodes.Status409Conflict => Conflict,
        StatusCodes.Status412PreconditionFailed => PreconditionFailed,
        StatusCodes.Status413PayloadTooLarge => PayloadTooLarge,
        StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaType,
        StatusCodes.Status429TooManyRequests => RateLimitExceeded,
        >= 500 => InternalError,
        _ => InvalidRequest,
    };
}
