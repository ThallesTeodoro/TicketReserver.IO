namespace TR.HW.OutboxRelay.Application.Ports;

/// <summary>
/// Port para envio de mensagens ao Kafka.
/// </summary>
public interface IKafkaPort
{
    /// <summary>
    /// Publica uma mensagem no tópico especificado.
    /// </summary>
    Task PublishAsync(string topic, string key, string payload, string correlationId);
}
