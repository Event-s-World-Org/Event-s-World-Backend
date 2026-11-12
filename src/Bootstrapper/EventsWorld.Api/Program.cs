using EventsWorld.Api.Endpoints;
using EventsWorld.Api.Extensions;
using EventsWorld.Api.Logging;
using EventsWorld.Modules.Accounts.Api;
using EventsWorld.Modules.Audit.Api;
using EventsWorld.Modules.Events.Api;
using EventsWorld.Modules.Files.Api;
using EventsWorld.Modules.Legal.Api;
using EventsWorld.Modules.Notifications.Api;
using EventsWorld.Modules.Payments.Api;
using EventsWorld.Modules.Registrations.Api;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with structured console logging and sensitive data masking
builder.ConfigureSerilog();

// Configure cross-cutting API concerns: RFC 9457 errors, CORS, rate limiting, health checks, OpenTelemetry, TimeProvider
builder.Services.AddOpenApi();
builder.Services.AddApiCrossCuttingConcerns(builder.Configuration);

// Register modules
builder.Services
    .AddAccountsModule(builder.Configuration)
    .AddEventsModule(builder.Configuration)
    .AddRegistrationsModule(builder.Configuration)
    .AddPaymentsModule(builder.Configuration)
    .AddFilesModule(builder.Configuration)
    .AddNotificationsModule(builder.Configuration)
    .AddAuditModule(builder.Configuration)
    .AddLegalModule(builder.Configuration);

var app = builder.Build();

// Use cross-cutting request pipeline: Correlation ID, security headers, exception handling, Scalar UI, CORS, rate limiting, health checks
app.UseApiCrossCuttingConcerns();

app.UseHttpsRedirection();

// Register v1 route groups (/api/v1/public, /participant, /me, /events/{eventId}, /admin)
app.MapApiV1();

app.Run();

public partial class Program { }
