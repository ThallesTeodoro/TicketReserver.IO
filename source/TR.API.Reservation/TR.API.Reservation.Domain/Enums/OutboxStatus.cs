namespace TR.API.Reservation.Domain.Enums;

/// <summary>
/// Representa os possíveis estados de uma mensagem na tabela de Outbox.
/// </summary>
public enum OutboxStatus : short
{
    Pendente = 1,
    EmProcessamento = 2,
    Processado = 3,
    ErroPermanente = 4
}
