namespace TR.API.Reservation.Infrastructure.Persistence.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TR.API.Reservation.Domain.Entities;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(r => r.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").IsRequired();
        builder.Property(r => r.TotalAmount).HasColumnName("total_amount").HasPrecision(12, 2).IsRequired();
        builder.Property(r => r.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(r => r.HoldExpiresAt).HasColumnName("hold_expires_at").IsRequired();
        builder.Property(r => r.PaymentAttemptedAt).HasColumnName("payment_attempted_at");
        builder.Property(r => r.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(r => r.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(r => r.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(150);
        builder.Property(r => r.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasMany(r => r.Items)
            .WithOne()
            .HasForeignKey(i => i.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
