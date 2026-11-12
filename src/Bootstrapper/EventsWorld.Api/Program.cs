using EventsWorld.Modules.Accounts;
using EventsWorld.Modules.Audit;
using EventsWorld.Modules.Events;
using EventsWorld.Modules.Files;
using EventsWorld.Modules.Legal;
using EventsWorld.Modules.Notifications;
using EventsWorld.Modules.Payments;
using EventsWorld.Modules.Registrations;

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
