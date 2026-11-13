using EventsWorld.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsWorld.Modules.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
            .IsRequired();

        builder.Property(m => m.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(m => m.Attempts)
            .HasDefaultValue(0);

        builder.Property(m => m.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.HasIndex(m => new { m.ProcessedAt, m.NextAttemptAt });
        builder.HasIndex(m => m.CreatedAt);
    }
}
