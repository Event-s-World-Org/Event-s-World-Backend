using EventsWorld.Shared.Kernel.Domain;

namespace EventsWorld.UnitTests;

public sealed class AggregateRootTests
{
    private sealed record SampleRaisedEvent(Guid AggregateId) : DomainEvent;

    private sealed class SampleAggregate : AggregateRoot<Guid>
    {
        public SampleAggregate(Guid id) : base(id) { }

        public void TriggerEvent() => RaiseDomainEvent(new SampleRaisedEvent(Id));
    }

    [Fact]
    public void RaiseAndClearDomainEvents_ShouldManageEventsCollection()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());

        aggregate.TriggerEvent();
        Assert.Single(aggregate.DomainEvents);

        aggregate.ClearDomainEvents();
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void EntitiesWithSameIdAndType_ShouldBeEqual()
    {
        var sharedId = Guid.NewGuid();
        var first = new SampleAggregate(sharedId);
        var second = new SampleAggregate(sharedId);

        Assert.Equal(first, second);
        Assert.True(first == second);
    }
}
