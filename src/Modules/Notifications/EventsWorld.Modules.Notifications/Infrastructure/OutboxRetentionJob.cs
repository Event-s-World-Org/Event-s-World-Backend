using EventsWorld.Modules.Notifications.Infrastructure.Persistence;
using EventsWorld.Shared.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventsWorld.Modules.Notifications.Infrastructure;

/// <summary>
/// Daily scheduled job that deletes processed or permanently failed outbox records and email logs
/// older than <see cref="NotificationsOptions.RetentionDays"/> (default 90 days, FR-08).
/// </summary>
internal sealed partial class OutboxRetentionJob : ScheduledJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<NotificationsOptions> _options;
    private readonly ILogger<OutboxRetentionJob> _logger;
    private readonly TimeProvider _timeProvider;

    public OutboxRetentionJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IOptions<NotificationsOptions> options,
        ILogger<OutboxRetentionJob> logger,
        TimeProvider? timeProvider = null)
        : base(configuration, logger, timeProvider)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public override string JobName => "NotificationsRetention";

    public override TimeSpan Interval => TimeSpan.FromHours(24);

    protected override bool IsEnabled => _options.Value.WorkerEnabled;

    protected override async Task RunJobAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        await PurgeExpiredRecordsAsync(dbContext, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> PurgeExpiredRecordsAsync(
        NotificationsDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var retentionDays = Math.Max(1, _options.Value.RetentionDays);
        var cutoff = _timeProvider.GetUtcNow().AddDays(-retentionDays);

        var deletedLogs = await dbContext.EmailLogs
            .Where(l => l.SentAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedOutbox = await dbContext.Outbox
            .Where(m => (m.ProcessedAt != null && m.ProcessedAt < cutoff) ||
                        (m.FailedAt != null && m.FailedAt < cutoff))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalDeleted = deletedLogs + deletedOutbox;
        if (totalDeleted > 0)
        {
            LogPurged(_logger, deletedOutbox, deletedLogs, retentionDays);
        }

        return totalDeleted;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {OutboxCount} outbox message(s) and {EmailLogCount} email log(s) older than {RetentionDays} days.")]
    private static partial void LogPurged(ILogger logger, int outboxCount, int emailLogCount, int retentionDays);
}
