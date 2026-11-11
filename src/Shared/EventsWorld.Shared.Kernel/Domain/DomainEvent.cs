namespace EventsWorld.Shared.Kernel.Domain;

public class DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid( );
    public DateTimeOffset OccurredOnUtc { get; init; } = DateTimeOffset.UtcNow;
}
