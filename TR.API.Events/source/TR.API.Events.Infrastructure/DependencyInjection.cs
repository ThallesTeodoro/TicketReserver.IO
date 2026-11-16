namespace TR.API.Events.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TR.API.Events.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("EventsDb")
            ?? "Host=postgres;Port=5432;Database=catalog_events_db;Username=postgres;Password=postgres";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

        services.AddDbContext<EventsDbContext>(options =>
        {
            options.UseNpgsql(dataSource);
        });

        return services;
    }
}
