using System.Reflection;
using EventsWorld.Api.Errors;
using EventsWorld.Api.Health;
using EventsWorld.Api.Middleware;
using EventsWorld.Api.RateLimiting;
using FluentValidation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

namespace EventsWorld.Api.Extensions;

/// <summary>The shared request pipeline of TS-004 (backend.md §3–5).</summary>
public static class ServiceCollectionExtensions
{
    public const string ServiceName = "EventsWorld.Api";

    /// <param name="services">The host services.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="validatorAssemblies">Assemblies with FluentValidation validators (the host and every module).</param>
    public static IServiceCollection AddApiCrossCuttingConcerns(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<Assembly> validatorAssemblies)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddApiProblemDetails();
        services.AddValidatorsFromAssemblies(validatorAssemblies, includeInternalTypes: true);

        services.AddOpenApi();
        services.AddFrontendCors(configuration);
        services.AddTrustedProxies(configuration);
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });
        services.AddApiRateLimiting(configuration);
        services.AddApiHealthChecks();

        // Traces and metrics are collected now; the exporter is configured later (NFR-06, [Next]).
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        return services;
    }

    public static WebApplication UseApiCrossCuttingConcerns(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // The real client IP and scheme first, so everything after it (logs, rate limit, HTTPS) uses them.
        app.UseForwardedHeaders();
        app.UseCorrelationId();
        app.UseSecurityHeaders();
        app.UseExceptionHandler();
        // Empty 4xx/5xx responses (unknown route, wrong method) also get a ProblemDetails body.
        app.UseStatusCodePages();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options => options.WithTitle("Event's World API"));
        }

        app.UseCors(NetworkExtensions.CorsPolicyName);
        app.UseRateLimiter();
        app.MapApiHealthChecks();

        return app;
    }
}
