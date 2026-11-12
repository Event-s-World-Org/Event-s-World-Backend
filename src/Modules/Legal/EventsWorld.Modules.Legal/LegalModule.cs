using EventsWorld.Modules.Legal.Infrastructure.Persistence;
using EventsWorld.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventsWorld.Modules.Legal;

public static class LegalModule
{
    public static IServiceCollection AddLegalModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<LegalDbContext>(LegalDbContext.Schema);

        return services;
    }
}
