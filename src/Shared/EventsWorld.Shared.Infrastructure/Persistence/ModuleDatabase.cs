namespace EventsWorld.Shared.Infrastructure.Persistence;

/// <summary>
/// A module's DbContext and the schema it owns (database.md §2). One instance per module is
/// registered in DI so the migrator can find every context.
/// </summary>
public sealed record ModuleDatabase(Type ContextType, string Schema);
