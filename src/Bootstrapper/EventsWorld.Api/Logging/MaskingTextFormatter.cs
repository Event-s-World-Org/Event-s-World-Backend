using EventsWorld.Shared.Infrastructure.Logging;
using Serilog.Events;
using Serilog.Formatting;

namespace EventsWorld.Api.Logging;

/// <summary>Masks emails and tokens in the final log text, including exception messages.</summary>
public sealed class MaskingTextFormatter(ITextFormatter innerFormatter) : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);

        using var buffer = new StringWriter();
        innerFormatter.Format(logEvent, buffer);
        output.Write(SensitiveDataMasker.Mask(buffer.ToString()));
    }
}
