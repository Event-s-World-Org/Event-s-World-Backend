using System.Data.Common;
using EventsWorld.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EventsWorld.Shared.Infrastructure.Data;

/// <summary>
/// Coordinates writes across module DbContexts in a single request (ADR-0003 §6, database.md §4.2).
/// Every <see cref="CommitAsync"/> guarantees a single shared PostgreSQL connection and transaction
/// across all participating module contexts, even if <see cref="BeginTransactionAsync"/> was not called explicitly.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork, IDisposable, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<ModuleDatabase> _moduleDatabases;
    private readonly List<DbContext> _enlistedContexts = [];
    private IDbContextTransaction? _currentTransaction;
    private DbContext? _transactionOwner;

    public UnitOfWork(IServiceProvider serviceProvider, IEnumerable<ModuleDatabase> moduleDatabases)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _moduleDatabases = moduleDatabases ?? throw new ArgumentNullException(nameof(moduleDatabases));
    }

    /// <summary>Starts a shared database transaction if one is not already active.</summary>
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
        {
            return _currentTransaction;
        }

        DiscoverResolvedContexts();

        _transactionOwner = _enlistedContexts.FirstOrDefault();
        if (_transactionOwner is null)
        {
            var firstModule = _moduleDatabases.FirstOrDefault()
                ?? throw new InvalidOperationException("No module database contexts registered.");

            _transactionOwner = (DbContext)_serviceProvider.GetRequiredService(firstModule.ContextType);
        }

        _currentTransaction = await _transactionOwner.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        Enlist(_transactionOwner);

        foreach (var context in _enlistedContexts.ToArray())
        {
            if (context != _transactionOwner)
            {
                AttachToCurrentTransaction(context);
            }
        }

        return _currentTransaction;
    }

    /// <summary>Enlists a <see cref="DbContext"/> into the unit of work and attaches it to the active transaction.</summary>
    public void Enlist(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_enlistedContexts.Contains(context))
        {
            _enlistedContexts.Add(context);
        }

        if (_currentTransaction is not null && context != _transactionOwner)
        {
            AttachToCurrentTransaction(context);
        }
    }

    /// <summary>
    /// Opens a shared transaction (if one is not already active), saves changes across all
    /// participating module contexts, and commits atomically. Rolls back if any save fails.
    /// </summary>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        DiscoverResolvedContexts();

        var contextsWithChanges = _enlistedContexts
            .Where(c => c.ChangeTracker.HasChanges())
            .ToList();

        if (contextsWithChanges.Count == 0 && _currentTransaction is null)
        {
            return;
        }

        if (_currentTransaction is null)
        {
            await BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            foreach (var context in _enlistedContexts)
            {
                if (context != _transactionOwner)
                {
                    AttachToCurrentTransaction(context);
                }
            }
        }

        try
        {
            foreach (var context in _enlistedContexts)
            {
                if (context.ChangeTracker.HasChanges())
                {
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            if (_currentTransaction is not null)
            {
                await _currentTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                await CleanupTransactionAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            await RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Rolls back the shared transaction if active and clears pending change tracker entries.</summary>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        DiscoverResolvedContexts();

        if (_currentTransaction is not null)
        {
            try
            {
                await _currentTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await CleanupTransactionAsync().ConfigureAwait(false);
            }
        }

        foreach (var context in _enlistedContexts)
        {
            context.ChangeTracker.Clear();
        }
    }

    private void DiscoverResolvedContexts()
    {
        foreach (var module in _moduleDatabases)
        {
            if (_serviceProvider.GetService(module.ContextType) is DbContext context &&
                !_enlistedContexts.Contains(context))
            {
                _enlistedContexts.Add(context);
            }
        }
    }

    private void AttachToCurrentTransaction(DbContext context)
    {
        if (_currentTransaction is null || context == _transactionOwner)
        {
            return;
        }

        var dbTransaction = _currentTransaction.GetDbTransaction();
        var sharedConnection = dbTransaction.Connection;

        if (sharedConnection is not null &&
            !ReferenceEquals(context.Database.GetDbConnection(), sharedConnection))
        {
            context.Database.CloseConnection();
            context.Database.SetDbConnection(sharedConnection);
        }

        if (!ReferenceEquals(context.Database.CurrentTransaction?.GetDbTransaction(), dbTransaction))
        {
            context.Database.UseTransaction(dbTransaction);
        }
    }

    private async Task CleanupTransactionAsync()
    {
        foreach (var context in _enlistedContexts)
        {
            if (context != _transactionOwner && context.Database.CurrentTransaction is not null)
            {
                await context.Database.UseTransactionAsync(null).ConfigureAwait(false);
            }
        }

        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync().ConfigureAwait(false);
            _currentTransaction = null;
        }

        _transactionOwner = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync().ConfigureAwait(false);
            _currentTransaction = null;
        }

        _transactionOwner = null;
        _enlistedContexts.Clear();
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _currentTransaction = null;
        _transactionOwner = null;
        _enlistedContexts.Clear();
    }
}
