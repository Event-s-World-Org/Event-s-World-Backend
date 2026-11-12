using EventsWorld.Modules.Registrations.Infrastructure.Persistence;
using EventsWorld.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventsWorld.Modules.Registrations;

public static class RegistrationsModule
{
    public static IServiceCollection AddRegistrationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<RegistrationsDbContext>(RegistrationsDbContext.Schema);

        return services;
    }
}
