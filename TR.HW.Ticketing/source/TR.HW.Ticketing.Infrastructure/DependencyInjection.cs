namespace TR.HW.Ticketing.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Ticketing.Application.Interfaces;
using TR.HW.Ticketing.Domain.Services;
using TR.HW.Ticketing.Infrastructure.Configuration;
using TR.HW.Ticketing.Infrastructure.Cryptography;
using TR.HW.Ticketing.Infrastructure.Messaging;
using TR.HW.Ticketing.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddTicketingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TicketingDatabaseOptions>(configuration.GetSection(TicketingDatabaseOptions.SectionName));
        services.Configure<KafkaProducerOptions>(configuration.GetSection(KafkaProducerOptions.SectionName));
        services.Configure<TicketingSecurityOptions>(configuration.GetSection(TicketingSecurityOptions.SectionName));

        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<ITicketingRepository, TicketingRepository>();
        services.AddSingleton<IQrCodeGenerator, HmacSha256QrCodeGenerator>();
        services.AddSingleton<IKafkaTicketingProducer, KafkaTicketingProducer>();

        return services;
    }
}
