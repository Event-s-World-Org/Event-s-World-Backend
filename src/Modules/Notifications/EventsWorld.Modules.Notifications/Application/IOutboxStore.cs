using EventsWorld.Modules.Notifications.Domain;

namespace EventsWorld.Modules.Notifications.Application;

/// <summary>
/// Application port for staging outbox messages inside the current unit of work (ADR-0003).
/// </summary>
internal interface IOutboxStore
{
    void Add(OutboxMessage message);
}
