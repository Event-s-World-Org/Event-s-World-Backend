namespace EventsWorld.Shared.Infrastructure.Data;

/// <summary>
/// One PostgreSQL transaction per request shared by every module DbContext (ADR-0003 §6).
/// Use cases call <see cref="CommitAsync"/> once, at the end.
/// </summary>
public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
