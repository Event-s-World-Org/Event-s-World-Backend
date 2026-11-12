namespace EventsWorld.Shared.Kernel.Logging;

/// <summary>
/// Marks a property that holds personal data or a secret (NFR-04): the logging pipeline writes
/// <c>[REDACTED]</c> instead of its value. Use it on commands and DTOs whose property names do not
/// already reveal it (for example a <c>Contact</c> field holding a phone number).
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class SensitiveDataAttribute : Attribute;
