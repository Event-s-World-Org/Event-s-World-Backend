namespace EventsWorld.Modules.Notifications.Infrastructure.Email;

/// <summary>Configuration options for the Resend email integration (ADR-0005).</summary>
internal sealed class ResendOptions
{
    public const string Section = "Resend";

    /// <summary>Resend API Key (starts with <c>re_</c>). Stored as a secret in production.</summary>
    public string? ApiKey { get; set; }

    /// <summary>From email address (must belong to a verified domain in Resend).</summary>
    public string SenderEmail { get; set; } = "notificaciones@eventsworld.app";

    /// <summary>From display name.</summary>
    public string SenderName { get; set; } = "Event's World";

    /// <summary>Provider mode: "Resend" or "Development".</summary>
    public string Provider { get; set; } = "Resend";
}
