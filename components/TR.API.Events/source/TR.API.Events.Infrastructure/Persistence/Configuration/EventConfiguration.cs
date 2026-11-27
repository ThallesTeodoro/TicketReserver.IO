namespace TR.API.Events.Infrastructure.Persistence.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TR.API.Events.Domain.Entities;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.VenueId).HasColumnName("venue_id").IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Slug).HasColumnName("slug").HasMaxLength(220).IsRequired();
        builder.HasIndex(e => e.Slug).IsUnique();

        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.Category).HasColumnName("category").HasMaxLength(60).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.SalesStartAt).HasColumnName("sales_start_at").IsRequired();
        builder.Property(e => e.SalesEndAt).HasColumnName("sales_end_at").IsRequired();
        builder.Property(e => e.EventStartAt).HasColumnName("event_start_at").IsRequired();
        builder.Property(e => e.EventEndAt).HasColumnName("event_end_at").IsRequired();
        builder.Property(e => e.AgeRestriction).HasColumnName("age_restriction").IsRequired();
        builder.Property(e => e.BannerUrl).HasColumnName("banner_url").HasMaxLength(500);
        builder.Property(e => e.MaxTicketsPerUser).HasColumnName("max_tickets_per_user").IsRequired();
        builder.Property(e => e.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasMany(e => e.TicketTiers)
            .WithOne()
            .HasForeignKey(t => t.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.TicketTiers)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
