namespace TR.HW.Ticketing.Worker;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Ticketing.Application;
using TR.HW.Ticketing.Infrastructure;
using TR.HW.Ticketing.Infrastructure.Configuration;
using TR.HW.Ticketing.Worker.Configuration;
using TR.HW.Ticketing.Worker.Messaging;
using TR.HW.Ticketing.Worker.Workers;

public static class DependencyInjection
{
    public static IServiceCollection AddTicketingWorkerServices(this IServiceCollection services, IConfiguration configuration)
    {
        var dbConnectionString = configuration.GetSection($"{TicketingDatabaseOptions.SectionName}:ConnectionString").Value
            ?? configuration.GetConnectionString("TicketingDb");

        if (string.IsNullOrWhiteSpace(dbConnectionString))
        {
            throw new ArgumentNullException(
                nameof(configuration),
                "Database:ConnectionString or ConnectionStrings:TicketingDb configuration is required and cannot be empty.");
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

        services.AddTicketingApplication();
        services.AddTicketingInfrastructure(configuration);

        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
        services.AddHostedService<TicketIssuanceWorker>();

        return services;
    }
}
