using System.Text.Json;
using EventsWorld.Modules.Notifications.Contracts;
using EventsWorld.Modules.Notifications.Domain;

namespace EventsWorld.Modules.Notifications.Application;

/// <summary>
/// Implements <see cref="INotificationsModule"/> by staging messages into the outbox
/// inside the caller's unit of work transaction (database.md §3.6, ADR-0003 §5–6).
/// </summary>
internal sealed class NotificationsService : INotificationsModule
{
    private readonly IOutboxStore _outboxStore;
    private readonly TimeProvider _timeProvider;

    public NotificationsService(
        IOutboxStore outboxStore,
        TimeProvider? timeProvider = null)
    {
        _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void Enqueue(NotificationEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(email.Template);
        ArgumentException.ThrowIfNullOrWhiteSpace(email.ToEmail);

        var payloadJson = JsonSerializer.Serialize(email, email.GetType());
        var outboxMessage = OutboxMessage.Create(email.Template, payloadJson, _timeProvider.GetUtcNow());

        _outboxStore.Add(outboxMessage);
    }
}
