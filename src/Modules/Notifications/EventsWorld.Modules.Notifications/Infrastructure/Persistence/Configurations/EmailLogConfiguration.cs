using EventsWorld.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsWorld.Modules.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("email_log", t =>
            t.HasCheckConstraint("ck_email_log_status", "status IN ('sent', 'failed')"));

        builder.HasKey(l => l.Id);

        builder.Property(l => l.ToEmail)
            .HasColumnType("citext")
            .IsRequired();

        builder.Property(l => l.Template)
            .IsRequired();

        builder.Property(l => l.ProviderMessageId)
            .IsRequired(false);

        builder.Property(l => l.Status)
            .IsRequired();

        builder.Property(l => l.SentAt)
            .HasDefaultValueSql("now()");

        builder.HasOne<OutboxMessage>()
            .WithMany()
            .HasForeignKey(l => l.OutboxId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(l => l.OutboxId);
        builder.HasIndex(l => l.ToEmail);
        builder.HasIndex(l => l.SentAt);
    }
}
