namespace EventsWorld.Shared.Infrastructure.Outbox;

public interface IOutboxWriter
{
    Task WriteAsync(object domainEvent, CancellationToken cancellationToken = default);
}
