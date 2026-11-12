using Serilog;
using Serilog.Formatting.Display;

namespace EventsWorld.Api.Logging;

/// <summary>
/// Serilog structured console logs (backend.md §5). Personal data and tokens never reach the output:
/// sensitive properties are redacted (enricher and destructuring policy), and the final text is
/// masked once more as a safety net. Levels come from the <c>Serilog</c> section of appsettings.
/// </summary>
public static class LoggingExtensions
{
    public const string OutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] ({CorrelationId}) {SourceContext}: {Message:lj}{NewLine}{Exception}";

    public static WebApplicationBuilder ConfigureSerilog(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSerilog((services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .WithSensitiveDataMasking());

        return builder;
    }

    /// <summary>
    /// The enrichers, destructuring policy and masked text output used by the API, written to the
    /// console. Tests pass their own writer to check the real output.
    /// </summary>
    public static LoggerConfiguration WithSensitiveDataMasking(this LoggerConfiguration logger, TextWriter? output = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var formatter = new MaskingTextFormatter(new MessageTemplateTextFormatter(OutputTemplate, formatProvider: null));

        logger
            .Enrich.FromLogContext()
            .Enrich.With<SensitiveDataMaskingEnricher>()
            .Destructure.With<SensitiveDataMaskingDestructuringPolicy>();

        return output is null
            ? logger.WriteTo.Console(formatter)
            : logger.WriteTo.Sink(new TextWriterSink(formatter, output));
    }
}
