using EventsWorld.Modules.Notifications.Application.Templates;
using EventsWorld.Modules.Notifications.Contracts;

namespace EventsWorld.UnitTests;

public sealed class EmailTemplateRendererTests
{
    private readonly EmailTemplateRenderer _renderer = new(new FixedTimeProvider(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void Render_RegistrationReceived_IncludesCodeAndPersonalLink_WithSalarCristalLightTokens()
    {
        var email = new RegistrationReceivedEmail(
            "participante@example.com",
            "María Gómez",
            "Congreso Salar Tech 2026",
            "EW-7F3K2",
            "https://eventsworld.app/p/tok_123");

        var result = _renderer.Render(email.Template, email);

        Assert.True(result.IsSuccess);
        Assert.Contains("inscripción", result.Value.Subject);
        Assert.DoesNotContain("registro", result.Value.Subject, StringComparison.OrdinalIgnoreCase);

        // Required content per FR-07.1
        Assert.Contains("María Gómez", result.Value.HtmlBody);
        Assert.Contains("Congreso Salar Tech 2026", result.Value.HtmlBody);
        Assert.Contains("EW-7F3K2", result.Value.HtmlBody);
        Assert.Contains("https://eventsworld.app/p/tok_123", result.Value.HtmlBody);

        // Salar Cristal light-mode tokens & Outlook fallback (review point 9)
        Assert.Contains("bgcolor=\"#0B63B6\"", result.Value.HtmlBody);
        Assert.Contains("background-color: #0B63B6", result.Value.HtmlBody);
        Assert.Contains("#E6F0FB", result.Value.HtmlBody);
        Assert.Contains("#3E5367", result.Value.HtmlBody);
        Assert.DoesNotContain("#FF8A73", result.Value.HtmlBody);
        Assert.DoesNotContain("#7F95B0", result.Value.HtmlBody);
        Assert.Contains("2026", result.Value.HtmlBody);

        // Plain-text alternative
        Assert.Contains("María Gómez", result.Value.TextBody);
        Assert.Contains("EW-7F3K2", result.Value.TextBody);
        Assert.Contains("https://eventsworld.app/p/tok_123", result.Value.TextBody);
    }

    [Fact]
    public void Render_PaymentApproved_IncludesCodeAndPersonalLink_WithoutMentioningEntryQr()
    {
        var email = new PaymentApprovedEmail(
            "ana@example.com",
            "Ana Torres",
            "Taller de Fotografía",
            "EW-4K9M2",
            "https://eventsworld.app/p/tok_456");

        var result = _renderer.Render(email.Template, email);

        Assert.True(result.IsSuccess);
        Assert.Contains("EW-4K9M2", result.Value.HtmlBody);
        Assert.Contains("https://eventsworld.app/p/tok_456", result.Value.HtmlBody);
        Assert.Contains("#DCFCE7", result.Value.HtmlBody);
        Assert.Contains("#166534", result.Value.HtmlBody);
        Assert.DoesNotContain("credencial QR", result.Value.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credencial QR", result.Value.TextBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_PaymentObserved_UsesObservationPalette_AndIncludesPersonalLink()
    {
        var email = new PaymentObservedEmail(
            "carlos@example.com",
            "Carlos Zurita",
            "Simposio IA",
            "EW-8P2N5",
            "La imagen está borrosa y no se distingue el número de transacción.",
            "https://eventsworld.app/p/tok_789",
            "20 de noviembre, 18:00");

        var result = _renderer.Render(email.Template, email);

        Assert.True(result.IsSuccess);
        Assert.Contains("La imagen está borrosa", result.Value.HtmlBody);
        Assert.Contains("20 de noviembre, 18:00", result.Value.HtmlBody);
        Assert.Contains("https://eventsworld.app/p/tok_789", result.Value.HtmlBody);

        // Under review (En observación) palette from design-system.md §2.2: #5B21B6 on #EDE9FE
        Assert.Contains("#EDE9FE", result.Value.HtmlBody);
        Assert.Contains("#5B21B6", result.Value.HtmlBody);
        Assert.DoesNotContain("#FEF3C7", result.Value.HtmlBody);
    }

    [Fact]
    public void Render_PaymentRejected_IncludesPersonalLink_AndNotSupportButton()
    {
        var email = new PaymentRejectedEmail(
            "lucia@example.com",
            "Lucía Méndez",
            "Encuentro Nacional",
            "EW-3R6T9",
            "Comprobante duplicado.",
            "https://eventsworld.app/p/tok_321");

        var result = _renderer.Render(email.Template, email);

        Assert.True(result.IsSuccess);
        Assert.Contains("EW-3R6T9", result.Value.HtmlBody);
        Assert.Contains("Comprobante duplicado.", result.Value.HtmlBody);
        Assert.Contains("https://eventsworld.app/p/tok_321", result.Value.HtmlBody);
        Assert.Contains("Ver mi inscripción", result.Value.HtmlBody);
        Assert.DoesNotContain("Contactar a soporte", result.Value.HtmlBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_AllRemainingFr071Templates_RenderSuccessfully()
    {
        var rejectedFinal = new RegistrationRejectedFinalEmail(
            "p@example.com", "Pedro", "Evento A", "EW-22222", "Plazo vencido", "https://eventsworld.app/p/1");
        var dataEdited = new ParticipantDataEditedEmail(
            "p@example.com", "Pedro", "Evento A", "EW-22222", "Corrección de apellido", "Nombre: Pedro Pérez", "https://eventsworld.app/p/1");
        var qrExpired = new PaymentQrExpiredEmail(
            "org@example.com", "Organizador", "Evento A", "2026-11-15", "https://eventsworld.app/events/1/payment-settings");
        var suspended = new EventSuspendedEmail(
            "org@example.com", "Organizador", "Evento A", "Denuncia verificada", "https://eventsworld.app/events/1");
        var reactivated = new EventReactivatedEmail(
            "org@example.com", "Organizador", "Evento A", "https://eventsworld.app/events/1");
        var linkRecovery = new PersonalLinkRecoveryEmail(
            "p@example.com",
            "Pedro",
            [new RecoveredRegistrationItem("Evento A", "EW-22222", "https://eventsworld.app/p/1")]);

        Assert.True(_renderer.Render(rejectedFinal.Template, rejectedFinal).IsSuccess);
        Assert.True(_renderer.Render(dataEdited.Template, dataEdited).IsSuccess);
        Assert.True(_renderer.Render(qrExpired.Template, qrExpired).IsSuccess);
        Assert.True(_renderer.Render(suspended.Template, suspended).IsSuccess);
        Assert.True(_renderer.Render(reactivated.Template, reactivated).IsSuccess);
        Assert.True(_renderer.Render(linkRecovery.Template, linkRecovery).IsSuccess);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"participant_name\":\"Ana\",\"event_title\":\"Evt\",\"registration_code\":\"EW-23456\",\"personal_link\":\"#\"}")]
    [InlineData("{\"participant_name\":\"Ana\",\"event_title\":\"Evt\",\"personal_link\":\"https://eventsworld.app/p/1\"}")]
    public void Render_MissingOrInvalidRequiredFields_ReturnsValidationFailure(string invalidPayloadJson)
    {
        var result = _renderer.Render(EmailTemplates.RegistrationReceived, invalidPayloadJson);

        Assert.True(result.IsFailure);
        Assert.Equal("notifications.missing_template_data", result.Error.Code);
    }

    [Fact]
    public void Render_EscapesHtmlSpecialCharacters_PreventingInjection()
    {
        var email = new RegistrationReceivedEmail(
            "xss@example.com",
            "<script>alert('xss')</script>",
            "Evento & Más",
            "EW-7F3K2",
            "https://eventsworld.app/p/safe");

        var result = _renderer.Render(email.Template, email);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("<script>", result.Value.HtmlBody);
        Assert.Contains("&lt;script&gt;", result.Value.HtmlBody);
        Assert.Contains("Evento &amp; Más", result.Value.HtmlBody);
    }

    [Fact]
    public void Render_UnknownTemplate_ReturnsNotFoundFailure()
    {
        var result = _renderer.Render("non_existent_template", "{}");

        Assert.True(result.IsFailure);
        Assert.Equal("notifications.template_not_found", result.Error.Code);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
