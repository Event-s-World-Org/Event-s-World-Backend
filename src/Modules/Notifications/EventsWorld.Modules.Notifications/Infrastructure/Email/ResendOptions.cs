namespace EventsWorld.Modules.Notifications.Infrastructure.Email;

/// <summary>Configuration options for email delivery (ADR-0005).</summary>
internal sealed class ResendOptions
{
    public const string Section = "Resend";

    /// <summary>Resend API Key (starts with <c>re_</c>). Required when <see cref="Provider"/> is <c>Resend</c>.</summary>
    public string? ApiKey { get; set; }

    /// <summary>From email address (must belong to a verified domain in Resend).</summary>
    public string SenderEmail { get; set; } = "notificaciones@eventsworld.app";

    /// <summary>From display name.</summary>
    public string SenderName { get; set; } = "Event's World";

    /// <summary>Delivery provider: <c>Resend</c> (production default), <c>Smtp</c> (Mailpit in development), or <c>Development</c> (in-memory/log).</summary>
    public string Provider { get; set; } = "Resend";

    /// <summary>SMTP host for local Mailpit in development.</summary>
    public string SmtpHost { get; set; } = "localhost";

    /// <summary>SMTP port for local Mailpit in development (default 1025).</summary>
    public int SmtpPort { get; set; } = 1025;
}
