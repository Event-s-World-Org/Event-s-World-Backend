using System.Net;
using System.Text;
using System.Text.Json;
using EventsWorld.Shared.Kernel.Results;

namespace EventsWorld.Modules.Notifications.Application.Templates;

/// <summary>
/// Renders Spanish transactional email templates using the Salar Cristal design tokens (ADR-0008, design-system.md §2).
/// Includes responsive tables, high email client compatibility, and plain-text alternatives.
/// </summary>
internal sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    public Result<RenderedEmail> Render(string templateName, object model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentNullException.ThrowIfNull(model);

        var data = NormalizeModel(model);

        return templateName.Trim().ToLowerInvariant() switch
        {
            "registration_received" or "registro_recibido" => RenderRegistrationReceived(data),
            "payment_approved" or "pago_aprobado" => RenderPaymentApproved(data),
            "payment_observed" or "pago_observado" => RenderPaymentObserved(data),
            "payment_rejected" or "pago_rechazado" => RenderPaymentRejected(data),
            _ => Result.Failure<RenderedEmail>(
                Error.NotFound("notifications.template_not_found", $"Email template '{templateName}' is not defined."))
        };
    }

    private static Result<RenderedEmail> RenderRegistrationReceived(IReadOnlyDictionary<string, string> data)
    {
        var participantName = data.GetValueOrDefault("participant_name")
            ?? data.GetValueOrDefault("participantname")
            ?? data.GetValueOrDefault("name")
            ?? "Participante";
        var eventTitle = data.GetValueOrDefault("event_title")
            ?? data.GetValueOrDefault("eventtitle")
            ?? "el evento";
        var accessLink = data.GetValueOrDefault("access_link")
            ?? data.GetValueOrDefault("accesslink")
            ?? data.GetValueOrDefault("link")
            ?? "#";

        var subject = $"Tu registro para {eventTitle} ha sido recibido";

        var contentHtml = $"""
            <h2 style="margin: 0 0 16px; font-size: 22px; line-height: 1.3; color: #0E1B2A; font-weight: 700;">¡Hola, {WebUtility.HtmlEncode(participantName)}!</h2>
            <p style="margin: 0 0 16px; font-size: 15px; line-height: 1.6; color: #3E5367;">
                Hemos recibido tu solicitud de registro para <strong>{WebUtility.HtmlEncode(eventTitle)}</strong>.
            </p>
            <p style="margin: 0 0 24px; font-size: 15px; line-height: 1.6; color: #3E5367;">
                Puedes acceder a los detalles de tu registro y consultar el estado de tu participación en cualquier momento a través de tu enlace personal:
            </p>
            <div style="text-align: center; margin: 32px 0;">
                <a href="{WebUtility.HtmlEncode(accessLink)}" style="display: inline-block; background-color: #0B63B6; color: #FFFFFF; text-decoration: none; font-size: 15px; font-weight: 600; padding: 14px 28px; border-radius: 12px; box-shadow: 0 4px 12px rgba(11, 99, 182, 0.25);">
                    Ver mi registro
                </a>
            </div>
            <p style="margin: 24px 0 0; font-size: 13px; line-height: 1.5; color: #5A6B80;">
                Si el botón no funciona, copia y pega el siguiente enlace en tu navegador:<br />
                <a href="{WebUtility.HtmlEncode(accessLink)}" style="color: #0B63B6; word-break: break-all;">{WebUtility.HtmlEncode(accessLink)}</a>
            </p>
            """;

        var contentText = $"""
            ¡Hola, {participantName}!

            Hemos recibido tu solicitud de registro para: {eventTitle}.

            Puedes consultar los detalles y estado de tu registro en el siguiente enlace personal:
            {accessLink}

            Si no solicitaste este registro, puedes ignorar este correo.
            """;

        return Result<RenderedEmail>.Success(new RenderedEmail(
            subject,
            WrapWithSalarCristalLayout(subject, contentHtml),
            WrapWithPlainTextLayout(subject, contentText)));
    }

    private static Result<RenderedEmail> RenderPaymentApproved(IReadOnlyDictionary<string, string> data)
    {
        var participantName = data.GetValueOrDefault("participant_name")
            ?? data.GetValueOrDefault("participantname")
            ?? data.GetValueOrDefault("name")
            ?? "Participante";
        var eventTitle = data.GetValueOrDefault("event_title")
            ?? data.GetValueOrDefault("eventtitle")
            ?? "el evento";
        var accessLink = data.GetValueOrDefault("access_link")
            ?? data.GetValueOrDefault("accesslink")
            ?? data.GetValueOrDefault("link")
            ?? "#";
        var registrationCode = data.GetValueOrDefault("registration_code")
            ?? data.GetValueOrDefault("registrationcode")
            ?? string.Empty;

        var subject = $"¡Pago aprobado! Tu entrada para {eventTitle} está confirmada";

        var codeBlock = string.IsNullOrWhiteSpace(registrationCode)
            ? string.Empty
            : $"""
              <div style="background-color: #DCFCE7; border: 1px solid #86EFAC; border-radius: 12px; padding: 16px; margin: 20px 0; text-align: center;">
                  <span style="font-size: 12px; font-weight: 600; text-transform: uppercase; color: #166534; display: block; margin-bottom: 4px;">Código de Registro</span>
                  <span style="font-size: 20px; font-weight: 700; color: #166534; letter-spacing: 2px;">{WebUtility.HtmlEncode(registrationCode)}</span>
              </div>
              """;

        var contentHtml = $"""
            <h2 style="margin: 0 0 16px; font-size: 22px; line-height: 1.3; color: #0E1B2A; font-weight: 700;">¡Excelentes noticias, {WebUtility.HtmlEncode(participantName)}!</h2>
            <p style="margin: 0 0 16px; font-size: 15px; line-height: 1.6; color: #3E5367;">
                Tu comprobante de pago para <strong>{WebUtility.HtmlEncode(eventTitle)}</strong> ha sido revisado y <strong>aprobado</strong> exitosamente.
            </p>
            {codeBlock}
            <p style="margin: 0 0 24px; font-size: 15px; line-height: 1.6; color: #3E5367;">
                Tu lugar está garantizado. Puedes ver tu entrada digital y credencial QR de acceso directo en el siguiente enlace:
            </p>
            <div style="text-align: center; margin: 32px 0;">
                <a href="{WebUtility.HtmlEncode(accessLink)}" style="display: inline-block; background-color: #0B63B6; color: #FFFFFF; text-decoration: none; font-size: 15px; font-weight: 600; padding: 14px 28px; border-radius: 12px; box-shadow: 0 4px 12px rgba(11, 99, 182, 0.25);">
                    Ver mi entrada y credencial QR
                </a>
            </div>
            <p style="margin: 24px 0 0; font-size: 13px; line-height: 1.5; color: #5A6B80;">
                Enlace directo:<br />
                <a href="{WebUtility.HtmlEncode(accessLink)}" style="color: #0B63B6; word-break: break-all;">{WebUtility.HtmlEncode(accessLink)}</a>
            </p>
            """;

        var contentText = $"""
            ¡Excelentes noticias, {participantName}!

            Tu comprobante de pago para {eventTitle} ha sido APROBADO exitosamente.
            {(string.IsNullOrWhiteSpace(registrationCode) ? string.Empty : $"Código de registro: {registrationCode}\n")}
            Tu entrada digital y credencial QR de acceso están disponibles en:
            {accessLink}
            """;

        return Result<RenderedEmail>.Success(new RenderedEmail(
            subject,
            WrapWithSalarCristalLayout(subject, contentHtml),
            WrapWithPlainTextLayout(subject, contentText)));
    }

    private static Result<RenderedEmail> RenderPaymentObserved(IReadOnlyDictionary<string, string> data)
    {
        var participantName = data.GetValueOrDefault("participant_name")
            ?? data.GetValueOrDefault("participantname")
            ?? "Participante";
        var eventTitle = data.GetValueOrDefault("event_title")
            ?? data.GetValueOrDefault("eventtitle")
            ?? "el evento";
        var observationReason = data.GetValueOrDefault("reason")
            ?? data.GetValueOrDefault("observation_reason")
            ?? "El comprobante no es legible o los datos no coinciden.";
        var uploadLink = data.GetValueOrDefault("upload_link")
            ?? data.GetValueOrDefault("uploadlink")
            ?? data.GetValueOrDefault("link")
            ?? "#";
        var deadline = data.GetValueOrDefault("deadline") ?? string.Empty;

        var subject = $"Observación en tu comprobante para {eventTitle}";

        var deadlineText = string.IsNullOrWhiteSpace(deadline)
            ? string.Empty
            : $"<p style=\"margin: 0 0 16px; font-size: 14px; color: #8A3B0A;\"><strong>Fecha límite para responder:</strong> {WebUtility.HtmlEncode(deadline)}</p>";

        var contentHtml = $"""
            <h2 style="margin: 0 0 16px; font-size: 22px; line-height: 1.3; color: #0E1B2A; font-weight: 700;">Hola, {WebUtility.HtmlEncode(participantName)}</h2>
            <p style="margin: 0 0 16px; font-size: 15px; line-height: 1.6; color: #3E5367;">
                El equipo organizador revisó tu comprobante de pago para <strong>{WebUtility.HtmlEncode(eventTitle)}</strong> y tiene una observación:
            </p>
            <div style="background-color: #FEF3C7; border-left: 4px solid #8A3B0A; padding: 14px 16px; border-radius: 8px; margin: 20px 0;">
                <p style="margin: 0; font-size: 14px; line-height: 1.5; color: #8A3B0A;">
                    <strong>Motivo:</strong> {WebUtility.HtmlEncode(observationReason)}
                </p>
            </div>
            {deadlineText}
            <p style="margin: 0 0 24px; font-size: 15px; line-height: 1.6; color: #3E5367;">
                Por favor, sube una foto o PDF claro de tu comprobante para confirmar tu inscripción:
            </p>
            <div style="text-align: center; margin: 32px 0;">
                <a href="{WebUtility.HtmlEncode(uploadLink)}" style="display: inline-block; background-color: #0B63B6; color: #FFFFFF; text-decoration: none; font-size: 15px; font-weight: 600; padding: 14px 28px; border-radius: 12px; box-shadow: 0 4px 12px rgba(11, 99, 182, 0.25);">
                    Subir nuevo comprobante
                </a>
            </div>
            """;

        var contentText = $"""
            Hola, {participantName}

            Tu comprobante para {eventTitle} tiene una observación:
            Motivo: {observationReason}
            {(string.IsNullOrWhiteSpace(deadline) ? string.Empty : $"Fecha límite: {deadline}\n")}
            Por favor sube un nuevo comprobante en el siguiente enlace:
            {uploadLink}
            """;

        return Result<RenderedEmail>.Success(new RenderedEmail(
            subject,
            WrapWithSalarCristalLayout(subject, contentHtml),
            WrapWithPlainTextLayout(subject, contentText)));
    }

    private static Result<RenderedEmail> RenderPaymentRejected(IReadOnlyDictionary<string, string> data)
    {
        var participantName = data.GetValueOrDefault("participant_name")
            ?? data.GetValueOrDefault("participantname")
            ?? "Participante";
        var eventTitle = data.GetValueOrDefault("event_title")
            ?? data.GetValueOrDefault("eventtitle")
            ?? "el evento";
        var reason = data.GetValueOrDefault("reason")
            ?? "El plazo para subsanar observaciones ha vencido.";
        var contactLink = data.GetValueOrDefault("contact_link")
            ?? data.GetValueOrDefault("link")
            ?? "#";

        var subject = $"Registro no completado para {eventTitle}";

        var contentHtml = $"""
            <h2 style="margin: 0 0 16px; font-size: 22px; line-height: 1.3; color: #0E1B2A; font-weight: 700;">Hola, {WebUtility.HtmlEncode(participantName)}</h2>
            <p style="margin: 0 0 16px; font-size: 15px; line-height: 1.6; color: #3E5367;">
                Te informamos que tu registro para <strong>{WebUtility.HtmlEncode(eventTitle)}</strong> no pudo ser completado.
            </p>
            <div style="background-color: #FEE2E2; border-left: 4px solid #991B1B; padding: 14px 16px; border-radius: 8px; margin: 20px 0;">
                <p style="margin: 0; font-size: 14px; line-height: 1.5; color: #991B1B;">
                    <strong>Detalle:</strong> {WebUtility.HtmlEncode(reason)}
                </p>
            </div>
            <p style="margin: 0 0 24px; font-size: 15px; line-height: 1.6; color: #3E5367;">
                Si consideras que esto es un error o necesitas ayuda, puedes comunicarte con el equipo organizador:
            </p>
            <div style="text-align: center; margin: 32px 0;">
                <a href="{WebUtility.HtmlEncode(contactLink)}" style="display: inline-block; background-color: #0B63B6; color: #FFFFFF; text-decoration: none; font-size: 15px; font-weight: 600; padding: 14px 28px; border-radius: 12px;">
                    Contactar a soporte
                </a>
            </div>
            """;

        var contentText = $"""
            Hola, {participantName}

            Te informamos que tu registro para {eventTitle} no pudo ser completado.
            Motivo: {reason}

            Para más información o consultas, visita:
            {contactLink}
            """;

        return Result<RenderedEmail>.Success(new RenderedEmail(
            subject,
            WrapWithSalarCristalLayout(subject, contentHtml),
            WrapWithPlainTextLayout(subject, contentText)));
    }

    private static string WrapWithSalarCristalLayout(string title, string bodyContent)
    {
        return $$"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <title>{{WebUtility.HtmlEncode(title)}}</title>
                <style>
                    body, table, td, a { -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }
                    table, td { mso-table-lspace: 0pt; mso-table-rspace: 0pt; }
                    img { -ms-interpolation-mode: bicubic; border: 0; height: auto; line-height: 100%; outline: none; text-decoration: none; }
                </style>
            </head>
            <body style="margin: 0; padding: 0; background-color: #E6F0FB; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; color: #0E1B2A;">
                <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%" style="background-color: #E6F0FB; padding: 32px 12px;">
                    <tr>
                        <td align="center">
                            <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width: 580px; background-color: #FFFFFF; border-radius: 18px; border: 1px solid #D3DEE9; box-shadow: 0 10px 30px rgba(14, 27, 42, 0.08); overflow: hidden;">
                                <!-- Header: Salar Cristal brand -->
                                <tr>
                                    <td style="padding: 28px 32px 20px; background: linear-gradient(135deg, #0B63B6 0%, #09528F 100%); text-align: left;">
                                        <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%">
                                            <tr>
                                                <td>
                                                    <span style="font-size: 20px; font-weight: 700; color: #FFFFFF; letter-spacing: 0.5px; text-decoration: none;">Event's World</span>
                                                </td>
                                                <td align="right">
                                                    <span style="display: inline-block; width: 10px; height: 10px; border-radius: 50%; background-color: #FF8A73;"></span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <!-- Body Content -->
                                <tr>
                                    <td style="padding: 36px 32px;">
                                        {{bodyContent}}
                                    </td>
                                </tr>
                                <!-- Footer -->
                                <tr>
                                    <td style="padding: 24px 32px; background-color: #EBF1F7; border-top: 1px solid #D3DEE9; text-align: center;">
                                        <p style="margin: 0 0 8px; font-size: 12px; color: #5A6B80; line-height: 1.5;">
                                            Este es un correo automático enviado por Event's World. Por favor, no respondas directamente a este mensaje.
                                        </p>
                                        <p style="margin: 0; font-size: 11px; color: #7F95B0;">
                                            &copy; {{DateTime.UtcNow.Year}} Event's World. Todos los derechos reservados.
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;
    }

    private static string WrapWithPlainTextLayout(string title, string bodyContent)
    {
        return $"""
            ===================================================================
            EVENT'S WORLD
            ===================================================================
            {title}
            -------------------------------------------------------------------

            {bodyContent.Trim()}

            -------------------------------------------------------------------
            Este es un correo automático de Event's World.
            (c) {DateTime.UtcNow.Year} Event's World.
            """;
    }

    private static Dictionary<string, string> NormalizeModel(object model)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        switch (model)
        {
            case IReadOnlyDictionary<string, string> stringDict:
                foreach (var (k, v) in stringDict)
                {
                    dict[k] = v;
                }
                break;

            case IReadOnlyDictionary<string, object> objDict:
                foreach (var (k, v) in objDict)
                {
                    dict[k] = v?.ToString() ?? string.Empty;
                }
                break;

            case JsonDocument jsonDoc:
                ExtractFromJsonElement(jsonDoc.RootElement, dict);
                break;

            case JsonElement jsonElement:
                ExtractFromJsonElement(jsonElement, dict);
                break;

            case string rawJson when rawJson.TrimStart().StartsWith('{'):
                try
                {
                    using var doc = JsonDocument.Parse(rawJson);
                    ExtractFromJsonElement(doc.RootElement, dict);
                }
                catch
                {
                    dict["raw"] = rawJson;
                }
                break;

            default:
                var properties = model.GetType().GetProperties();
                foreach (var prop in properties)
                {
                    var val = prop.GetValue(model);
                    dict[prop.Name] = val?.ToString() ?? string.Empty;
                }
                break;
        }

        return dict;
    }

    private static void ExtractFromJsonElement(JsonElement element, Dictionary<string, string> dict)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                _ => property.Value.GetRawText()
            };
        }
    }
}
