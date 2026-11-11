using EventsWorld.Modules.Accounts.Api;
using EventsWorld.Modules.Audit.Api;
using EventsWorld.Modules.Events.Api;
using EventsWorld.Modules.Files.Api;
using EventsWorld.Modules.Legal.Api;
using EventsWorld.Modules.Notifications.Api;
using EventsWorld.Modules.Payments.Api;
using EventsWorld.Modules.Registrations.Api;

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = System.DateTimeOffset.UtcNow }));

app.Run();
public partial class Program { }
