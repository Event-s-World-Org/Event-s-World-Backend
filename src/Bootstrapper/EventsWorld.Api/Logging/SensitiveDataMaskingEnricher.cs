using EventsWorld.Shared.Kernel.Logging;
using Serilog.Core;
using Serilog.Events;

namespace EventsWorld.Api.Logging;

public sealed class SensitiveDataMaskingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var propertiesToUpdate = new List<KeyValuePair<string, LogEventPropertyValue>>();

        foreach (var (key, value) in logEvent.Properties)
        {
            if (SensitiveDataMasker.IsSensitivePropertyName(key))
            {
                var masked = key.Contains("Email", StringComparison.OrdinalIgnoreCase)
                    ? SensitiveDataMasker.RedactedEmail
                    : SensitiveDataMasker.RedactedToken;
                propertiesToUpdate.Add(new KeyValuePair<string, LogEventPropertyValue>(key, new ScalarValue(masked)));
            }
            else if (value is ScalarValue { Value: string strVal })
            {
                var masked = SensitiveDataMasker.Mask(strVal);
                if (!string.Equals(masked, strVal, StringComparison.Ordinal))
                {
                    propertiesToUpdate.Add(new KeyValuePair<string, LogEventPropertyValue>(key, new ScalarValue(masked)));
                }
            }
        }

        foreach (var (key, value) in propertiesToUpdate)
        {
            logEvent.AddOrUpdateProperty(new LogEventProperty(key, value));
        }
    }
}
