namespace TR.API.Reservation.Application.Ports;

/// <summary>
/// Port de saída para verificação de idempotência.
/// </summary>
public interface IIdempotencyPort
{
    /// <summary>
    /// Verifica se uma requisição é idempotente com base em uma chave e tempo de vida (TTL).
    /// </summary>
    /// <param name="key">Chave única de idempotência.</param>
    /// <param name="ttl">Tempo de vida da chave no cache.</param>
    /// <returns>True se a chave foi registrada com sucesso (primeiro processamento); False caso contrário.</returns>
    Task<bool> IsIdempotentAsync(string key, TimeSpan ttl);
}
