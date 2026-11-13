using EventsWorld.Shared.Kernel.Results;

namespace EventsWorld.Modules.Notifications.Application.Emails;

/// <summary>
/// Abstraction for sending emails via an external provider (ADR-0005, backend.md §8).
/// </summary>
internal interface IEmailSender
{
    Task<Result<EmailSendReceipt>> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
