namespace EventsWorld.Modules.Notifications.Domain;

/// <summary>
/// Domain entity representing a pending, processed, or permanently failed transactional outbox entry (database.md §3.6).
/// </summary>
internal sealed class OutboxMessage
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage Create(string type, string payload, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            Payload = payload,
            Attempts = 0,
            CreatedAt = createdAt,
            NextAttemptAt = null,
            ProcessedAt = null,
            FailedAt = null,
            LastError = null
        };
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        Attempts++;
        ProcessedAt = processedAt;
        FailedAt = null;
        NextAttemptAt = null;
        LastError = null;
    }

    public void RecordFailure(string error, DateTimeOffset nextAttemptAt)
    {
        Attempts++;
        LastError = error;
        NextAttemptAt = nextAttemptAt;
    }

    public void MarkFailedPermanently(string error, DateTimeOffset failedAt, bool incrementAttempt = true)
    {
        if (incrementAttempt)
        {
            Attempts++;
        }

        LastError = error;
        FailedAt = failedAt;
        NextAttemptAt = null;
    }
}
