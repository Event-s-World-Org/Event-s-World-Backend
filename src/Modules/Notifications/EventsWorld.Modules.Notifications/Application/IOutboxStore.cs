using EventsWorld.Modules.Notifications.Domain;

namespace EventsWorld.Modules.Notifications.Application;

/// <summary>Abstraction allowing the Application layer to stage outbox messages without depending on Infrastructure.</summary>
internal interface IOutboxStore
{
    void Add(OutboxMessage message);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
