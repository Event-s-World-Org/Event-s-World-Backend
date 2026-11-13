using System.Data.Common;
using EventsWorld.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EventsWorld.Shared.Infrastructure.Data;

/// <summary>
/// Coordinates writes across module DbContexts in a single request (ADR-0003 §6).
/// When a cross-module transaction is started, all participating DbContexts enlist in it,
/// and <see cref="CommitAsync"/> saves all changes and commits atomically.
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

        // Use the first resolved DbContext (or resolve the first registered one) to initiate the transaction
        var firstModule = _moduleDatabases.FirstOrDefault()
            ?? throw new InvalidOperationException("No module database contexts registered.");

        _transactionOwner = (DbContext)_serviceProvider.GetRequiredService(firstModule.ContextType);
        _currentTransaction = await _transactionOwner.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        Enlist(_transactionOwner);

        // Enlist any other already resolved contexts
        foreach (var module in _moduleDatabases)
        {
            if (_serviceProvider.GetService(module.ContextType) is DbContext context && context != _transactionOwner)
            {
                Enlist(context);
            }
        }

        return _currentTransaction;
    }

    /// <summary>Enlists an existing DbContext into the current transaction if active.</summary>
    public void Enlist(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_enlistedContexts.Contains(context))
        {
            _enlistedContexts.Add(context);
        }

        if (_currentTransaction is not null &&
            context != _transactionOwner &&
            context.Database.CurrentTransaction is null)
        {
            var dbTransaction = _currentTransaction.GetDbTransaction();
            var sharedConnection = dbTransaction.Connection;
            if (sharedConnection is not null && context.Database.GetDbConnection() != sharedConnection)
            {
                context.Database.SetDbConnection(sharedConnection);
            }
            context.Database.UseTransaction(dbTransaction);
        }
    }

    /// <summary>Saves all changes in all enlisted contexts and commits the shared transaction.</summary>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        // Enlist all module contexts that have been resolved and have pending changes
        foreach (var module in _moduleDatabases)
        {
            if (_serviceProvider.GetService(module.ContextType) is DbContext context)
            {
                Enlist(context);
            }
        }

        // Save changes across all enlisted contexts
        foreach (var context in _enlistedContexts)
        {
            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // Commit transaction if one was initiated by UnitOfWork
        if (_currentTransaction is not null)
        {
            await _currentTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            await _currentTransaction.DisposeAsync().ConfigureAwait(false);
            _currentTransaction = null;
        }
    }

    /// <summary>Rolls back the shared transaction if active.</summary>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            await _currentTransaction.DisposeAsync().ConfigureAwait(false);
            _currentTransaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync().ConfigureAwait(false);
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _currentTransaction = null;
    }
}
