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

        services.Configure<ResendOptions>(configuration.GetSection(ResendOptions.Section));
        services.Configure<NotificationsOptions>(configuration.GetSection(NotificationsOptions.Section));

        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<INotificationsModule, NotificationsService>();
        services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();

        services.AddHttpClient<ResendEmailSender>();
        services.AddSingleton<DevelopmentEmailSender>();

        services.AddScoped<IEmailSender>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ResendOptions>>().Value;
            if (!string.Equals(options.Provider, "Development", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(options.ApiKey))
            {
                return sp.GetRequiredService<ResendEmailSender>();
            }

            return sp.GetRequiredService<DevelopmentEmailSender>();
        });

        services.AddSingleton<OutboxWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxWorker>());

        return services;
    }
}
