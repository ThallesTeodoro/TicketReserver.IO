namespace TR.HW.ReservationOutboxRelay;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TR.HW.ReservationOutboxRelay.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddRelayInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ReservationDb")
            ?? configuration.GetSection($"{OutboxRelayOptions.SectionName}:ConnectionString").Value;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentNullException(
                nameof(connectionString),
                "A connection string para o banco de reservas ('ConnectionStrings:ReservationDb' ou 'OutboxRelay:ConnectionString') não foi fornecida.");
        }

        var kafkaBootstrapServers = configuration.GetSection($"{KafkaProducerOptions.SectionName}:BootstrapServers").Value;
        if (string.IsNullOrWhiteSpace(kafkaBootstrapServers))
        {
            throw new ArgumentNullException(
                nameof(kafkaBootstrapServers),
                "O endereço do broker Kafka ('Kafka:BootstrapServers') não foi fornecido.");
        }

        services.Configure<OutboxRelayOptions>(options =>
        {
            configuration.GetSection(OutboxRelayOptions.SectionName).Bind(options);
            options.ConnectionString = connectionString;
        });

        services.Configure<KafkaProducerOptions>(configuration.GetSection(KafkaProducerOptions.SectionName));

        services.AddSingleton<TR.HW.ReservationOutboxRelay.Persistence.IDbConnectionFactory, TR.HW.ReservationOutboxRelay.Persistence.NpgsqlConnectionFactory>();
        services.AddScoped<TR.HW.ReservationOutboxRelay.Persistence.IOutboxRepository, TR.HW.ReservationOutboxRelay.Persistence.OutboxRepository>();
        services.AddSingleton<TR.HW.ReservationOutboxRelay.Messaging.IKafkaProducer, TR.HW.ReservationOutboxRelay.Messaging.KafkaProducer>();

        return services;
    }
}
