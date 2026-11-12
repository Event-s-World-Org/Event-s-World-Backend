using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using EventsWorld.Shared.Kernel.Logging;
using Serilog.Core;
using Serilog.Events;

namespace EventsWorld.Api.Logging;

public sealed class SensitiveDataMaskingDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        if (value is null)
        {
            result = null;
            return false;
        }

        var type = value.GetType();

        // Do not destructure primitives or common scalar types
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan))
        {
            result = null;
            return false;
        }

        if (value is IEnumerable && value is not IDictionary)
        {
            result = null;
            return false;
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

        var structureProperties = new List<LogEventProperty>();

        foreach (var prop in properties)
        {
            object? propVal;
            try
            {
                propVal = prop.GetValue(value);
            }
            catch
            {
                continue;
            }

            if (SensitiveDataMasker.IsSensitivePropertyName(prop.Name))
            {
                var maskedVal = prop.Name.Contains("Email", StringComparison.OrdinalIgnoreCase)
                    ? SensitiveDataMasker.RedactedEmail
                    : SensitiveDataMasker.RedactedToken;
                structureProperties.Add(new LogEventProperty(prop.Name, new ScalarValue(maskedVal)));
            }
            else if (propVal is string strVal)
            {
                structureProperties.Add(new LogEventProperty(prop.Name, new ScalarValue(SensitiveDataMasker.Mask(strVal))));
            }
            else
            {
                structureProperties.Add(new LogEventProperty(prop.Name, propertyValueFactory.CreatePropertyValue(propVal, destructureObjects: true)));
            }
        }

        result = new StructureValue(structureProperties, type.Name);
        return true;
    }
}
