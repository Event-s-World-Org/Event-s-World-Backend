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

// Add services to the container.

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = System.DateTimeOffset.UtcNow }));

await app.RunAsync();

public partial class Program { }
