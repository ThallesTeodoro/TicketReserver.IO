namespace TR.API.Reservation.Infrastructure.Persistence.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TR.API.Reservation.Domain.Entities;

public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");

        builder.HasKey(m => new { m.MessageId, m.ConsumerGroup });
        builder.Property(m => m.MessageId).HasColumnName("message_id").HasMaxLength(150).IsRequired();
        builder.Property(m => m.ConsumerGroup).HasColumnName("consumer_group").HasMaxLength(100).IsRequired();
        builder.Property(m => m.EventType).HasColumnName("event_type").HasMaxLength(150).IsRequired();
        builder.Property(m => m.ProcessedAt).HasColumnName("processed_at").IsRequired();
    }
}
