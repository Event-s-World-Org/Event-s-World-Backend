using System.Text.Json;
using EventsWorld.IntegrationTests.Infrastructure;
using EventsWorld.Modules.Notifications.Application.Emails;
using EventsWorld.Modules.Notifications.Application.Templates;
using EventsWorld.Modules.Notifications.Contracts;
using EventsWorld.Modules.Notifications.Domain;
using EventsWorld.Modules.Notifications.Infrastructure;
using EventsWorld.Modules.Notifications.Infrastructure.Persistence;
using EventsWorld.Shared.Infrastructure.Data;
using EventsWorld.Shared.Infrastructure.Jobs;
using EventsWorld.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace EventsWorld.IntegrationTests.Notifications;

[Collection(DatabaseTestGroup.Name)]
public sealed class NotificationsOutboxTests(PostgresFixture database)
{
    [Fact]
    public async Task RollbackTransaction_EnsuresNoEmailIsSent_AndOutboxIsEmpty()
    {
        using var scope = database.Api.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationsModule>();
        var testEmail = $"rollback_{Guid.NewGuid():N}@example.com";

        await unitOfWork.BeginTransactionAsync();

        notifications.Enqueue("registration_received", testEmail, "registration_received", new
        {
            participant_name = "Test Rollback",
            event_title = "Cancelled Event",
            access_link = "https://eventsworld.app/link"
        });

        // Roll back the business transaction
        await unitOfWork.RollbackAsync();

        // Verify outbox has no records for this email
        await using var connection = new NpgsqlConnection(database.AppConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM notifications.outbox WHERE payload->>'to_email' = @email",
            connection);
        cmd.Parameters.AddWithValue("email", testEmail);
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);

        Assert.Equal(0L, count);
    }

    [Fact]
    public async Task CommitTransaction_PersistsOutboxMessage_AndOutboxWorkerProcessesIt()
    {
        using var scope = database.Api.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationsModule>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var templateRenderer = scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();
        var worker = scope.ServiceProvider.GetRequiredService<OutboxWorker>();

        var testEmail = $"success_{Guid.NewGuid():N}@example.com";

        await unitOfWork.BeginTransactionAsync();

        notifications.Enqueue("registration_received", testEmail, "registration_received", new
        {
            participant_name = "Juan Perez",
            event_title = "Conferencia Anual",
            access_link = "https://eventsworld.app/participant/success-token"
        });

        await unitOfWork.CommitAsync();

        // Process pending messages with the outbox worker
        var processed = await worker.ProcessPendingMessagesAsync(dbContext, emailSender, templateRenderer);
        Assert.True(processed >= 1);

        // Verify outbox message is marked as processed
        var messages = await dbContext.Outbox.ToListAsync();
        var outboxMessage = messages.FirstOrDefault(m => m.Payload.Contains(testEmail));

        Assert.NotNull(outboxMessage);
        Assert.NotNull(outboxMessage.ProcessedAt);
        Assert.Null(outboxMessage.LastError);

        // Verify email_log has a row with provider message id
        var log = await dbContext.EmailLogs
            .FirstOrDefaultAsync(l => l.ToEmail == testEmail);

        Assert.NotNull(log);
        Assert.Equal("sent", log.Status);
        Assert.Equal("registration_received", log.Template);
        Assert.False(string.IsNullOrWhiteSpace(log.ProviderMessageId));
        Assert.Equal(outboxMessage.Id, log.OutboxId);
    }

    [Fact]
    public async Task OutboxWorker_WhenSenderFails_RetriesWithBackoff_AndRecovers()
    {
        using var scope = database.Api.Services.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationsModule>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var templateRenderer = scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();
        var worker = scope.ServiceProvider.GetRequiredService<OutboxWorker>();

        var testEmail = $"retry_{Guid.NewGuid():N}@example.com";

        await notifications.EnqueueAsync("pago_aprobado", testEmail, "payment_approved", new
        {
            participant_name = "Carlos Retries",
            event_title = "Retry Summit",
            registration_code = "EW-99XYZ",
            access_link = "https://eventsworld.app/qr"
        });

        var failingSender = new FailingEmailSender(new InvalidOperationException("Resend 503 Service Unavailable"));

        // Attempt 1: Fails
        await worker.ProcessPendingMessagesAsync(dbContext, failingSender, templateRenderer);

        var messages1 = await dbContext.Outbox.ToListAsync();
        var message = messages1.First(m => m.Payload.Contains(testEmail));
        Assert.Equal(1, message.Attempts);
        Assert.Null(message.ProcessedAt);
        Assert.NotNull(message.LastError);
        Assert.Contains("Resend 503", message.LastError);
        Assert.NotNull(message.NextAttemptAt);

        // Simulate backoff elapsed
        await using var connection = new NpgsqlConnection(database.AppConnectionString);
        await connection.OpenAsync();
        await using var updateCmd = new NpgsqlCommand(
            "UPDATE notifications.outbox SET next_attempt_at = now() - interval '1 second' WHERE id = @id",
            connection);
        updateCmd.Parameters.AddWithValue("id", message.Id);
        await updateCmd.ExecuteNonQueryAsync();

        // Refresh EF entity state
        await dbContext.Entry(message).ReloadAsync();

        // Attempt 2: Recovered sender
        var recoveredSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        await worker.ProcessPendingMessagesAsync(dbContext, recoveredSender, templateRenderer);

        await dbContext.Entry(message).ReloadAsync();
        Assert.NotNull(message.ProcessedAt);
        Assert.Null(message.LastError);

        var log = await dbContext.EmailLogs.FirstOrDefaultAsync(l => l.OutboxId == message.Id);
        Assert.NotNull(log);
        Assert.Equal("sent", log.Status);
    }

    [Fact]
    public async Task ScheduledJob_WithTwoInstancesRunning_OnlyOneRunsPerInterval()
    {
        var config = database.Api.Services.GetRequiredService<IConfiguration>();
        var job1 = new TestAdvisoryLockJob(config, "AdvisoryLockTestJob");
        var job2 = new TestAdvisoryLockJob(config, "AdvisoryLockTestJob");

        await using var holdLockConnection = new NpgsqlConnection(database.AppConnectionString);
        await holdLockConnection.OpenAsync();
        await using var lockCmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key);", holdLockConnection);
        lockCmd.Parameters.AddWithValue("key", job1.LockId);
        var acquired = (bool)(await lockCmd.ExecuteScalarAsync() ?? false);
        Assert.True(acquired, "Test connection must acquire the advisory lock.");

        try
        {
            // While the lock is held, neither job can run
            var run1 = await job1.TryExecuteOnceAsync();
            var run2 = await job2.TryExecuteOnceAsync();

            Assert.False(run1);
            Assert.False(run2);
            Assert.Equal(0, job1.ExecutionCount);
            Assert.Equal(0, job2.ExecutionCount);
        }
        finally
        {
            await using var unlockCmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@key);", holdLockConnection);
            unlockCmd.Parameters.AddWithValue("key", job1.LockId);
            await unlockCmd.ExecuteScalarAsync();
        }

        // Once released, one instance acquires and runs
        var executedAfterRelease = await job1.TryExecuteOnceAsync();
        Assert.True(executedAfterRelease);
        Assert.Equal(1, job1.ExecutionCount);
    }

    private sealed class FailingEmailSender(Exception exception) : IEmailSender
    {
        public Task<Result<EmailSendReceipt>> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Failure<EmailSendReceipt>(
                Error.Failure("resend.outage", exception.Message)));
        }
    }

    private sealed class TestAdvisoryLockJob(IConfiguration config, string name)
        : ScheduledJob(config, NullLogger.Instance)
    {
        public int ExecutionCount { get; private set; }

        public override string JobName => name;

        public override TimeSpan Interval => TimeSpan.FromMinutes(1);

        protected override Task RunJobAsync(CancellationToken cancellationToken)
        {
            ExecutionCount++;
            return Task.CompletedTask;
        }
    }
}
