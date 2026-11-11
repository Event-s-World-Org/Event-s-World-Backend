namespace EventsWorld.Shared.Kernel.Domain;

public interface IStronglyTypedId<out TValue>
{
    TValue Value { get; }
}
