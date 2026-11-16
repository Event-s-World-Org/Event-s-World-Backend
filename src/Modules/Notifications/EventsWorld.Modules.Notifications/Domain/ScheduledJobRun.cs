namespace EventsWorld.Modules.Notifications.Domain;

/// <summary>
/// Tracks the last execution timestamp of each scheduled background job so that across multiple
/// API instances a job runs at most once per interval (database.md §4.4).
/// </summary>
internal sealed class ScheduledJobRun
{
    public string JobName { get; private set; } = string.Empty;
    public DateTimeOffset LastRunAt { get; private set; }

    private ScheduledJobRun()
    {
    }

    public ScheduledJobRun(string jobName, DateTimeOffset lastRunAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        JobName = jobName;
        LastRunAt = lastRunAt;
    }
}
