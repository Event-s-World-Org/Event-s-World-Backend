using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EventsWorld.Shared.Infrastructure.Data;

/// <summary>
/// One PostgreSQL transaction per request shared by every module DbContext (ADR-0003 §6).
/// Use cases call <see cref="CommitAsync"/> once, at the end.
/// </summary>
public interface IUnitOfWork
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    void Enlist(DbContext context);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
