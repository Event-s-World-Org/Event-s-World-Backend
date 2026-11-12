using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace EventsWorld.Api.Logging;

/// <summary>Writes formatted log events to a <see cref="TextWriter"/> (used by the logging tests).</summary>
public sealed class TextWriterSink(ITextFormatter formatter, TextWriter output) : ILogEventSink
{
    private readonly Lock _sync = new();

    public void Emit(LogEvent logEvent)
    {
        lock (_sync)
        {
            formatter.Format(logEvent, output);
            output.Flush();
        }
    }
}
