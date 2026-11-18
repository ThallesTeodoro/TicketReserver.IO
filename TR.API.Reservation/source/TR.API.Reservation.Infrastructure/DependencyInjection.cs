namespace TR.API.Reservation.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Npgsql.NameTranslation;
using StackExchange.Redis;
using TR.API.Reservation.Domain.Enums;
using TR.API.Reservation.Infrastructure.Caching;
using TR.API.Reservation.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ReservationDb")
            ?? throw new ArgumentNullException(nameof(configuration), "A connection string 'ReservationDb' não foi configurada.");

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new ArgumentNullException(nameof(configuration), "A connection string 'Redis' não foi configurada.");

        var translator = new NpgsqlNullNameTranslator();
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.MapEnum<ReservationStatus>("reservation_status_enum", translator);
        dataSourceBuilder.MapEnum<OutboxStatus>("outbox_status_enum", translator);

        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

        services.AddDbContext<ReservationDbContext>(options =>
        {
            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                npgsqlOptions.MapEnum<ReservationStatus>("reservation_status_enum", nameTranslator: translator);
                npgsqlOptions.MapEnum<OutboxStatus>("outbox_status_enum", nameTranslator: translator);
            });
        });

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddSingleton<IRedisHoldService, RedisHoldService>();

        return services;
    }
}
