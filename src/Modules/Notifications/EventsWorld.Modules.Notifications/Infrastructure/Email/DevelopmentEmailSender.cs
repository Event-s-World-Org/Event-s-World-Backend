using System.Collections.Concurrent;
using EventsWorld.Modules.Notifications.Application.Emails;
using EventsWorld.Shared.Kernel.Results;
using Microsoft.Extensions.Logging;

namespace EventsWorld.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// Development and test email sender that logs outgoing emails without contacting an external service.
/// Stores sent messages in memory for test assertions.
/// </summary>
internal sealed partial class DevelopmentEmailSender : IEmailSender
{
    private readonly ILogger<DevelopmentEmailSender> _logger;
    private readonly ConcurrentBag<EmailMessage> _sentMessages = [];

    public DevelopmentEmailSender(ILogger<DevelopmentEmailSender> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Messages sent through this sender during the application lifetime.</summary>
    public IReadOnlyCollection<EmailMessage> SentMessages => _sentMessages.ToArray();

    public Task<Result<EmailSendReceipt>> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var providerId = $"dev_{Guid.CreateVersion7():N}";
        _sentMessages.Add(message);

        LogEmailDispatched(_logger, message.To, message.Subject, message.IdempotencyKey, providerId);

        return Task.FromResult(Result<EmailSendReceipt>.Success(new EmailSendReceipt(providerId)));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[DEV EMAIL] To: {To} | Subject: '{Subject}' | Outbox: {IdempotencyKey} | Provider ID: {ProviderId}")]
    private static partial void LogEmailDispatched(
        ILogger logger,
        string to,
        string subject,
        string idempotencyKey,
        string providerId);
}
