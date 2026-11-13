namespace EventsWorld.Modules.Notifications.Infrastructure;

/// <summary>Options for the notifications outbox worker and retry policy (database.md §3.6, §4.4).</summary>
internal sealed class NotificationsOptions
{
    public const string Section = "Notifications";

    /// <summary>How often the outbox worker checks for pending notifications (in seconds).</summary>
    public double OutboxIntervalSeconds { get; set; } = 5.0;

    /// <summary>Maximum delivery attempts before abandoning a message.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Number of pending messages processed per cycle.</summary>
    public int BatchSize { get; set; } = 25;
}
