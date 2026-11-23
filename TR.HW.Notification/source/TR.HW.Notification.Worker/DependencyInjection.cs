using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Notification.Worker.Configuration;
using TR.HW.Notification.Worker.Messaging;
using TR.HW.Notification.Worker.Workers;

namespace TR.HW.Notification.Worker;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkerServices(this IServiceCollection services, IConfiguration configuration)
    {
        var kafkaSection = configuration.GetSection(KafkaConsumerOptions.SectionName);
        services.Configure<KafkaConsumerOptions>(kafkaSection);

        var bootstrapServers = kafkaSection[nameof(KafkaConsumerOptions.BootstrapServers)]
            ?? configuration["KAFKA_BOOTSTRAP_SERVERS"]
            ?? configuration.GetConnectionString("Kafka");

        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            throw new ArgumentNullException(
                nameof(KafkaConsumerOptions.BootstrapServers),
                "Kafka BootstrapServers must be configured in application configuration.");
        }

        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
        services.AddHostedService<NotificationWorker>();

        return services;
    }
}
