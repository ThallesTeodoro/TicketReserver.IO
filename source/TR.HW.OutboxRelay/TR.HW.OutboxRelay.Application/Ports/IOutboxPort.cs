using TR.HW.OutboxRelay.Domain.Entities;

namespace TR.HW.OutboxRelay.Application.Ports;

/// <summary>
/// Port para interação com a tabela de Outbox.
/// </summary>
public interface IOutboxPort
{
    /// <summary>
    /// Obtém as mensagens pendentes para processamento.
    /// </summary>
    Task<IEnumerable<OutboxMessage>> GetPendingMessagesAsync(int limit);

    /// <summary>
    /// Remove uma mensagem processada do banco.
    /// </summary>
    Task DeleteMessageAsync(Guid id);
}
