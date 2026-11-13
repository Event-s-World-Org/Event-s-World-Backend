using System.Text.Json;
using EventsWorld.Modules.Notifications.Application.Emails;
using EventsWorld.Modules.Notifications.Application.Templates;
using EventsWorld.Modules.Notifications.Domain;
using EventsWorld.Modules.Notifications.Infrastructure.Persistence;
using EventsWorld.Shared.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventsWorld.Modules.Notifications.Infrastructure;

/// <summary>
/// Background worker driven by a <see cref="PeriodicTimer"/> and guarded by a PostgreSQL advisory lock
/// that sends pending outbox emails with exponential backoff retries (backend.md §6, database.md §3.6, §4.4).
/// </summary>
internal sealed partial class OutboxWorker : ScheduledJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<NotificationsOptions> _options;
    private readonly ILogger<OutboxWorker> _logger;
    private readonly TimeProvider _timeProvider;

    public OutboxWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IOptions<NotificationsOptions> options,
        ILogger<OutboxWorker> logger,
        TimeProvider? timeProvider = null)
        : base(configuration, logger, timeProvider)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public override string JobName => "NotificationsOutbox";

    public override TimeSpan Interval => TimeSpan.FromSeconds(Math.Max(1.0, _options.Value.OutboxIntervalSeconds));

    protected override async Task RunJobAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var templateRenderer = scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();

        await ProcessPendingMessagesAsync(dbContext, emailSender, templateRenderer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Processes all pending outbox records available at the current time.</summary>
    public async Task<int> ProcessPendingMessagesAsync(
        NotificationsDbContext dbContext,
        IEmailSender emailSender,
        IEmailTemplateRenderer templateRenderer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(emailSender);
        ArgumentNullException.ThrowIfNull(templateRenderer);

        var now = _timeProvider.GetUtcNow();
        var batchSize = Math.Max(1, _options.Value.BatchSize);
        var maxAttempts = Math.Max(1, _options.Value.MaxAttempts);

        var pending = await dbContext.Outbox
            .Where(m => m.ProcessedAt == null && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pending.Count == 0)
        {
            return 0;
        }

        LogProcessingBatch(_logger, pending.Count);

        foreach (var message in pending)
        {
            string toEmail = string.Empty;
            string template = string.Empty;
            JsonDocument? doc = null;

            try
            {
                doc = JsonDocument.Parse(message.Payload);
                if (doc.RootElement.TryGetProperty("to_email", out var toProp))
                {
                    toEmail = toProp.GetString() ?? string.Empty;
                }

                if (doc.RootElement.TryGetProperty("template", out var tmplProp))
                {
                    template = tmplProp.GetString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogPayloadParseError(_logger, ex, message.Id);
                message.MarkFailedPermanently($"Invalid payload JSON: {ex.Message}");
                dbContext.EmailLogs.Add(EmailLog.Create(message.Id, "unknown", "unknown", string.Empty, "failed", now));
                continue;
            }
            finally
            {
                doc?.Dispose();
            }

            var renderResult = templateRenderer.Render(template, message.Payload);
            if (renderResult.IsFailure)
            {
                LogRenderError(_logger, message.Id, template, renderResult.Error.Description);
                var backoff = ComputeBackoff(message.Attempts);
                message.RecordFailure(renderResult.Error.Description, now + backoff);

                if (message.Attempts >= maxAttempts)
                {
                    message.MarkFailedPermanently(renderResult.Error.Description);
                    dbContext.EmailLogs.Add(EmailLog.Create(message.Id, toEmail, template, string.Empty, "failed", now));
                }

                continue;
            }

            var rendered = renderResult.Value;
            var emailMessage = new EmailMessage(
                message.Id.ToString(),
                toEmail,
                rendered.Subject,
                rendered.HtmlBody,
                rendered.TextBody);

            var sendResult = await emailSender.SendAsync(emailMessage, cancellationToken).ConfigureAwait(false);

            if (sendResult.IsSuccess)
            {
                message.MarkProcessed(now);
                dbContext.EmailLogs.Add(EmailLog.Create(
                    message.Id,
                    toEmail,
                    template,
                    sendResult.Value.ProviderMessageId,
                    "sent",
                    now));

                LogEmailSent(_logger, message.Id, toEmail, sendResult.Value.ProviderMessageId);
            }
            else
            {
                var errorDescription = sendResult.Error.Description;
                var backoff = ComputeBackoff(message.Attempts);
                message.RecordFailure(errorDescription, now + backoff);

                LogDeliveryFailed(_logger, message.Id, toEmail, message.Attempts, maxAttempts, errorDescription);

                if (message.Attempts >= maxAttempts)
                {
                    message.MarkFailedPermanently(errorDescription);
                    dbContext.EmailLogs.Add(EmailLog.Create(
                        message.Id,
                        toEmail,
                        template,
                        string.Empty,
                        "failed",
                        now));
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pending.Count;
    }

    /// <summary>Computes exponential backoff for retries: 10s, 30s, 2m, 10m, 30m, capped at 1h.</summary>
    public static TimeSpan ComputeBackoff(int attempt)
    {
        return attempt switch
        {
            <= 0 => TimeSpan.FromSeconds(10),
            1 => TimeSpan.FromSeconds(30),
            2 => TimeSpan.FromMinutes(2),
            3 => TimeSpan.FromMinutes(10),
            4 => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromHours(1)
        };
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Processing {Count} pending outbox notification(s).")]
    private static partial void LogProcessingBatch(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse JSON payload for outbox message {MessageId}.")]
    private static partial void LogPayloadParseError(ILogger logger, Exception ex, Guid messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to render template {Template} for outbox message {MessageId}: {Error}.")]
    private static partial void LogRenderError(ILogger logger, Guid messageId, string template, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully sent outbox email {MessageId} to {To} with provider ID {ProviderId}.")]
    private static partial void LogEmailSent(ILogger logger, Guid messageId, string to, string providerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Delivery failed for outbox {MessageId} to {To} (attempt {Attempt}/{MaxAttempts}): {Error}.")]
    private static partial void LogDeliveryFailed(ILogger logger, Guid messageId, string to, int attempt, int maxAttempts, string error);
}

