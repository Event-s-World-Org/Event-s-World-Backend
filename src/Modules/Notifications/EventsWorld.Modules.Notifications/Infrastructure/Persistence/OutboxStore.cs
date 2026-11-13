using EventsWorld.Modules.Notifications.Application;
using EventsWorld.Modules.Notifications.Domain;
using EventsWorld.Shared.Infrastructure.Data;

namespace EventsWorld.Modules.Notifications.Infrastructure.Persistence;

internal sealed class OutboxStore(NotificationsDbContext dbContext, IUnitOfWork unitOfWork) : IOutboxStore
{
    public void Add(OutboxMessage message)
    {
        dbContext.Outbox.Add(message);
        unitOfWork.Enlist(dbContext);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

