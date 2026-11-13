namespace EventsWorld.Modules.Notifications.Contracts;

/// <summary>
/// Public contract of the Notifications module (backend.md §2, database.md §3.6).
/// Enqueues transactional notifications into the outbox within the caller's database transaction.
/// </summary>
public interface INotificationsModule
{
    /// <summary>Enqueues an outbox message inside the current unit of work transaction.</summary>
    void Enqueue(OutboxMessageDto message);

    /// <summary>Enqueues an outbox message inside the current unit of work transaction.</summary>
    void Enqueue(string type, string toEmail, string template, object payload);

    /// <summary>Enqueues an outbox message and saves changes within the active transaction.</summary>
    Task EnqueueAsync(
        string type,
        string toEmail,
        string template,
        object payload,
        CancellationToken cancellationToken = default);
}
