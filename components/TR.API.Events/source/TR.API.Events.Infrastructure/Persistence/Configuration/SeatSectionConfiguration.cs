namespace TR.API.Events.Infrastructure.Persistence.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TR.API.Events.Domain.Entities;

public sealed class SeatSectionConfiguration : IEntityTypeConfiguration<SeatSection>
{
    public void Configure(EntityTypeBuilder<SeatSection> builder)
    {
        builder.ToTable("seat_sections");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.VenueId).HasColumnName("venue_id").IsRequired();
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(s => s.Code).HasColumnName("code").HasMaxLength(30).IsRequired();
        builder.Property(s => s.IsNumbered).HasColumnName("is_numbered").IsRequired();
        builder.Property(s => s.Capacity).HasColumnName("capacity").IsRequired();
        builder.Property(s => s.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(s => new { s.VenueId, s.Code }).IsUnique();
    }
}
