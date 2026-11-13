using EventsWorld.Shared.Kernel.Results;

namespace EventsWorld.Modules.Notifications.Application.Templates;

/// <summary>
/// Renders Spanish email templates with a Salar Cristal layout and plain-text alternatives (ADR-0008).
/// </summary>
internal interface IEmailTemplateRenderer
{
    Result<RenderedEmail> Render(string templateName, object model);
}
