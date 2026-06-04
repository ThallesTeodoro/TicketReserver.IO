using TR.API.Reservation.Domain.Enums;
using TR.API.Reservation.Domain.Exceptions;

namespace TR.API.Reservation.Domain.Entities;

/// <summary>
/// Entidade que representa uma reserva de ingresso para um evento.
/// </summary>
public class Reservation
{
    /// <summary>
    /// Identificador único da reserva.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Identificador do usuário que realizou a reserva.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Identificador do evento para o qual o ingresso foi reservado.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    /// Número ou identificação do assento reservado.
    /// </summary>
    public string SeatNumber { get; private set; }

    /// <summary>
    /// Preço do ingresso no momento da reserva.
    /// </summary>
    public decimal Price { get; private set; }

    /// <summary>
    /// Status atual da reserva (Pendente, Confirmada, Cancelada).
    /// </summary>
    public ReservationStatus Status { get; private set; }

    /// <summary>
    /// Data e hora de criação da reserva (UTC).
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    // Construtor privado para ORM/Dapper
    private Reservation() 
    { 
        SeatNumber = null!;
    }

    public Reservation(Guid userId, Guid eventId, string seatNumber, decimal price)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("UserId cannot be empty.");

        if (eventId == Guid.Empty)
            throw new ValidationException("EventId cannot be empty.");

        if (string.IsNullOrWhiteSpace(seatNumber))
            throw new ValidationException("SeatNumber cannot be null or empty.");

        if (price <= 0)
            throw new ValidationException("Price must be greater than zero.");

        Id = Guid.NewGuid();
        UserId = userId;
        EventId = eventId;
        SeatNumber = seatNumber;
        Price = price;
        Status = ReservationStatus.Pendente;
        CreatedAt = DateTime.UtcNow;
    }

    public void Confirm()
    {
        if (Status != ReservationStatus.Pendente)
            throw new BusinessException($"Cannot confirm reservation in {Status} status.");

        Status = ReservationStatus.Confirmada;
    }

    public void Cancel()
    {
        if (Status == ReservationStatus.Cancelada)
            throw new BusinessException("Reservation is already cancelled.");

        Status = ReservationStatus.Cancelada;
    }
}
