using EventsWorld.Shared.Kernel.Logging;
using Serilog.Events;
using Serilog.Formatting;

namespace EventsWorld.Api.Logging;

public sealed class MaskingTextFormatter(ITextFormatter innerFormatter) : ITextFormatter
{
    private readonly ITextFormatter _innerFormatter = innerFormatter ?? throw new ArgumentNullException(nameof(innerFormatter));

    public void Format(LogEvent logEvent, TextWriter output)
    {
        using var stringWriter = new StringWriter();
        _innerFormatter.Format(logEvent, stringWriter);
        var formatted = stringWriter.ToString();
        var masked = SensitiveDataMasker.Mask(formatted);
        output.Write(masked);
    }
}
