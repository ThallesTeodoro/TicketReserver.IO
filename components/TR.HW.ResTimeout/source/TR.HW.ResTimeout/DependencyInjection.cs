namespace TR.HW.ResTimeout;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TR.HW.ResTimeout.Caching;
using TR.HW.ResTimeout.Configuration;
using TR.HW.ResTimeout.Messaging;
using TR.HW.ResTimeout.Persistence;
using TR.HW.ResTimeout.Workers;

public static class DependencyInjection
{
    public static IServiceCollection AddTimeoutInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbConnectionString = configuration.GetConnectionString("ReservationDb")
            ?? configuration.GetSection($"{DatabaseOptions.SectionName}:ConnectionString").Value;

        if (string.IsNullOrWhiteSpace(dbConnectionString))
        {
            throw new ArgumentNullException(
                nameof(dbConnectionString),
                "A connection string para o banco de reservas ('ConnectionStrings:ReservationDb' ou 'Database:ConnectionString') não foi fornecida.");
        }

        var kafkaBootstrapServers = configuration.GetSection($"{KafkaConsumerOptions.SectionName}:BootstrapServers").Value;
        if (string.IsNullOrWhiteSpace(kafkaBootstrapServers))
        {
            throw new ArgumentNullException(
                nameof(kafkaBootstrapServers),
                "O endereço do broker Kafka ('Kafka:BootstrapServers') não foi fornecido.");
        }

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? configuration.GetSection($"{RedisOptions.SectionName}:ConnectionString").Value;

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new ArgumentNullException(
                nameof(redisConnectionString),
                "A connection string do Redis ('ConnectionStrings:Redis' ou 'Redis:ConnectionString') não foi fornecida.");
        }

        services.Configure<DatabaseOptions>(options =>
        {
            configuration.GetSection(DatabaseOptions.SectionName).Bind(options);
            options.ConnectionString = dbConnectionString;
        });

        services.Configure<KafkaConsumerOptions>(configuration.GetSection(KafkaConsumerOptions.SectionName));

        services.Configure<RedisOptions>(options =>
        {
            configuration.GetSection(RedisOptions.SectionName).Bind(options);
            options.ConnectionString = redisConnectionString;
        });

        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<IReservationTimeoutRepository, ReservationTimeoutRepository>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var configOptions = ConfigurationOptions.Parse(redisConnectionString);
            configOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(configOptions);
        });

        services.AddSingleton<ISeatLockManager, SeatLockManager>();
        services.AddSingleton<IKafkaConsumer, KafkaConsumer>();

        services.AddHostedService<ReservationTimeoutWorker>();

        return services;
    }
}
