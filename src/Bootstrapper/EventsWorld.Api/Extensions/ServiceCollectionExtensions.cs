using EventsWorld.Api.Errors;
using EventsWorld.Api.Health;
using EventsWorld.Api.Middleware;
using EventsWorld.Api.RateLimiting;
using FluentValidation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

namespace EventsWorld.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public const string CorsPolicyName = "DefaultCorsPolicy";

    public static IServiceCollection AddApiCrossCuttingConcerns(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Testable time provider
        services.AddSingleton(TimeProvider.System);

        // 2. Exception handling & RFC 9457 ProblemDetails
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddValidatorsFromAssemblyContaining<Program>();

        // 3. CORS
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:4200", "http://localhost:3000"];

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        // 4. Rate Limiting
        services.AddApiRateLimiting(configuration);

        // 5. Health Checks
        services.AddApiHealthChecks(configuration);

        // 6. OpenTelemetry
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        return services;
    }

    public static WebApplication UseApiCrossCuttingConcerns(this WebApplication app)
    {
        // 1. Correlation ID
        app.UseCorrelationId();

        // 2. Security Headers
        app.UseSecurityHeaders();

        // 3. Global Exception Handler
        app.UseExceptionHandler();

        // 4. OpenAPI & Scalar in Development
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
            {
                options.WithTitle("EventsWorld API Reference");
                options.WithTheme(ScalarTheme.Moon);
            });
        }

        // 5. CORS
        app.UseCors(CorsPolicyName);

        // 6. Rate Limiter
        app.UseRateLimiter();

        // 7. Health Checks
        app.MapApiHealthChecks();

        return app;
    }
}
