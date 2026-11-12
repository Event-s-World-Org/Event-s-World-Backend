using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EventsWorld.IntegrationTests.Infrastructure;

/// <summary>In-memory API host for integration tests, with optional configuration overrides.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _settings = [];

    public ApiFactory WithSetting(string key, string? value)
    {
        _settings[key] = value;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Mvc.Testing computes the content root in MSBuild, which breaks when a parent folder
        // has an apostrophe (e.g. "Event'sWorld"). Point it at the API project explicitly.
        builder.UseContentRoot(SolutionPaths.ApiProject);

        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }
    }
}
