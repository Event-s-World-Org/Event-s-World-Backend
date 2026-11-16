using EventsWorld.IntegrationTests.Infrastructure;
using EventsWorld.Modules.Notifications.Application.Emails;
using EventsWorld.Modules.Notifications.Application.Templates;
using EventsWorld.Modules.Notifications.Contracts;
using EventsWorld.Modules.Notifications.Domain;
using EventsWorld.Modules.Notifications.Infrastructure;
using EventsWorld.Modules.Notifications.Infrastructure.Persistence;
using EventsWorld.Shared.Infrastructure.Data;
using EventsWorld.Shared.Infrastructure.Jobs;
using EventsWorld.Shared.Infrastructure.Persistence;
using EventsWorld.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace EventsWorld.IntegrationTests.Notifications;

[Collection(DatabaseTestGroup.Name)]
public sealed class NotificationsOutboxTests(PostgresFixture database)
{
    [Fact]
    public async Task RollbackTransaction_AcrossTwoModuleContexts_FlushesThenRevertsBothChanges()
    {
        using var scope = database.Api.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationsModule>();
        var notificationsDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        var eventsModule = scope.ServiceProvider.GetServices<ModuleDatabase>()
            .First(m => m.Schema == "events");
        var eventsDb = (DbContext)scope.ServiceProvider.GetRequiredService(eventsModule.ContextType);

        var testEmail = $"rollback_{Guid.NewGuid():N}@example.com";

        await unitOfWork.BeginTransactionAsync();
        unitOfWork.Enlist(eventsDb);

        notifications.Enqueue(new RegistrationReceivedEmail(
            testEmail,
            "Test Rollback",
            "Cancelled Event",
            "EW-9R8K7",
            "https://eventsworld.app/p/rollback"));

        // Flush the outbox insert to PostgreSQL inside the uncommitted transaction
        await notificationsDb.SaveChangesAsync();

        // Both DbContexts share the same underlying connection and transaction,
        // so eventsDb can see the uncommitted outbox row before rollback
        Assert.Same(eventsDb.Database.GetDbConnection(), notificationsDb.Database.GetDbConnection());
        var countInsideTx = await eventsDb.Database
            .SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM notifications.outbox WHERE payload->>'to_email' = {0}", testEmail)
            .SingleAsync();
        Assert.Equal(1, countInsideTx);

        // Roll back the business transaction
        await unitOfWork.RollbackAsync();

        // Verify on an independent connection that the flushed outbox row was rolled back
        await using var connection = new NpgsqlConnection(database.AppConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT count(*) FROM notifications.outbox WHERE payload->>'to_email' = @email",
            connection);
        cmd.Parameters.AddWithValue("email", testEmail);
        var countAfterRollback = (long)(await cmd.ExecuteScalarAsync() ?? 0L);

        Assert.Equal(0L, countAfterRollback);
    }

    [Fact]
    public async Task CommitAsync_WithoutExplicitBeginTransaction_OpensSharedTransactionAndRollsBackOnError()
    {
        using var scope = database.Api.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationsModule>();
        var notificationsDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        var testEmail = $"atomic_{Guid.NewGuid():N}@example.com";

        notifications.Enqueue(new RegistrationReceivedEmail(
            testEmail,
            "Atomic User",
            "Atomic Event",
            "EW-4T5Y6",
            "https://eventsworld.app/p/atomic"));

        // Stage an EmailLog with an invalid status that violates ck_email_log_status in PostgreSQL
        var invalidLog = EmailLog.CreateSent(null, testEmail, EmailTemplates.RegistrationReceived, "msg_1", DateTimeOffset.UtcNow);
        typeof(EmailLog).GetProperty(nameof(EmailLog.Status))!.SetValue(invalidLog, "invalid_status");
        notificationsDb.EmailLogs.Add(invalidLog);

        await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.CommitAsync());

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

        notifications.Enqueue(new RegistrationReceivedEmail(
            testEmail,
            "Juan Pérez",
            "Conferencia Anual",
            "EW-7F3K2",
            "https://eventsworld.app/p/success-token"));

        await unitOfWork.CommitAsync();

        var processed = await worker.ProcessPendingMessagesAsync(dbContext, emailSender, templateRenderer);
        Assert.True(processed >= 1);

        var messages = await dbContext.Outbox.AsNoTracking().ToListAsync();
        var outboxMessage = messages.FirstOrDefault(m => m.Payload.Contains(testEmail, StringComparison.Ordinal));

        Assert.NotNull(outboxMessage);
        Assert.NotNull(outboxMessage.ProcessedAt);
        Assert.Null(outboxMessage.FailedAt);
        Assert.Null(outboxMessage.LastError);

        var log = await dbContext.EmailLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.ToEmail == testEmail);

        Assert.NotNull(log);
        Assert.Equal("sent", log.Status);
        Assert.Equal(EmailTemplates.RegistrationReceived, log.Template);
        Assert.False(string.IsNullOrWhiteSpace(log.ProviderMessageId));
        Assert.Equal(outboxMessage.Id, log.OutboxId);
    }

    [Fact]
    public async Task OutboxWorker_WhenSenderFailsTransiently_RetriesWithBackoff_AndRecovers()
    {
        using var scope = database.Api.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationsModule>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var templateRenderer = scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();
        var worker = scope.ServiceProvider.GetRequiredService<OutboxWorker>();

        var testEmail = $"retry_{Guid.NewGuid():N}@example.com";

        notifications.Enqueue(new PaymentApprovedEmail(
            testEmail,
            "Carlos Retries",
            "Retry Summit",
            "EW-99XYZ",
            "https://eventsworld.app/p/retry"));
        await unitOfWork.CommitAsync();

        var failingSender = newSelectiveFailingSender(
            shouldFail: _ => true,
            isTransient: true,
            errorDescription: "Resend 503 Service Unavailable");

        // Attempt 1: Fails transiently
        await worker.ProcessPendingMessagesAsync(dbContext, failingSender, templateRenderer);

        var messages1 = await dbContext.Outbox.AsNoTracking().ToListAsync();
        var message = messages1.First(m => m.Payload.Contains(testEmail, StringComparison.Ordinal));
        Assert.Equal(1, message.Attempts);
        Assert.Null(message.ProcessedAt);
        Assert.Null(message.FailedAt);
        Assert.NotNull(message.LastError);
        Assert.Contains("Resend 503", message.LastError);
        Assert.NotNull(message.NextAttemptAt);

        // Simulate backoff elapsed
        await ElapseBackoffAsync(message.Id);

        // Attempt 2: Recovered sender
        var recoveredSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        await worker.ProcessPendingMessagesAsync(dbContext, recoveredSender, templateRenderer);

        var updated = await dbContext.Outbox.AsNoTracking().FirstAsync(m => m.Id == message.Id);
        Assert.NotNull(updated.ProcessedAt);
        Assert.Null(updated.FailedAt);
        Assert.Null(updated.LastError);

        var log = await dbContext.EmailLogs.AsNoTracking().FirstOrDefaultAsync(l => l.OutboxId == message.Id);
        Assert.NotNull(log);
        Assert.Equal("sent", log.Status);
        Assert.False(string.IsNullOrWhiteSpace(log.ProviderMessageId));
    }

    [Fact]
    public async Task OutboxWorker_WhenEmailExhaustsMaxAttempts_MarksFailedWithNullProviderId_AndNeverBlocksSubsequentEmails()
    {
        using var scope = database.Api.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationsModule>();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var templateRenderer = scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();

        var workerOptions = Options.Create(new NotificationsOptions
        {
            WorkerEnabled = false,
            MaxAttempts = 3,
            BatchSize = 25
        });
        var worker = new OutboxWorker(scopeFactory, config, workerOptions, NullLogger<OutboxWorker>.Instance);

        var badEmail = $"bad_{Guid.NewGuid():N}@example.com";
        var lateEmail = $"late_{Guid.NewGuid():N}@example.com";

        notifications.Enqueue(new RegistrationReceivedEmail(
            badEmail, "Bad User", "Evento", "EW-23456", "https://eventsworld.app/p/bad"));
        await unitOfWork.CommitAsync();

        var sender = newSelectiveFailingSender(
            shouldFail: msg => string.Equals(msg.To, badEmail, StringComparison.OrdinalIgnoreCase),
            isTransient: true,
            errorDescription: "Simulated upstream timeout");

        // Cycles 1, 2, 3: badEmail fails 3 times and reaches MaxAttempts (3)
        for (var cycle = 1; cycle <= 3; cycle++)
        {
            await worker.ProcessPendingMessagesAsync(dbContext, sender, templateRenderer);

            var currentBad = (await dbContext.Outbox.AsNoTracking().ToListAsync())
                .First(m => m.Payload.Contains(badEmail, StringComparison.Ordinal));
            Assert.Equal(cycle, currentBad.Attempts);

            if (cycle < 3)
            {
                Assert.Null(currentBad.FailedAt);
                await ElapseBackoffAsync(currentBad.Id);
            }
            else
            {
                Assert.NotNull(currentBad.FailedAt);
                Assert.Null(currentBad.ProcessedAt);
                Assert.Null(currentBad.NextAttemptAt);
            }
        }

        // Verify email_log recorded the failed email with null provider_message_id
        var failedLog = await dbContext.EmailLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.ToEmail == badEmail);
        Assert.NotNull(failedLog);
        Assert.Equal("failed", failedLog.Status);
        Assert.Null(failedLog.ProviderMessageId);

        // Now enqueue a late email and run cycle 4: badEmail must not be retried, and lateEmail must be sent!
        notifications.Enqueue(new RegistrationReceivedEmail(
            lateEmail, "Late User", "Evento", "EW-78923", "https://eventsworld.app/p/late"));
        await unitOfWork.CommitAsync();

        await worker.ProcessPendingMessagesAsync(dbContext, sender, templateRenderer);

        var allMessages = await dbContext.Outbox.AsNoTracking().ToListAsync();
        var badAfterCycle4 = allMessages.First(m => m.Payload.Contains(badEmail, StringComparison.Ordinal));
        var lateAfterCycle4 = allMessages.First(m => m.Payload.Contains(lateEmail, StringComparison.Ordinal));

        Assert.Equal(3, badAfterCycle4.Attempts);
        Assert.NotNull(lateAfterCycle4.ProcessedAt);

        var sentLateLog = await dbContext.EmailLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.ToEmail == lateEmail);
        Assert.NotNull(sentLateLog);
        Assert.Equal("sent", sentLateLog.Status);
        Assert.NotNull(sentLateLog.ProviderMessageId);
    }

    [Fact]
    public async Task OutboxWorker_WhenPayloadIsInvalidOrPermanent4xx_FailsImmediatelyOnAttempt1_WithoutBlockingQueue()
    {
        using var scope = database.Api.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var templateRenderer = scope.ServiceProvider.GetRequiredService<IEmailTemplateRenderer>();
        var worker = scope.ServiceProvider.GetRequiredService<OutboxWorker>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var corruptEmail = $"corrupt_{Guid.NewGuid():N}@example.com";
        var validEmail = $"valid_{Guid.NewGuid():N}@example.com";

        // Insert a message with missing required template fields
        var corruptOutbox = OutboxMessage.Create(
            EmailTemplates.RegistrationReceived,
            $"{{\"to_email\":\"{corruptEmail}\",\"template\":\"registration_received\"}}",
            DateTimeOffset.UtcNow);
        dbContext.Outbox.Add(corruptOutbox);

        // Insert a valid message right after it
        var validNotifications = scope.ServiceProvider.GetRequiredService<INotificationsModule>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        validNotifications.Enqueue(new RegistrationReceivedEmail(
            validEmail, "Valid User", "Valid Event", "EW-55555", "https://eventsworld.app/p/valid"));
        await unitOfWork.CommitAsync();

        await worker.ProcessPendingMessagesAsync(dbContext, sender, templateRenderer);

        var reloadedCorrupt = await dbContext.Outbox.AsNoTracking().FirstAsync(m => m.Id == corruptOutbox.Id);
        Assert.Equal(1, reloadedCorrupt.Attempts);
        Assert.NotNull(reloadedCorrupt.FailedAt);
        Assert.Null(reloadedCorrupt.ProcessedAt);

        var validOutbox = (await dbContext.Outbox.AsNoTracking().ToListAsync())
            .First(m => m.Payload.Contains(validEmail, StringComparison.Ordinal));
        Assert.NotNull(validOutbox.ProcessedAt);
    }

    [Fact]
    public async Task ScheduledJob_WithTwoInstancesRunningAcrossIntervals_ExecutesOnlyOncePerInterval()
    {
        var config = database.Api.Services.GetRequiredService<IConfiguration>();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
        var jobName = $"IntervalJob_{Guid.NewGuid():N}";
        var interval = TimeSpan.FromMinutes(1);

        var instance1 = new FastTestJob(config, jobName, interval, clock);
        var instance2 = new FastTestJob(config, jobName, interval, clock);

        for (var i = 0; i < 3; i++)
        {
            // Instance 1 runs and completes in a few milliseconds
            var ran1 = await instance1.TryExecuteOnceAsync();

            // Instance 2 wakes up 5 seconds later (still inside the same 1-minute interval)
            clock.Advance(TimeSpan.FromSeconds(5));
            var ran2 = await instance2.TryExecuteOnceAsync();

            Assert.True(ran1);
            Assert.False(ran2);

            // Advance past the 1-minute interval for the next tick
            clock.Advance(TimeSpan.FromSeconds(56));
        }

        Assert.Equal(3, instance1.ExecutionCount + instance2.ExecutionCount);
    }

    [Fact]
    public void AllModuleDbContexts_HaveNoPendingModelChanges()
    {
        using var scope = database.Api.Services.CreateScope();
        var modules = scope.ServiceProvider.GetServices<ModuleDatabase>().ToList();
        Assert.NotEmpty(modules);

        foreach (var module in modules)
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(module.ContextType);
            Assert.False(
                context.Database.HasPendingModelChanges(),
                $"DbContext '{module.ContextType.Name}' has pending model changes not captured in a migration.");
        }
    }

    private async Task ElapseBackoffAsync(Guid messageId)
    {
        await using var connection = new NpgsqlConnection(database.AppConnectionString);
        await connection.OpenAsync();
        await using var updateCmd = new NpgsqlCommand(
            "UPDATE notifications.outbox SET next_attempt_at = now() - interval '1 second' WHERE id = @id",
            connection);
        updateCmd.Parameters.AddWithValue("id", messageId);
        await updateCmd.ExecuteNonQueryAsync();
    }

    private static SelectiveFailingEmailSender newSelectiveFailingSender(
        Func<EmailMessage, bool> shouldFail,
        bool isTransient,
        string errorDescription) =>
        new(shouldFail, isTransient, errorDescription);

    private sealed class SelectiveFailingEmailSender(
        Func<EmailMessage, bool> shouldFail,
        bool isTransient,
        string errorDescription) : IEmailSender
    {
        public Task<Result<EmailSendReceipt>> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (shouldFail(message))
            {
                var error = isTransient
                    ? EmailSendError.Transient("notifications.provider_down", errorDescription)
                    : EmailSendError.Permanent("notifications.provider_rejected", errorDescription);
                return Task.FromResult(Result.Failure<EmailSendReceipt>(error));
            }

            return Task.FromResult(Result.Success(new EmailSendReceipt($"ok_{Guid.CreateVersion7():N}")));
        }
    }

    private sealed class FastTestJob(
        IConfiguration configuration,
        string jobName,
        TimeSpan interval,
        TimeProvider timeProvider)
        : ScheduledJob(configuration, NullLogger.Instance, timeProvider)
    {
        private int _executionCount;

        public int ExecutionCount => _executionCount;
        public override string JobName => jobName;
        public override TimeSpan Interval => interval;

        protected override Task RunJobAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executionCount);
            return Task.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}
