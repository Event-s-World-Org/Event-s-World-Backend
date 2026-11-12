using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using EventsWorld.Shared.Infrastructure.Logging;
using Serilog.Core;
using Serilog.Events;

namespace EventsWorld.Api.Logging;

/// <summary>
/// For objects logged with <c>{@Object}</c>: sensitive properties (by name or
/// <c>[SensitiveData]</c>) are redacted and the other text values are masked.
/// </summary>
public sealed class SensitiveDataMaskingDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        ArgumentNullException.ThrowIfNull(propertyValueFactory);

        var type = value?.GetType();
        if (value is null || type is null || IsScalar(type) || value is IEnumerable)
        {
            result = null;
            return false;
        }

        var properties = new List<LogEventProperty>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (SensitiveDataMasker.IsSensitive(property))
            {
                properties.Add(new LogEventProperty(
                    property.Name,
                    new ScalarValue(SensitiveDataMasker.RedactedValueFor(property.Name))));
                continue;
            }

            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            properties.Add(new LogEventProperty(
                property.Name,
                propertyValue is string text
                    ? new ScalarValue(SensitiveDataMasker.Mask(text))
                    : propertyValueFactory.CreatePropertyValue(propertyValue, destructureObjects: true)));
        }

        result = new StructureValue(properties, type.Name);
        return true;
    }

    private static bool IsScalar(Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(Guid)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(DateOnly)
        || type == typeof(TimeOnly)
        || type == typeof(TimeSpan)
        || type == typeof(Uri);
}
