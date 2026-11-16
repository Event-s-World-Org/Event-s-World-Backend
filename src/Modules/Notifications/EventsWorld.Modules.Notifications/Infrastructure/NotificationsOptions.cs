namespace EventsWorld.Modules.Notifications.Infrastructure;

/// <summary>Options for the notifications outbox worker, retry policy, and retention cleanup (database.md §3.6, §4.4, FR-08).</summary>
internal sealed class NotificationsOptions
{
    public const string Section = "Notifications";

    /// <summary>Whether the background outbox and retention workers run automatically (disabled in integration tests).</summary>
    public bool WorkerEnabled { get; set; } = true;

    /// <summary>How often the outbox worker checks for pending notifications (in seconds).</summary>
    public double OutboxIntervalSeconds { get; set; } = 5.0;

    /// <summary>
    /// Maximum delivery attempts before marking a message as permanently failed.
    /// Default 30 (~43 minutes for the first 5 attempts + 25 hourly attempts &gt; 24 hours).
    /// </summary>
    public int MaxAttempts { get; set; } = 30;

    /// <summary>Number of pending messages processed per cycle.</summary>
    public int BatchSize { get; set; } = 25;

    /// <summary>Retention period in days for processed/failed outbox messages and email logs (FR-08).</summary>
    public int RetentionDays { get; set; } = 90;
}
