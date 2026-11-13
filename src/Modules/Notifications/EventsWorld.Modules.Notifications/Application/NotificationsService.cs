using System.Text.Json;
using EventsWorld.Modules.Notifications.Contracts;
using EventsWorld.Modules.Notifications.Domain;

namespace EventsWorld.Modules.Notifications.Application;

/// <summary>
/// Implements <see cref="INotificationsModule"/> by staging messages into the outbox
/// inside the caller's unit of work transaction (database.md §3.6, ADR-0003 §5–6).
/// </summary>
internal sealed class NotificationsService : INotificationsModule
{
    private readonly IOutboxStore _outboxStore;
    private readonly TimeProvider _timeProvider;

    public NotificationsService(
        IOutboxStore outboxStore,
        TimeProvider? timeProvider = null)
    {
        _outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void Enqueue(OutboxMessageDto message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payloadJson = SerializePayload(message.ToEmail, message.Template, message.Payload);
        var outboxMessage = OutboxMessage.Create(message.Type, payloadJson, _timeProvider.GetUtcNow());

        _outboxStore.Add(outboxMessage);
    }

    public void Enqueue(string type, string toEmail, string template, object payload) =>
        Enqueue(new OutboxMessageDto(type, toEmail, template, payload));

    public async Task EnqueueAsync(
        string type,
        string toEmail,
        string template,
        object payload,
        CancellationToken cancellationToken = default)
    {
        Enqueue(type, toEmail, template, payload);
        await _outboxStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string SerializePayload(string toEmail, string template, object payload)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["to_email"] = toEmail,
            ["template"] = template
        };

        if (payload is IReadOnlyDictionary<string, object?> d)
        {
            foreach (var (k, v) in d)
            {
                dict[k] = v;
            }
        }
        else if (payload is not null)
        {
            var json = JsonSerializer.Serialize(payload);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.Clone();
                }
            }
            else
            {
                dict["data"] = payload;
            }
        }

        return JsonSerializer.Serialize(dict);
    }
}
