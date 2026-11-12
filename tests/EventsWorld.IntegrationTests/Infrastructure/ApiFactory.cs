using EventsWorld.Shared.Infrastructure.Api;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EventsWorld.IntegrationTests.Infrastructure;

/// <summary>
/// In-memory API host for integration tests (Development environment), with optional configuration
/// overrides and the test-only endpoints of <see cref="TestEndpoints"/>.
/// </summary>
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
        builder.UseSolutionContentRoot();

        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, ClientIpStartupFilter>();
            services.AddSingleton<IApiEndpoints, TestEndpoints>();
            services.AddScoped<IValidator<EchoRequest>, EchoRequestValidator>();
        });
    }
}

internal static class WebHostBuilderExtensions
{
    /// <summary>
    /// Mvc.Testing computes the content root in MSBuild, which breaks when a parent folder has an
    /// apostrophe (e.g. "Event'sWorld"). Point it at the API project explicitly.
    /// </summary>
    public static IWebHostBuilder UseSolutionContentRoot(this IWebHostBuilder builder) =>
        builder.UseContentRoot(SolutionPaths.ApiProject);
}
