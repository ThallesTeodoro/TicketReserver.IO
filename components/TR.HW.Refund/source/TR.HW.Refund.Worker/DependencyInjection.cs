namespace TR.HW.Refund.Worker;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Refund.Application;
using TR.HW.Refund.Infrastructure;
using TR.HW.Refund.Infrastructure.Configuration;
using TR.HW.Refund.Worker.Configuration;
using TR.HW.Refund.Worker.Messaging;
using TR.HW.Refund.Worker.Workers;

public static class DependencyInjection
{
    public static IServiceCollection AddRefundWorkerServices(this IServiceCollection services, IConfiguration configuration)
    {
        var dbConnectionString = configuration.GetSection($"{RefundDatabaseOptions.SectionName}:ConnectionString").Value
            ?? configuration.GetConnectionString("PaymentDb");

        if (string.IsNullOrWhiteSpace(dbConnectionString))
        {
            throw new ArgumentNullException(
                nameof(configuration),
                "Database:ConnectionString or ConnectionStrings:PaymentDb configuration is required and cannot be empty.");
        }

        var kafkaBootstrap = configuration.GetSection($"{KafkaProducerOptions.SectionName}:BootstrapServers").Value
            ?? configuration.GetSection($"{KafkaConsumerOptions.SectionName}:BootstrapServers").Value;

        if (string.IsNullOrWhiteSpace(kafkaBootstrap))
        {
            throw new ArgumentNullException(
                nameof(configuration),
                "Kafka:BootstrapServers configuration is required and cannot be empty.");
        }

        services.Configure<KafkaConsumerOptions>(configuration.GetSection(KafkaConsumerOptions.SectionName));

        services.AddRefundApplication();
        services.AddRefundInfrastructure(configuration);

        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
        services.AddHostedService<PaymentRefundWorker>();

        return services;
    }
}
