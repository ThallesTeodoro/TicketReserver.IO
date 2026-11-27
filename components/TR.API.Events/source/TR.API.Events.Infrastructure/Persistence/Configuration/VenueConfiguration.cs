namespace TR.API.Events.Infrastructure.Persistence.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TR.API.Events.Domain.Entities;

public sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ToTable("venues");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");

        builder.Property(v => v.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(v => v.Slug).HasColumnName("slug").HasMaxLength(160).IsRequired();
        builder.HasIndex(v => v.Slug).IsUnique();

        builder.Property(v => v.Description).HasColumnName("description");
        builder.Property(v => v.Address).HasColumnName("address").HasMaxLength(255).IsRequired();
        builder.Property(v => v.City).HasColumnName("city").HasMaxLength(100).IsRequired();
        builder.Property(v => v.State).HasColumnName("state").HasMaxLength(50).IsRequired();
        builder.Property(v => v.Country).HasColumnName("country").HasMaxLength(50).IsRequired();
        builder.Property(v => v.PostalCode).HasColumnName("postal_code").HasMaxLength(20).IsRequired();
        builder.Property(v => v.Timezone).HasColumnName("timezone").HasMaxLength(50).IsRequired();
        builder.Property(v => v.Latitude).HasColumnName("latitude").HasPrecision(10, 8);
        builder.Property(v => v.Longitude).HasColumnName("longitude").HasPrecision(11, 8);
        builder.Property(v => v.TotalCapacity).HasColumnName("total_capacity").IsRequired();
        builder.Property(v => v.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasMany(v => v.Sections)
            .WithOne()
            .HasForeignKey(s => s.VenueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(v => v.Sections)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
