namespace EventsWorld.Modules.Notifications.Domain;

/// <summary>
/// Domain entity logging an email delivery outcome and provider tracking receipt (database.md §3.6).
/// </summary>
internal sealed class EmailLog
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid? OutboxId { get; private set; }
    public string ToEmail { get; private set; } = string.Empty;
    public string Template { get; private set; } = string.Empty;
    public string? ProviderMessageId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; }

    private EmailLog()
    {
    }

    public static EmailLog CreateSent(
        Guid? outboxId,
        string toEmail,
        string template,
        string providerMessageId,
        DateTimeOffset sentAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerMessageId);

        return new EmailLog
        {
            Id = Guid.CreateVersion7(),
            OutboxId = outboxId,
            ToEmail = toEmail,
            Template = template,
            ProviderMessageId = providerMessageId,
            Status = "sent",
            SentAt = sentAt
        };
    }

    public static EmailLog CreateFailed(
        Guid? outboxId,
        string toEmail,
        string template,
        DateTimeOffset occurredAt)
    {
        var safeEmail = string.IsNullOrWhiteSpace(toEmail) ? "unknown@invalid.local" : toEmail;
        var safeTemplate = string.IsNullOrWhiteSpace(template) ? "unknown" : template;

        return new EmailLog
        {
            Id = Guid.CreateVersion7(),
            OutboxId = outboxId,
            ToEmail = safeEmail,
            Template = safeTemplate,
            ProviderMessageId = null,
            Status = "failed",
            SentAt = occurredAt
        };
    }
}
