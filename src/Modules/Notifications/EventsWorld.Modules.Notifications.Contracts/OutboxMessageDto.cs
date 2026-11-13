namespace EventsWorld.Modules.Notifications.Contracts;

/// <summary>Represents a notification to be staged in the transactional outbox.</summary>
public sealed record OutboxMessageDto(
    string Type,
    string ToEmail,
    string Template,
    object Payload);
