namespace TR.API.Reservation.Domain.Constants;

public static class RedisKeys
{
    /// <summary>
    /// Prefixo para chaves de idempotência de reservas.
    /// Padrão: idempotency:reservation:{key}
    /// </summary>
    public const string IdempotencyPrefix = "idempotency:reservation:";
}
