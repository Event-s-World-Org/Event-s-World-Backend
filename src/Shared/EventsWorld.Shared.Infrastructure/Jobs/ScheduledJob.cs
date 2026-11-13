using System.Text;
using EventsWorld.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EventsWorld.Shared.Infrastructure.Jobs;

/// <summary>
/// Base class for periodic background tasks. Each run takes a PostgreSQL advisory lock
/// (<c>pg_try_advisory_lock</c>) so that across multiple API instances only one instance runs per interval
/// (database.md §4.4, backend.md §6).
/// </summary>
public abstract partial class ScheduledJob : BackgroundService
{
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

    /// <summary>Unique name of the job, used to generate the deterministic advisory lock ID.</summary>
    public abstract string JobName { get; }

    /// <summary>Frequency at which the job should run.</summary>
    public abstract TimeSpan Interval { get; }

    /// <summary>Deterministic 64-bit advisory lock key derived from <see cref="JobName"/>.</summary>
    public virtual long LockId => ComputeLockId(JobName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_logger, JobName, Interval);

        using var timer = new PeriodicTimer(Interval, _timeProvider);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
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

        LogStopped(_logger, JobName);
    }

    /// <summary>
    /// Attempts to acquire the advisory lock and run the job once.
    /// Returns <c>true</c> if the lock was acquired and the job executed, or <c>false</c> if another instance holds the lock.
    /// </summary>
    public async Task<bool> TryExecuteOnceAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString(ConnectionStringNames.Database);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            LogMissingConnectionString(_logger, JobName);
            return false;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var lockCommand = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key);", connection);
        lockCommand.Parameters.AddWithValue("key", LockId);

        var result = await lockCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var acquired = result is true or 1 or 1L;

        if (!acquired)
        {
            LogLockHeld(_logger, JobName, LockId);
            return false;
        }

        try
        {
            LogLockAcquired(_logger, LockId, JobName);
            await RunJobAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            try
            {
                await using var unlockCommand = new NpgsqlCommand("SELECT pg_advisory_unlock(@key);", connection);
                unlockCommand.Parameters.AddWithValue("key", LockId);
                await unlockCommand.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false);
                LogLockReleased(_logger, LockId, JobName);
            }
            catch (Exception ex)
            {
                LogUnlockFailed(_logger, ex, LockId, JobName);
            }
        }
    }

    /// <summary>The actual work executed when this instance holds the advisory lock.</summary>
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Acquired advisory lock {LockId} for job {JobName}.")]
    private static partial void LogLockAcquired(ILogger logger, long lockId, string jobName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Released advisory lock {LockId} for job {JobName}.")]
    private static partial void LogLockReleased(ILogger logger, long lockId, string jobName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to release advisory lock {LockId} for job {JobName}.")]
    private static partial void LogUnlockFailed(ILogger logger, Exception ex, long lockId, string jobName);
}
