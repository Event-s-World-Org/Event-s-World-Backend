using EventsWorld.Shared.Infrastructure.Logging;
using Serilog.Core;
using Serilog.Events;

namespace EventsWorld.Api.Logging;

/// <summary>
/// For message properties such as <c>{Email}</c>: sensitive names are redacted and other text
/// values are masked.
/// </summary>
public sealed class SensitiveDataMaskingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var updates = new List<LogEventProperty>();

        foreach (var (name, value) in logEvent.Properties)
        {
            if (SensitiveDataMasker.IsSensitivePropertyName(name))
            {
                updates.Add(new LogEventProperty(name, new ScalarValue(SensitiveDataMasker.RedactedValueFor(name))));
            }
            else if (value is ScalarValue { Value: string text })
            {
                var masked = SensitiveDataMasker.Mask(text);
                if (!string.Equals(masked, text, StringComparison.Ordinal))
                {
                    updates.Add(new LogEventProperty(name, new ScalarValue(masked)));
                }
            }
        }

        foreach (var property in updates)
        {
            logEvent.AddOrUpdateProperty(property);
        }
    }
}
