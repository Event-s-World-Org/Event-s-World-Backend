using EventsWorld.Shared.Kernel.Results;

namespace EventsWorld.Modules.Notifications.Application.Emails;

/// <summary>Represents an outgoing email message.</summary>
internal sealed record EmailMessage(
    string IdempotencyKey,
    string To,
    string Subject,
    string HtmlBody,
    string TextBody);

/// <summary>Receipt returned by the email provider upon acceptance.</summary>
internal sealed record EmailSendReceipt(string ProviderMessageId);

/// <summary>
/// Distinguishes transient provider failures (5xx, 429, network timeouts) from permanent 4xx rejections,
/// and carries an optional <c>Retry-After</c> delay when rate-limited (429).
/// </summary>
internal sealed record EmailSendError(
    string Code,
    string Description,
    bool IsTransient,
    TimeSpan? RetryAfter = null)
    : Error(Code, Description, IsTransient ? ErrorType.Failure : ErrorType.Validation)
{
    public static EmailSendError Transient(string code, string description, TimeSpan? retryAfter = null) =>
        new(code, description, IsTransient: true, retryAfter);

    public static EmailSendError Permanent(string code, string description) =>
        new(code, description, IsTransient: false);
}
