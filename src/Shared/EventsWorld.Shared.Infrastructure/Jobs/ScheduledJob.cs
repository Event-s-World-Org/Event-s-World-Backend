using System.Text;
using EventsWorld.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EventsWorld.Shared.Infrastructure.Jobs;

/// <summary>
/// Base class for periodic background tasks (database.md §4.4, backend.md §6).
/// Each execution opens a transaction, takes a transaction-scoped PostgreSQL advisory lock
/// (<c>pg_try_advisory_xact_lock</c>, compatible with the Supabase transaction pooler on port 6543),
/// and records the run timestamp in <c>notifications.scheduled_job_runs</c> so that across multiple
/// API instances the job runs at most once per interval.
/// If the hosting environment sleeps when idle (D-02, backend.md §6), <see cref="TryExecuteOnceAsync"/>
/// can also be triggered by a protected internal cron endpoint.
/// </summary>
public abstract partial class ScheduledJob : BackgroundService
{
    private const string UpsertJobRunSql = """
        INSERT INTO notifications.scheduled_job_runs (job_name, last_run_at)
        VALUES (@name, @now)
        ON CONFLICT (job_name) DO UPDATE
        SET last_run_at = EXCLUDED.last_run_at
        WHERE notifications.scheduled_job_runs.last_run_at <= @cutoff;
        """;

    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;

    protected ScheduledJob(
        IConfiguration configuration,
        ILogger logger,
        TimeProvider? timeProvider = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Unique name of the job, used as the primary key in <c>scheduled_job_runs</c> and to derive <see cref="LockId"/>.</summary>
    public abstract string JobName { get; }

    /// <summary>Frequency at which the job should run.</summary>
    public abstract TimeSpan Interval { get; }

    /// <summary>Whether the background timer loop is enabled.</summary>
    protected virtual bool IsEnabled => true;

    /// <summary>Whether to enforce the <c>notifications.scheduled_job_runs</c> interval gate inside the advisory lock.</summary>
    protected virtual bool EnforceIntervalGate => true;

    /// <summary>Deterministic 64-bit advisory lock key derived from <see cref="JobName"/>.</summary>
    public virtual long LockId => ComputeLockId(JobName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            LogDisabled(_logger, JobName);
            return;
        }

        LogStarted(_logger, JobName, Interval);

        using var timer = new PeriodicTimer(Interval, _timeProvider);

        do
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await TryExecuteOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogJobError(_logger, ex, JobName);
            }
        }
        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));

        LogStopped(_logger, JobName);
    }

    /// <summary>
    /// Attempts to acquire the transaction-scoped advisory lock, verify that the job has not already run
    /// in the current interval, and execute the job once.
    /// Returns <c>true</c> if the job ran, or <c>false</c> if another instance holds the lock or already ran it in this interval.
    /// </summary>
    public async Task<bool> TryExecuteOnceAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString(ConnectionStringNames.Database);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            LogMissingConnectionString(_logger, JobName);
            return false;
        }

        var poolerConnectionString = PersistenceServiceCollectionExtensions.ForTransactionPooler(connectionString);
        await using var connection = new NpgsqlConnection(poolerConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using var lockCommand = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock(@key);", connection, transaction);
        lockCommand.Parameters.AddWithValue("key", LockId);

        var result = await lockCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var acquired = result is true or 1 or 1L;

        if (!acquired)
        {
            LogLockHeld(_logger, JobName, LockId);
            return false;
        }

        if (EnforceIntervalGate)
        {
            var now = _timeProvider.GetUtcNow();
            var cutoff = now - Interval;

            await using var gateCommand = new NpgsqlCommand(UpsertJobRunSql, connection, transaction);
            gateCommand.Parameters.AddWithValue("name", JobName);
            gateCommand.Parameters.AddWithValue("now", now);
            gateCommand.Parameters.AddWithValue("cutoff", cutoff);

            var rowsAffected = await gateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (rowsAffected == 0)
            {
                LogAlreadyRanInInterval(_logger, JobName);
                return false;
            }
        }

        LogLockAcquired(_logger, LockId, JobName);
        await RunJobAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        LogLockReleased(_logger, LockId, JobName);
        return true;
    }

    /// <summary>The actual work executed when this instance holds the advisory lock for the current interval.</summary>
    protected abstract Task RunJobAsync(CancellationToken cancellationToken);

    /// <summary>Computes a stable 64-bit FNV-1a hash from the job name.</summary>
    public static long ComputeLockId(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        ulong hash = 14695981039346656037UL;
        foreach (var b in Encoding.UTF8.GetBytes(name))
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }

        return unchecked((long)hash);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduled job {JobName} is disabled by configuration.")]
    private static partial void LogDisabled(ILogger logger, string jobName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduled job {JobName} started with interval {Interval}.")]
    private static partial void LogStarted(ILogger logger, string jobName, TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduled job {JobName} stopped.")]
    private static partial void LogStopped(ILogger logger, string jobName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled error in scheduled job {JobName}.")]
    private static partial void LogJobError(ILogger logger, Exception ex, string jobName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled job {JobName} skipped: no database connection string configured.")]
    private static partial void LogMissingConnectionString(ILogger logger, string jobName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scheduled job {JobName} skipped: advisory lock {LockId} is held by another instance.")]
    private static partial void LogLockHeld(ILogger logger, string jobName, long lockId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scheduled job {JobName} skipped: another instance already executed within the current interval.")]
    private static partial void LogAlreadyRanInInterval(ILogger logger, string jobName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Acquired transaction advisory lock {LockId} for job {JobName}.")]
    private static partial void LogLockAcquired(ILogger logger, long lockId, string jobName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Completed job {JobName} and released transaction advisory lock {LockId}.")]
    private static partial void LogLockReleased(ILogger logger, long lockId, string jobName);
}
