using System.Text.Json;
using EventsWorld.Modules.Notifications.Application.Emails;
using EventsWorld.Modules.Notifications.Application.Templates;
using EventsWorld.Modules.Notifications.Domain;
using EventsWorld.Modules.Notifications.Infrastructure.Persistence;
using EventsWorld.Shared.Infrastructure.Jobs;
using EventsWorld.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventsWorld.Modules.Notifications.Infrastructure;

/// <summary>
/// Background worker driven by a <see cref="PeriodicTimer"/> that sends pending outbox emails
/// with <c>SELECT ... FOR UPDATE SKIP LOCKED</c> per message, exponential backoff retries,
/// <c>Retry-After</c> support on 429, and isolated per-message persistence (backend.md §6, database.md §3.6, §4.4).
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

    protected override bool IsEnabled => _options.Value.WorkerEnabled;

    /// <summary>
    /// Outbox messages are claimed individually with <c>FOR UPDATE SKIP LOCKED</c>, so multiple instances
    /// can drain the queue safely without skipping an entire interval.
    /// </summary>
    protected override bool EnforceIntervalGate => false;

    protected override async Task RunJobAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var templateRenderer = scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();

        await ProcessPendingMessagesAsync(dbContext, emailSender, templateRenderer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Processes pending outbox records available at the current time, locking and saving each message
    /// in its own transaction so that a failing message never aborts or rolls back other messages in the batch.
    /// </summary>
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

        var candidateIds = await dbContext.Outbox
            .AsNoTracking()
            .Where(m => m.ProcessedAt == null &&
                        m.FailedAt == null &&
                        (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Select(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidateIds.Count == 0)
        {
            return 0;
        }

        LogProcessingBatch(_logger, candidateIds.Count);
        var processedCount = 0;

        foreach (var candidateId in candidateIds)
        {
            try
            {
                var handled = await ProcessSingleMessageAsync(
                    dbContext,
                    emailSender,
                    templateRenderer,
                    candidateId,
                    now,
                    maxAttempts,
                    cancellationToken).ConfigureAwait(false);

                if (handled)
                {
                    processedCount++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogUnexpectedMessageError(_logger, ex, candidateId);
                dbContext.ChangeTracker.Clear();
                await TryRecordUnexpectedFailureAsync(dbContext, candidateId, ex.Message, now, maxAttempts, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return processedCount;
    }

    private async Task<bool> ProcessSingleMessageAsync(
        NotificationsDbContext dbContext,
        IEmailSender emailSender,
        IEmailTemplateRenderer templateRenderer,
        Guid messageId,
        DateTimeOffset now,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var message = await dbContext.Outbox
            .FromSqlInterpolated($"""
                SELECT *
                FROM notifications.outbox
                WHERE id = {messageId}
                  AND processed_at IS NULL
                  AND failed_at IS NULL
                FOR UPDATE SKIP LOCKED
                """)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (message is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        string toEmail = string.Empty;
        string template = message.Type;
        var payloadValid = true;

        try
        {
            using var doc = JsonDocument.Parse(message.Payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                payloadValid = false;
            }
            else
            {
                if (doc.RootElement.TryGetProperty("to_email", out var toProp) &&
                    toProp.ValueKind == JsonValueKind.String)
                {
                    toEmail = toProp.GetString() ?? string.Empty;
                }

                if (doc.RootElement.TryGetProperty("template", out var tmplProp) &&
                    tmplProp.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(tmplProp.GetString()))
                {
                    template = tmplProp.GetString()!;
                }
            }
        }
        catch (JsonException ex)
        {
            LogPayloadParseError(_logger, ex, message.Id);
            payloadValid = false;
        }

        if (!payloadValid || string.IsNullOrWhiteSpace(toEmail))
        {
            message.MarkFailedPermanently("Invalid outbox JSON payload or missing recipient email.", now, incrementAttempt: true);
            dbContext.EmailLogs.Add(EmailLog.CreateFailed(message.Id, toEmail, template, now));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        var renderResult = templateRenderer.Render(template, message.Payload);
        if (renderResult.IsFailure)
        {
            LogRenderError(_logger, message.Id, template, renderResult.Error.Description);
            message.MarkFailedPermanently(renderResult.Error.Description, now, incrementAttempt: true);
            dbContext.EmailLogs.Add(EmailLog.CreateFailed(message.Id, toEmail, template, now));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
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
            dbContext.EmailLogs.Add(EmailLog.CreateSent(
                message.Id,
                toEmail,
                template,
                sendResult.Value.ProviderMessageId,
                now));

            LogEmailSent(_logger, message.Id, sendResult.Value.ProviderMessageId);
        }
        else
        {
            var errorDescription = sendResult.Error.Description;
            var isTransient = sendResult.Error is EmailSendError sendError
                ? sendError.IsTransient
                : sendResult.Error.Type == ErrorType.Failure;
            var retryAfter = (sendResult.Error as EmailSendError)?.RetryAfter;

            if (!isTransient)
            {
                message.MarkFailedPermanently(errorDescription, now, incrementAttempt: true);
                dbContext.EmailLogs.Add(EmailLog.CreateFailed(message.Id, toEmail, template, now));
                LogPermanentFailure(_logger, message.Id, errorDescription);
            }
            else
            {
                var backoff = ComputeBackoff(message.Attempts);
                var delay = retryAfter is { } ra && ra > backoff ? ra : backoff;
                message.RecordFailure(errorDescription, now + delay);

                LogDeliveryFailed(_logger, message.Id, message.Attempts, maxAttempts, errorDescription);

                if (message.Attempts >= maxAttempts)
                {
                    message.MarkFailedPermanently(errorDescription, now, incrementAttempt: false);
                    dbContext.EmailLogs.Add(EmailLog.CreateFailed(message.Id, toEmail, template, now));
                    LogPermanentFailure(_logger, message.Id, errorDescription);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task TryRecordUnexpectedFailureAsync(
        NotificationsDbContext dbContext,
        Guid messageId,
        string errorMessage,
        DateTimeOffset now,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var message = await dbContext.Outbox.FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken).ConfigureAwait(false);
            if (message is not null && message.ProcessedAt is null && message.FailedAt is null)
            {
                var backoff = ComputeBackoff(message.Attempts);
                message.RecordFailure($"Unexpected worker error: {errorMessage}", now + backoff);
                if (message.Attempts >= maxAttempts)
                {
                    message.MarkFailedPermanently($"Unexpected worker error: {errorMessage}", now, incrementAttempt: false);
                    dbContext.EmailLogs.Add(EmailLog.CreateFailed(message.Id, "unknown@invalid.local", message.Type, now));
                }

                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
        }
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully sent outbox email {MessageId} with provider ID {ProviderId}.")]
    private static partial void LogEmailSent(ILogger logger, Guid messageId, string providerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Delivery failed for outbox {MessageId} (attempt {Attempt}/{MaxAttempts}): {Error}.")]
    private static partial void LogDeliveryFailed(ILogger logger, Guid messageId, int attempt, int maxAttempts, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} permanently failed: {Error}.")]
    private static partial void LogPermanentFailure(ILogger logger, Guid messageId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error while processing outbox message {MessageId}.")]
    private static partial void LogUnexpectedMessageError(ILogger logger, Exception ex, Guid messageId);
}
