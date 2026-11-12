namespace EventsWorld.Shared.Kernel.Domain;

/// <summary>
/// Base for domain events. The time comes from the caller (a <c>TimeProvider</c> in the
/// application layer) so tests can use a fake clock; the id is a time-ordered UUID v7.
/// </summary>
public abstract record DomainEvent(DateTimeOffset OccurredOnUtc) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7(OccurredOnUtc);
}
