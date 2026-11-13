namespace EventsWorld.Modules.Notifications.Application.Emails;

/// <summary>Represents an outgoing email message.</summary>
internal sealed record EmailMessage(
    string IdempotencyKey,
    string To,
    string Subject,
    string HtmlBody,
    string TextBody);

/// <summary>Receipt returned by the email provider upon acceptance.</summary>
internal sealed record EmailSendReceipt(string ProviderMessageId);

