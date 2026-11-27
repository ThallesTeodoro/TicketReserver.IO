namespace TR.HW.Payment.Worker;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Payment.Application;
using TR.HW.Payment.Infrastructure;
using TR.HW.Payment.Infrastructure.Configuration;
using TR.HW.Payment.Worker.Configuration;
using TR.HW.Payment.Worker.Messaging;
using TR.HW.Payment.Worker.Workers;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentWorkerServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Fail-fast configuration validation
        var dbConnectionString = configuration.GetSection($"{PaymentDatabaseOptions.SectionName}:ConnectionString").Value
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

        // Configure options
        services.Configure<KafkaConsumerOptions>(configuration.GetSection(KafkaConsumerOptions.SectionName));

        // Register layers
        services.AddPaymentApplication();
        services.AddPaymentInfrastructure(configuration);

        // Register worker messaging and hosted service
        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
        services.AddHostedService<PaymentCheckoutWorker>();

        return services;
    }
}
