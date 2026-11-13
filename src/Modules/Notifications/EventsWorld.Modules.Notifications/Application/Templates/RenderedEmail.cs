namespace EventsWorld.Modules.Notifications.Application.Templates;

/// <summary>Rendered email with both HTML and plain-text alternatives (NFR-01.1, database.md §3.6).</summary>
internal sealed record RenderedEmail(string Subject, string HtmlBody, string TextBody);
