namespace EventsWorld.Shared.Infrastructure.Audit;

public interface IAuditWriter
{
    Task WriteAuditAsync(string action, string entityName, string entityId, object? payload, CancellationToken cancellationToken = default);
}
