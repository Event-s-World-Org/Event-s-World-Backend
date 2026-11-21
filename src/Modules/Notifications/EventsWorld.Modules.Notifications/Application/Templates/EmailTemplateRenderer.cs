using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text.Json;
using EventsWorld.Modules.Notifications.Contracts;
using EventsWorld.Shared.Kernel.Results;
using Scriban;
using Scriban.Runtime;

namespace EventsWorld.Modules.Notifications.Application.Templates;

/// <summary>
/// Renders Spanish transactional email templates using Scriban embedded templates and a shared
/// Salar Cristal layout (ADR-0008, design-system.md §2, FR-07.1).
/// Validates required fields for every template and produces both HTML and plain-text bodies.
/// </summary>
internal sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    private const string ResourcePrefix = "EventsWorld.Modules.Notifications.Application.Templates.Resources.";

    private static readonly Assembly ResourceAssembly = typeof(EmailTemplateRenderer).Assembly;
    private static readonly ConcurrentDictionary<string, Template> CompiledTemplates = new(StringComparer.OrdinalIgnoreCase);

    private readonly TimeProvider _timeProvider;

    public EmailTemplateRenderer(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Result<RenderedEmail> Render(string templateName, object model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentNullException.ThrowIfNull(model);

        var canonicalTemplate = NormalizeTemplateName(templateName);
        if (canonicalTemplate is null)
        {
            return Result.Failure<RenderedEmail>(
                Error.NotFound("notifications.template_not_found", $"Email template '{templateName}' is not defined."));
        }

        var parseResult = ExtractModel(model);
        if (parseResult.IsFailure)
        {
            return Result.Failure<RenderedEmail>(parseResult.Error);
        }

        var parsed = parseResult.Value;
        var validationResult = ValidateAndBuildSubject(canonicalTemplate, parsed);
        if (validationResult.IsFailure)
        {
            return Result.Failure<RenderedEmail>(validationResult.Error);
        }

        var subject = validationResult.Value;
        var year = _timeProvider.GetUtcNow().Year;

        var htmlBodyTemplate = GetCompiledTemplate($"{canonicalTemplate}.html.scriban");
        var textBodyTemplate = GetCompiledTemplate($"{canonicalTemplate}.txt.scriban");
        var htmlLayoutTemplate = GetCompiledTemplate("_layout.html.scriban");
        var textLayoutTemplate = GetCompiledTemplate("_layout.txt.scriban");

        var htmlContent = RenderScriban(htmlBodyTemplate, BuildScriptObject(parsed, encodeHtml: true));
        var textContent = RenderScriban(textBodyTemplate, BuildScriptObject(parsed, encodeHtml: false));

        var htmlLayoutModel = new ScriptObject
        {
            ["subject"] = EscapeHtml(subject),
            ["content"] = htmlContent,
            ["year"] = year
        };

        var textLayoutModel = new ScriptObject
        {
            ["subject"] = subject,
            ["content"] = textContent.Trim(),
            ["year"] = year
        };

        var fullHtml = RenderScriban(htmlLayoutTemplate, htmlLayoutModel);
        var fullText = RenderScriban(textLayoutTemplate, textLayoutModel);

        return Result.Success(new RenderedEmail(subject, fullHtml, fullText));
    }

    private static string? NormalizeTemplateName(string templateName) =>
        templateName.Trim().ToLowerInvariant() switch
        {
            EmailTemplates.RegistrationReceived or "registro_recibido" or "inscripcion_recibida" => EmailTemplates.RegistrationReceived,
            EmailTemplates.PaymentApproved or "pago_aprobado" => EmailTemplates.PaymentApproved,
            EmailTemplates.PaymentObserved or "pago_observado" => EmailTemplates.PaymentObserved,
            EmailTemplates.PaymentRejected or "pago_rechazado" => EmailTemplates.PaymentRejected,
            EmailTemplates.RegistrationRejectedFinal or "inscripcion_rechazada_definitiva" => EmailTemplates.RegistrationRejectedFinal,
            EmailTemplates.ParticipantDataEdited or "datos_participante_editados" => EmailTemplates.ParticipantDataEdited,
            EmailTemplates.PaymentQrExpired or "qr_pago_vencido" => EmailTemplates.PaymentQrExpired,
            EmailTemplates.EventSuspended or "evento_suspendido" => EmailTemplates.EventSuspended,
            EmailTemplates.EventReactivated or "evento_reactivado" => EmailTemplates.EventReactivated,
            EmailTemplates.PersonalLinkRecovery or "recuperacion_enlace_personal" => EmailTemplates.PersonalLinkRecovery,
            _ => null
        };

    private static Result<string> ValidateAndBuildSubject(string template, ParsedModel model)
    {
        switch (template)
        {
            case EmailTemplates.RegistrationReceived:
                return RequireFields(model, ["participant_name", "event_title", "registration_code"], ["personal_link"])
                    .Map(() => $"Tu inscripción para {model.Strings["event_title"]} ha sido recibida");

            case EmailTemplates.PaymentApproved:
                return RequireFields(model, ["participant_name", "event_title", "registration_code"], ["personal_link"])
                    .Map(() => $"¡Pago aprobado! Tu inscripción para {model.Strings["event_title"]} está confirmada");

            case EmailTemplates.PaymentObserved:
                return RequireFields(model, ["participant_name", "event_title", "registration_code", "reason"], ["personal_link"])
                    .Map(() => $"Observación en tu comprobante para {model.Strings["event_title"]}");

            case EmailTemplates.PaymentRejected:
                return RequireFields(model, ["participant_name", "event_title", "registration_code", "reason"], ["personal_link"])
                    .Map(() => $"Comprobante rechazado en tu inscripción para {model.Strings["event_title"]}");

            case EmailTemplates.RegistrationRejectedFinal:
                return RequireFields(model, ["participant_name", "event_title", "registration_code", "reason"], ["personal_link"])
                    .Map(() => $"Inscripción rechazada definitivamente para {model.Strings["event_title"]}");

            case EmailTemplates.ParticipantDataEdited:
                return RequireFields(model, ["participant_name", "event_title", "registration_code", "reason", "summary"], ["personal_link"])
                    .Map(() => $"Actualización en los datos de tu inscripción para {model.Strings["event_title"]}");

            case EmailTemplates.PaymentQrExpired:
                return RequireFields(model, ["organizer_name", "event_title", "expired_date"], ["manage_event_link"])
                    .Map(() => $"Código QR vencido e inscripciones pausadas en {model.Strings["event_title"]}");

            case EmailTemplates.EventSuspended:
                return RequireFields(model, ["organizer_name", "event_title", "reason"], ["manage_event_link"])
                    .Map(() => $"Tu evento {model.Strings["event_title"]} fue suspendido");

            case EmailTemplates.EventReactivated:
                return RequireFields(model, ["organizer_name", "event_title"], ["manage_event_link"])
                    .Map(() => $"Tu evento {model.Strings["event_title"]} fue reactivado");

            case EmailTemplates.PersonalLinkRecovery:
                var nameCheck = RequireFields(model, ["participant_name"], []);
                if (nameCheck.IsFailure)
                {
                    return Result.Failure<string>(nameCheck.Error);
                }

                if (model.Registrations.Count == 0)
                {
                    return Result.Failure<string>(
                        Error.Validation("notifications.missing_template_data", "Template 'personal_link_recovery' requires at least one registration item."));
                }

                foreach (var item in model.Registrations)
                {
                    if (string.IsNullOrWhiteSpace(item.EventTitle) ||
                        string.IsNullOrWhiteSpace(item.RegistrationCode) ||
                        !IsValidLink(item.PersonalLink))
                    {
                        return Result.Failure<string>(
                            Error.Validation("notifications.missing_template_data", "Every registration item in 'personal_link_recovery' requires event_title, registration_code, and a valid personal_link."));
                    }
                }

                return Result.Success("Tus enlaces personales de inscripción en Event's World");

            default:
                return Result.Failure<string>(
                    Error.NotFound("notifications.template_not_found", $"Template '{template}' is not supported."));
        }
    }

    private static Result RequireFields(
        ParsedModel model,
        ReadOnlySpan<string> requiredTextKeys,
        ReadOnlySpan<string> requiredLinkKeys)
    {
        foreach (var key in requiredTextKeys)
        {
            if (!model.Strings.TryGetValue(key, out var val) || string.IsNullOrWhiteSpace(val))
            {
                return Result.Failure(
                    Error.Validation("notifications.missing_template_data", $"Missing required template field '{key}'."));
            }
        }

        foreach (var linkKey in requiredLinkKeys)
        {
            if (!model.Strings.TryGetValue(linkKey, out var link) || !IsValidLink(link))
            {
                return Result.Failure(
                    Error.Validation("notifications.missing_template_data", $"Missing or invalid required link field '{linkKey}'."));
            }
        }

        return Result.Success();
    }

    private static bool IsValidLink(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value.Trim(), "#", StringComparison.Ordinal) &&
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static ScriptObject BuildScriptObject(ParsedModel model, bool encodeHtml)
    {
        var scriptObject = new ScriptObject
        {
            ["deadline"] = string.Empty
        };

        foreach (var (key, value) in model.Strings)
        {
            scriptObject[key] = encodeHtml ? EscapeHtml(value) : value;
        }

        var items = new List<ScriptObject>(model.Registrations.Count);
        foreach (var reg in model.Registrations)
        {
            items.Add(new ScriptObject
            {
                ["event_title"] = encodeHtml ? EscapeHtml(reg.EventTitle) : reg.EventTitle,
                ["registration_code"] = encodeHtml ? EscapeHtml(reg.RegistrationCode) : reg.RegistrationCode,
                ["personal_link"] = encodeHtml ? EscapeHtml(reg.PersonalLink) : reg.PersonalLink
            });
        }

        scriptObject["registrations"] = items;
        return scriptObject;
    }

    private static string EscapeHtml(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);

    private static string RenderScriban(Template template, ScriptObject model)
    {
        var context = new TemplateContext { AutoIndent = false };
        context.PushGlobal(model);
        return template.Render(context);
    }

    private static Template GetCompiledTemplate(string fileName) =>
        CompiledTemplates.GetOrAdd(fileName, static name =>
        {
            var resourceName = ResourcePrefix + name;
            using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded Scriban template resource '{resourceName}' was not found.");
            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();
            var parsed = Template.Parse(source, resourceName);
            if (parsed.HasErrors)
            {
                throw new InvalidOperationException($"Failed to parse Scriban template '{resourceName}': {string.Join("; ", parsed.Messages)}");
            }

            return parsed;
        });

    private static Result<ParsedModel> ExtractModel(object model)
    {
        var strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var registrations = new List<RecoveredRegistrationItem>();

        switch (model)
        {
            case string rawJson:
                if (string.IsNullOrWhiteSpace(rawJson) || !rawJson.TrimStart().StartsWith('{'))
                {
                    return Result.Failure<ParsedModel>(
                        Error.Validation("notifications.invalid_payload", "Template payload must be a valid JSON object."));
                }

                try
                {
                    using var doc = JsonDocument.Parse(rawJson);
                    ExtractFromJsonElement(doc.RootElement, strings, registrations);
                }
                catch (JsonException ex)
                {
                    return Result.Failure<ParsedModel>(
                        Error.Validation("notifications.invalid_payload", $"Invalid JSON payload: {ex.Message}"));
                }
                break;

            case JsonDocument jsonDoc:
                ExtractFromJsonElement(jsonDoc.RootElement, strings, registrations);
                break;

            case JsonElement jsonElement:
                ExtractFromJsonElement(jsonElement, strings, registrations);
                break;

            case IReadOnlyDictionary<string, string> stringDict:
                foreach (var (k, v) in stringDict)
                {
                    SetNormalizedKey(strings, k, v ?? string.Empty);
                }
                break;

            default:
                var json = JsonSerializer.Serialize(model, model.GetType());
                using (var serializedDoc = JsonDocument.Parse(json))
                {
                    ExtractFromJsonElement(serializedDoc.RootElement, strings, registrations);
                }
                break;
        }

        return Result.Success(new ParsedModel(strings, registrations));
    }

    private static void ExtractFromJsonElement(
        JsonElement element,
        Dictionary<string, string> strings,
        List<RecoveredRegistrationItem> registrations)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, "registrations", StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var itemDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var itemProp in item.EnumerateObject())
                    {
                        if (itemProp.Value.ValueKind == JsonValueKind.String)
                        {
                            SetNormalizedKey(itemDict, itemProp.Name, itemProp.Value.GetString() ?? string.Empty);
                        }
                    }

                    registrations.Add(new RecoveredRegistrationItem(
                        itemDict.GetValueOrDefault("event_title") ?? string.Empty,
                        itemDict.GetValueOrDefault("registration_code") ?? string.Empty,
                        itemDict.GetValueOrDefault("personal_link") ?? string.Empty));
                }

                continue;
            }

            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                _ => property.Value.GetRawText()
            };

            SetNormalizedKey(strings, property.Name, value);
        }
    }

    private static void SetNormalizedKey(Dictionary<string, string> dict, string key, string value)
    {
        var normalized = key.Trim().ToLowerInvariant() switch
        {
            "participantname" or "participant_name" => "participant_name",
            "organizername" or "organizer_name" => "organizer_name",
            "eventtitle" or "event_title" => "event_title",
            "registrationcode" or "registration_code" => "registration_code",
            "personallink" or "personal_link" or "access_link" or "accesslink" or "upload_link" or "uploadlink" => "personal_link",
            "manageeventlink" or "manage_event_link" => "manage_event_link",
            "expireddate" or "expired_date" => "expired_date",
            "observation_reason" or "reason" => "reason",
            _ => key.Trim().ToLowerInvariant()
        };

        dict[normalized] = value;
    }

    private sealed record ParsedModel(
        Dictionary<string, string> Strings,
        List<RecoveredRegistrationItem> Registrations);
}

internal static class ResultMappingExtensions
{
    public static Result<string> Map(this Result result, Func<string> onSuccess) =>
        result.IsSuccess ? Result.Success(onSuccess()) : Result.Failure<string>(result.Error);
}
