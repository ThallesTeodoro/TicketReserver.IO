namespace TR.HW.ResCompensator.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TR.HW.ResCompensator.Application.Interfaces;
using TR.HW.ResCompensator.Infrastructure.Caching;
using TR.HW.ResCompensator.Infrastructure.Configuration;
using TR.HW.ResCompensator.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddReservationCompensationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ReservationDatabaseOptions>(configuration.GetSection(ReservationDatabaseOptions.SectionName));
        services.Configure<RedisCacheOptions>(configuration.GetSection(RedisCacheOptions.SectionName));

        var redisConnectionString = configuration.GetSection($"{RedisCacheOptions.SectionName}:ConnectionString").Value
            ?? configuration.GetConnectionString("Redis");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString!));

        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<IReservationCompensationRepository, ReservationCompensationRepository>();
        services.AddScoped<IRedisSeatLockReleaser, RedisSeatLockReleaser>();

        return services;
    }
}
