namespace TR.API.Events.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Npgsql.NameTranslation;
using StackExchange.Redis;
using TR.API.Events.Domain.Contracts.Caching;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.Domain.Enums;
using TR.API.Events.Infrastructure.Caching;
using TR.API.Events.Infrastructure.Persistence;
using TR.API.Events.Infrastructure.Persistence.Repositories;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("EventsDb")
            ?? "Host=postgres;Port=5432;Database=catalog_events_db;Username=postgres;Password=postgres";

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? "redis:6379,abortConnect=false";

        var translator = new NpgsqlNullNameTranslator();
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.MapEnum<EventStatus>("event_status_enum", translator);
        dataSourceBuilder.MapEnum<TicketTierType>("ticket_tier_type_enum", translator);
        dataSourceBuilder.MapEnum<TicketTierStatus>("ticket_tier_status_enum", translator);

        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

        services.AddDbContext<EventsDbContext>(options =>
        {
            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                npgsqlOptions.MapEnum<EventStatus>("event_status_enum", nameTranslator: translator);
                npgsqlOptions.MapEnum<TicketTierType>("ticket_tier_type_enum", nameTranslator: translator);
                npgsqlOptions.MapEnum<TicketTierStatus>("ticket_tier_status_enum", nameTranslator: translator);
            });
        });

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<EventsDbContext>());
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<ICatalogReadRepository, CatalogReadRepository>();
        services.AddSingleton<DatabaseSeeder>();

        return services;
    }
}
