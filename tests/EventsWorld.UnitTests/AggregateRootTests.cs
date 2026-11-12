using EventsWorld.Shared.Kernel.Domain;

namespace EventsWorld.UnitTests;

public sealed class AggregateRootTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private sealed record SampleRaisedEvent(Guid AggregateId, DateTimeOffset OccurredOnUtc)
        : DomainEvent(OccurredOnUtc);

    private sealed class SampleAggregate : AggregateRoot<Guid>
    {
        public SampleAggregate(Guid id) : base(id) { }

        public void TriggerEvent() => RaiseDomainEvent(new SampleRaisedEvent(Id, Now));
    }

    [Fact]
    public void DomainEvent_ShouldKeepTheGivenTime_AndUseAVersion7Id()
    {
        var domainEvent = new SampleRaisedEvent(Guid.CreateVersion7(), Now);

        Assert.Equal(Now, domainEvent.OccurredOnUtc);
        Assert.Equal(7, domainEvent.EventId.Version);
    }

    [Fact]
    public void RaiseAndClearDomainEvents_ShouldManageEventsCollection()
    {
        var aggregate = new SampleAggregate(Guid.CreateVersion7());

        aggregate.TriggerEvent();
        Assert.Single(aggregate.DomainEvents);

        aggregate.ClearDomainEvents();
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void EntitiesWithSameIdAndType_ShouldBeEqual()
    {
        var sharedId = Guid.CreateVersion7();
        var first = new SampleAggregate(sharedId);
        var second = new SampleAggregate(sharedId);

        Assert.Equal(first, second);
        Assert.True(first == second);
    }
}
