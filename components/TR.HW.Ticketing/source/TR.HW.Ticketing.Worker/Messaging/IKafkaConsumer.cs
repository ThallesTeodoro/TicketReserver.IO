namespace TR.HW.Ticketing.Worker.Messaging;

using Confluent.Kafka;

public interface IKafkaConsumer : IDisposable
{
    void Subscribe();
    ConsumeResult<string, string>? Consume(TimeSpan timeout);
    void Commit(ConsumeResult<string, string> result);
    Task SendToDlqAsync(string key, string value, string reason, Headers? headers = null, CancellationToken ct = default);
    void Close();
}
