using EventsWorld.Modules.Notifications.Application.Emails;
using EventsWorld.Shared.Kernel.Results;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EventsWorld.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// Development SMTP email sender (using MailKit) for sending emails to a local catcher such as Mailpit
/// (<c>localhost:1025</c>, web UI at <c>http://localhost:8025</c>).
/// </summary>
internal sealed partial class SmtpEmailSender : IEmailSender
{
    private readonly IOptions<ResendOptions> _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<ResendOptions> options,
        ILogger<SmtpEmailSender> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<EmailSendReceipt>> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var opts = _options.Value;
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(opts.SenderName, opts.SenderEmail));

        if (!MailboxAddress.TryParse(message.To, out var recipient))
        {
            return Result.Failure<EmailSendReceipt>(
                EmailSendError.Permanent("notifications.invalid_recipient", "Recipient email address is invalid."));
        }

        mimeMessage.To.Add(recipient);
        mimeMessage.Subject = message.Subject;

        if (!string.IsNullOrWhiteSpace(message.IdempotencyKey))
        {
            mimeMessage.Headers.Add("X-Idempotency-Key", message.IdempotencyKey);
        }

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        };
        mimeMessage.Body = bodyBuilder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(opts.SmtpHost, opts.SmtpPort, SecureSocketOptions.None, cancellationToken).ConfigureAwait(false);
            var serverResponse = await client.SendAsync(mimeMessage, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);

            var providerId = string.IsNullOrWhiteSpace(mimeMessage.MessageId)
                ? $"smtp_{Guid.CreateVersion7():N}"
                : mimeMessage.MessageId;

            LogSmtpSent(_logger, providerId, message.IdempotencyKey, serverResponse);
            return Result.Success(new EmailSendReceipt(providerId));
        }
        catch (Exception ex)
        {
            LogSmtpFailed(_logger, ex, message.IdempotencyKey);
            return Result.Failure<EmailSendReceipt>(
                EmailSendError.Transient("notifications.smtp_error", "Failed to deliver email to local SMTP server."));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Delivered email via SMTP with ID {ProviderId} for outbox {IdempotencyKey} ({ServerResponse}).")]
    private static partial void LogSmtpSent(ILogger logger, string providerId, string idempotencyKey, string? serverResponse);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to deliver email via SMTP for outbox {IdempotencyKey}.")]
    private static partial void LogSmtpFailed(ILogger logger, Exception ex, string idempotencyKey);
}
