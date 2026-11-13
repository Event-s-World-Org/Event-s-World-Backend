using System.Net;
using EventsWorld.Modules.Notifications.Application.Templates;

namespace EventsWorld.UnitTests;

public sealed class EmailTemplateRendererTests
{
    private readonly EmailTemplateRenderer _renderer = new();

    [Fact]
    public void Render_RegistrationReceived_GeneratesSalarCristalHtmlAndPlainText()
    {
        var model = new Dictionary<string, string>
        {
            ["participant_name"] = "María Pérez",
            ["event_title"] = "Tech Summit 2026",
            ["access_link"] = "https://eventsworld.app/participant/token123"
        };

        var result = _renderer.Render("registration_received", model);

        Assert.True(result.IsSuccess);
        var email = result.Value;

        Assert.Equal("Tu registro para Tech Summit 2026 ha sido recibido", email.Subject);

        // Salar Cristal Design Tokens (ADR-0008, design-system.md §2)
        Assert.Contains("#0B63B6", email.HtmlBody); // Primary blue
        Assert.Contains("#E6F0FB", email.HtmlBody); // Sky backdrop
        Assert.Contains("#0E1B2A", email.HtmlBody); // Primary text
        Assert.Contains("border-radius: 12px", email.HtmlBody); // Soft button corner
        Assert.Contains(WebUtility.HtmlEncode("María Pérez"), email.HtmlBody);
        Assert.Contains("https://eventsworld.app/participant/token123", email.HtmlBody);

        // Plain-text alternative
        Assert.DoesNotContain("<html", email.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<table", email.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tech Summit 2026", email.TextBody);
        Assert.Contains("https://eventsworld.app/participant/token123", email.TextBody);
    }

    [Fact]
    public void Render_PaymentApproved_IncludesRegistrationCodeAndQrLink()
    {
        var model = new Dictionary<string, string>
        {
            ["participant_name"] = "Carlos Rojas",
            ["event_title"] = "Congreso de IA",
            ["registration_code"] = "EW-8K2MN",
            ["access_link"] = "https://eventsworld.app/participant/token-qr"
        };

        var result = _renderer.Render("pago_aprobado", model);

        Assert.True(result.IsSuccess);
        var email = result.Value;

        Assert.Contains("¡Pago aprobado!", email.Subject);
        Assert.Contains("EW-8K2MN", email.HtmlBody);
        Assert.Contains("#166534", email.HtmlBody); // Approved green status text
        Assert.Contains("#DCFCE7", email.HtmlBody); // Approved green background
        Assert.Contains("EW-8K2MN", email.TextBody);
        Assert.Contains("https://eventsworld.app/participant/token-qr", email.TextBody);
    }

    [Fact]
    public void Render_PaymentObserved_IncludesReasonAndUploadLink()
    {
        var model = new Dictionary<string, string>
        {
            ["participant_name"] = "Ana Gómez",
            ["event_title"] = "Seminario de Diseño",
            ["reason"] = "El comprobante no muestra el número de transacción.",
            ["upload_link"] = "https://eventsworld.app/participant/upload-link",
            ["deadline"] = "24 horas"
        };

        var result = _renderer.Render("payment_observed", model);

        Assert.True(result.IsSuccess);
        var email = result.Value;

        Assert.Contains("Observación en tu comprobante", email.Subject);
        Assert.Contains(WebUtility.HtmlEncode("El comprobante no muestra el número de transacción."), email.HtmlBody);
        Assert.Contains("#8A3B0A", email.HtmlBody); // Observed warning color
        Assert.Contains("24 horas", email.HtmlBody);
        Assert.Contains("https://eventsworld.app/participant/upload-link", email.TextBody);
    }

    [Fact]
    public void Render_PaymentRejected_IncludesRejectionReason()
    {
        var model = new Dictionary<string, string>
        {
            ["participant_name"] = "Luis Morales",
            ["event_title"] = "Hackathon 2026",
            ["reason"] = "Plazo de subsanación vencido.",
            ["link"] = "https://eventsworld.app/support"
        };

        var result = _renderer.Render("payment_rejected", model);

        Assert.True(result.IsSuccess);
        var email = result.Value;

        Assert.Contains("Registro no completado", email.Subject);
        Assert.Contains(WebUtility.HtmlEncode("Plazo de subsanación vencido."), email.HtmlBody);
        Assert.Contains("#991B1B", email.HtmlBody); // Rejected red text
        Assert.Contains("#FEE2E2", email.HtmlBody); // Rejected red bg
        Assert.Contains("https://eventsworld.app/support", email.TextBody);
    }

    [Fact]
    public void Render_UnknownTemplate_ReturnsFailureResult()
    {
        var result = _renderer.Render("unknown_non_existent_template", new { });

        Assert.True(result.IsFailure);
        Assert.Equal("notifications.template_not_found", result.Error.Code);
    }
}
