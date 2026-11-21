namespace EventsWorld.Modules.Notifications.Contracts;

/// <summary>
/// Public contract of the Notifications module (ADR-0003, database.md §3.6, backend.md §3).
/// Stages transactional emails into <c>notifications.outbox</c> within the caller's unit of work;
/// the email is persisted only when <c>IUnitOfWork.CommitAsync</c> succeeds.
/// </summary>
public interface INotificationsModule
{
    /// <summary>
    /// Stages a strongly-typed transactional email into the outbox within the active unit of work.
    /// </summary>
    void Enqueue(NotificationEmail email);
}
