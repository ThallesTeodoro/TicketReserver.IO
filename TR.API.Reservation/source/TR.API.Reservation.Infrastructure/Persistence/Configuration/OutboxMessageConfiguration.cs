namespace TR.API.Reservation.Infrastructure.Persistence.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TR.API.Reservation.Domain.Entities;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.CorrelationId).HasColumnName("correlation_id").IsRequired();
        builder.Property(m => m.EventType).HasColumnName("event_type").HasMaxLength(150).IsRequired();
        builder.Property(m => m.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100).IsRequired();
        builder.Property(m => m.AggregateId).HasColumnName("aggregate_id").IsRequired();
        builder.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.DelaySeconds).HasColumnName("delay_seconds").IsRequired();
        builder.Property(m => m.DeliverAfter).HasColumnName("deliver_after").IsRequired();
        builder.Property(m => m.Status).HasColumnName("status").IsRequired();
        builder.Property(m => m.RetryCount).HasColumnName("retry_count").IsRequired();
        builder.Property(m => m.ErrorMessage).HasColumnName("error_message");
        builder.Property(m => m.ProcessedAt).HasColumnName("processed_at");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
