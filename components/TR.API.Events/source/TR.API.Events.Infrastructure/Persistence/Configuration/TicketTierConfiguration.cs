namespace TR.API.Events.Infrastructure.Persistence.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TR.API.Events.Domain.Entities;

public sealed class TicketTierConfiguration : IEntityTypeConfiguration<TicketTier>
{
    public void Configure(EntityTypeBuilder<TicketTier> builder)
    {
        builder.ToTable("ticket_tiers");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(t => t.SectionId).HasColumnName("section_id").IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(t => t.TierType).HasColumnName("tier_type").IsRequired();
        builder.Property(t => t.Price).HasColumnName("price").HasPrecision(12, 2).IsRequired();
        builder.Property(t => t.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(t => t.TotalQuantity).HasColumnName("total_quantity").IsRequired();
        builder.Property(t => t.SalesStartAt).HasColumnName("sales_start_at").IsRequired();
        builder.Property(t => t.SalesEndAt).HasColumnName("sales_end_at").IsRequired();
        builder.Property(t => t.Status).HasColumnName("status").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
