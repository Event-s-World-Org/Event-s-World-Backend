using EventsWorld.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsWorld.Modules.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class ScheduledJobRunConfiguration : IEntityTypeConfiguration<ScheduledJobRun>
{
    public void Configure(EntityTypeBuilder<ScheduledJobRun> builder)
    {
        builder.ToTable("scheduled_job_runs");

        builder.HasKey(r => r.JobName);

        builder.Property(r => r.JobName)
            .IsRequired();

        builder.Property(r => r.LastRunAt)
            .IsRequired();
    }
}
