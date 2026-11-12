namespace EventsWorld.Shared.Kernel.Results;

/// <summary>
/// A business error. <see cref="Code"/> follows the <c>module.snake_case</c> convention
/// (for example <c>registrations.capacity_full</c>): the API returns it in ProblemDetails and the
/// frontend maps it to a Spanish message.
/// </summary>
public record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);
    public static readonly Error NullValue = new("general.null_value", "The specified result value is null.", ErrorType.Failure);

    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);
    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);
    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);
    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);
    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);
    public static Error PreconditionFailed(string code, string description) => new(code, description, ErrorType.PreconditionFailed);
}

/// <summary>
/// Maps to HTTP status codes: 500, 400, 404, 409, 401, 403 and 412 (a stale <c>If-Match</c>
/// ETag, backend.md §4).
/// </summary>
public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    PreconditionFailed = 6
}
