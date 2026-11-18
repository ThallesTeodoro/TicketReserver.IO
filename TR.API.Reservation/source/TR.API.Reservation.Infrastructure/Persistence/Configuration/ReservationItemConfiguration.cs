namespace TR.API.Reservation.Infrastructure.Persistence.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TR.API.Reservation.Domain.Entities;

public class ReservationItemConfiguration : IEntityTypeConfiguration<ReservationItem>
{
    public void Configure(EntityTypeBuilder<ReservationItem> builder)
    {
        builder.ToTable("reservation_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");

        builder.Property(i => i.ReservationId).HasColumnName("reservation_id").IsRequired();
        builder.Property(i => i.TicketTierId).HasColumnName("ticket_tier_id").IsRequired();
        builder.Property(i => i.SeatId).HasColumnName("seat_id");
        builder.Property(i => i.SeatLabel).HasColumnName("seat_label").HasMaxLength(50);
        builder.Property(i => i.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(i => i.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2).IsRequired();
        builder.Property(i => i.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2).IsRequired();
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
