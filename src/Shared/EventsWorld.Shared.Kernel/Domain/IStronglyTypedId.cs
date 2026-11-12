namespace EventsWorld.Shared.Kernel.Domain;

/// <summary>
/// Marker for strongly typed identifiers. Each module declares its ids as record structs backed
/// by a UUID v7 (database.md §1), for example:
/// <code>
/// internal readonly record struct EventId(Guid Value) : IStronglyTypedId&lt;Guid&gt;
/// {
///     public static EventId New() => new(Guid.CreateVersion7());
/// }
/// </code>
/// </summary>
public interface IStronglyTypedId<out TValue>
{
    TValue Value { get; }
}
