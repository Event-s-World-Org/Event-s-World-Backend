using System.Reflection;
using EventsWorld.Api.Endpoints;
using EventsWorld.Api.Extensions;
using EventsWorld.Api.Logging;
using EventsWorld.Modules.Accounts;
using EventsWorld.Modules.Audit;
using EventsWorld.Modules.Events;
using EventsWorld.Modules.Files;
using EventsWorld.Modules.Legal;
using EventsWorld.Modules.Notifications;
using EventsWorld.Modules.Payments;
using EventsWorld.Modules.Registrations;
using EventsWorld.Shared.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureSerilog();

Assembly[] moduleAssemblies =
[
    typeof(AccountsModule).Assembly,
    typeof(EventsModule).Assembly,
    typeof(RegistrationsModule).Assembly,
    typeof(PaymentsModule).Assembly,
    typeof(FilesModule).Assembly,
    typeof(NotificationsModule).Assembly,
    typeof(AuditModule).Assembly,
    typeof(LegalModule).Assembly,
];

// RFC 9457 errors, validation, CORS, rate limiting, health checks, OpenAPI, OpenTelemetry, TimeProvider (TS-004).
builder.Services.AddApiCrossCuttingConcerns(builder.Configuration, [typeof(Program).Assembly, .. moduleAssemblies]);

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

// `migrate` applies every module's migrations as ew_migrator and exits (database.md §6).
// The deployment pipeline runs it before starting the new API version.
if (args.Length > 0 && args[0] == "migrate")
{
    var migrationsConnection = builder.Configuration.GetConnectionString(ConnectionStringNames.Migrations)
        ?? throw new InvalidOperationException(
            $"Set ConnectionStrings:{ConnectionStringNames.Migrations} to run the migrations.");

    await app.Services.MigrateModuleDatabasesAsync(migrationsConnection);
    return;
}

app.UseApiCrossCuttingConcerns();

// /api/v1/public, /participant, /me, /events/{eventId}, /admin and every module's endpoints.
app.MapApiV1();

await app.RunAsync();

public partial class Program { }
