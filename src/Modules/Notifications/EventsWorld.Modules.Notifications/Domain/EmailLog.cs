namespace EventsWorld.Modules.Notifications.Domain;

/// <summary>
/// Domain entity logging an email delivery attempt and provider tracking receipt (database.md §3.6).
/// </summary>
internal sealed class EmailLog
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid? OutboxId { get; private set; }
    public string ToEmail { get; private set; } = string.Empty;
    public string Template { get; private set; } = string.Empty;
    public string ProviderMessageId { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; } = DateTimeOffset.UtcNow;

    private EmailLog()
    {
    }

    public static EmailLog Create(
        Guid? outboxId,
        string toEmail,
        string template,
        string providerMessageId,
        string status,
        DateTimeOffset sentAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerMessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        return new EmailLog
        {
            Id = Guid.CreateVersion7(),
            OutboxId = outboxId,
            ToEmail = toEmail,
            Template = template,
            ProviderMessageId = providerMessageId,
            Status = status,
            SentAt = sentAt
        };
    }
}
