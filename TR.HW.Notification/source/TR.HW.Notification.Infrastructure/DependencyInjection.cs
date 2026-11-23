using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TR.HW.Notification.Application.Interfaces;
using TR.HW.Notification.Infrastructure.Caching;
using TR.HW.Notification.Infrastructure.Configuration;
using TR.HW.Notification.Infrastructure.Dispatching;
using TR.HW.Notification.Infrastructure.Gateways;

namespace TR.HW.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationInfrastructureOptions>(
            configuration.GetSection(NotificationInfrastructureOptions.SectionName));

        var redisConn = configuration.GetSection(NotificationInfrastructureOptions.SectionName)["RedisConnectionString"]
            ?? configuration.GetConnectionString("Redis")
            ?? "localhost:6379,abortConnect=false";

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

        services.AddScoped<INotificationInboxStore, RedisNotificationInboxStore>();
        services.AddScoped<INotificationProviderGateway, SimulatedNotificationProviderGateway>();
        services.AddScoped<INotificationDispatcher, ResilientNotificationDispatcher>();

        return services;
    }
}
