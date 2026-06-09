using TR.HW.OutboxRelay.Domain.Entities;

namespace TR.HW.OutboxRelay.Application.Ports;

/// <summary>
/// Port para interação com a tabela de Outbox.
/// </summary>
public interface IOutboxPort
{
    /// <summary>
    /// Obtém as mensagens elegíveis para processamento e realiza o lock (transição para EmProcessamento).
    /// </summary>
    /// <param name="limit">Quantidade máxima de mensagens.</param>
    /// <param name="timeoutLimit">Data limite para considerar uma mensagem em processamento como 'travada'.</param>
    /// <returns>Coleção de mensagens bloqueadas para este worker.</returns>
    Task<IEnumerable<OutboxMessage>> GetAndLockEligibleMessagesAsync(int limit, DateTime timeoutLimit);

    /// <summary>
    /// Marca a mensagem como Processada.
    /// </summary>
    Task MarkAsProcessedAsync(Guid id);

    /// <summary>
    /// Marca falha no processamento, incrementando tentativas e podendo marcar como ErroPermanente.
    /// </summary>
    Task MarkAsFailedAsync(Guid id, string errorReason, bool permanent);
}
