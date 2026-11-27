namespace TR.HW.Refund.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Refund.Application.Interfaces;
using TR.HW.Refund.Infrastructure.Configuration;
using TR.HW.Refund.Infrastructure.Gateways;
using TR.HW.Refund.Infrastructure.Messaging;
using TR.HW.Refund.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddRefundInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RefundDatabaseOptions>(configuration.GetSection(RefundDatabaseOptions.SectionName));
        services.Configure<KafkaProducerOptions>(configuration.GetSection(KafkaProducerOptions.SectionName));
        services.Configure<RefundGatewayOptions>(configuration.GetSection(RefundGatewayOptions.SectionName));

        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<IRefundRepository, RefundRepository>();
        services.AddScoped<IRefundGatewayService, RefundGatewayService>();
        services.AddSingleton<IKafkaRefundProducer, KafkaRefundProducer>();

        return services;
    }
}
