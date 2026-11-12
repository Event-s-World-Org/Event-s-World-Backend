using Serilog;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace EventsWorld.Api.Logging;

public static class LoggingExtensions
{
    public const string OutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] (CorrelationId: {CorrelationId}) {Message:lj}{NewLine}{Exception}";

    public static WebApplicationBuilder ConfigureSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            var innerFormatter = new MessageTemplateTextFormatter(OutputTemplate);
            var maskingFormatter = new MaskingTextFormatter(innerFormatter);

            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.With<SensitiveDataMaskingEnricher>()
                .Destructure.With<SensitiveDataMaskingDestructuringPolicy>()
                .WriteTo.Console(maskingFormatter);
        });

        return builder;
    }
}
