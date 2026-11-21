using EventsWorld.Modules.Notifications.Application;
using EventsWorld.Modules.Notifications.Application.Emails;
using EventsWorld.Modules.Notifications.Application.Templates;
using EventsWorld.Modules.Notifications.Contracts;
using EventsWorld.Modules.Notifications.Infrastructure;
using EventsWorld.Modules.Notifications.Infrastructure.Email;
using EventsWorld.Modules.Notifications.Infrastructure.Persistence;
using EventsWorld.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EventsWorld.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<NotificationsDbContext>(NotificationsDbContext.Schema);

        services.AddSingleton<IValidateOptions<ResendOptions>, ResendOptionsValidator>();
        services.AddOptions<ResendOptions>()
            .Bind(configuration.GetSection(ResendOptions.Section))
            .ValidateOnStart();

        services.Configure<NotificationsOptions>(configuration.GetSection(NotificationsOptions.Section));

        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<INotificationsModule, NotificationsService>();
        services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();

        services.AddHttpClient<ResendEmailSender>();
        services.AddSingleton<DevelopmentEmailSender>();
        services.AddScoped<SmtpEmailSender>();

        services.AddScoped<IEmailSender>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ResendOptions>>().Value;
            var provider = options.Provider?.Trim() ?? "Resend";

            if (string.Equals(provider, "Development", StringComparison.OrdinalIgnoreCase))
            {
                return sp.GetRequiredService<DevelopmentEmailSender>();
            }

            if (string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase))
            {
                return sp.GetRequiredService<SmtpEmailSender>();
            }

            return sp.GetRequiredService<ResendEmailSender>();
        });

        services.AddSingleton<OutboxWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxWorker>());

        services.AddSingleton<OutboxRetentionJob>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxRetentionJob>());

        return services;
    }
}
