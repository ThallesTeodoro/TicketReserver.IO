namespace TR.HW.ResCompensator.Worker;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.ResCompensator.Application;
using TR.HW.ResCompensator.Infrastructure;
using TR.HW.ResCompensator.Infrastructure.Configuration;
using TR.HW.ResCompensator.Worker.Configuration;
using TR.HW.ResCompensator.Worker.Messaging;
using TR.HW.ResCompensator.Worker.Workers;

public static class DependencyInjection
{
    public static IServiceCollection AddReservationCompensatorWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbConnectionString = configuration.GetSection($"{ReservationDatabaseOptions.SectionName}:ConnectionString").Value
            ?? configuration.GetConnectionString("ReservationDb");

        if (string.IsNullOrWhiteSpace(dbConnectionString))
        {
            throw new ArgumentNullException(
                nameof(configuration),
                "Database:ConnectionString or ConnectionStrings:ReservationDb configuration is required and cannot be empty.");
        }

        var redisConnectionString = configuration.GetSection($"{RedisCacheOptions.SectionName}:ConnectionString").Value
            ?? configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new ArgumentNullException(
                nameof(configuration),
                "Redis:ConnectionString or ConnectionStrings:Redis configuration is required and cannot be empty.");
        }

        var kafkaBootstrap = configuration.GetSection($"{KafkaConsumerOptions.SectionName}:BootstrapServers").Value
            ?? configuration.GetSection("Kafka:BootstrapServers").Value;

        if (string.IsNullOrWhiteSpace(kafkaBootstrap))
        {
            throw new ArgumentNullException(
                nameof(configuration),
                "KafkaConsumer:BootstrapServers configuration is required and cannot be empty.");
        }

        services.Configure<KafkaConsumerOptions>(configuration.GetSection(KafkaConsumerOptions.SectionName));

        services.AddReservationCompensationApplication();
        services.AddReservationCompensationInfrastructure(configuration);

        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();
        services.AddHostedService<ReservationCompensationWorker>();

        return services;
    }
}
