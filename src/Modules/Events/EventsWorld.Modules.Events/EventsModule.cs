using EventsWorld.Modules.Events.Infrastructure.Persistence;
using EventsWorld.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventsWorld.Modules.Events;

public static class EventsModule
{
    public static IServiceCollection AddEventsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<EventsDbContext>(EventsDbContext.Schema);

        return services;
    }
}
