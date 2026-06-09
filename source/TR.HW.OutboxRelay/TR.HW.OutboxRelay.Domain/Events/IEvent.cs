namespace TR.HW.OutboxRelay.Domain.Events;

/// <summary>
/// Interface base para todos os eventos de domínio que podem ser persistidos no Outbox.
/// </summary>
public interface IEvent
{
    /// <summary>
    /// Identificador único da ocorrência deste evento.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Data e hora em que o evento ocorreu.
    /// </summary>
    DateTime OccurredAt { get; }
}
