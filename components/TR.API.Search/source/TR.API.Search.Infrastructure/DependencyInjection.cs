using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.API.Search.Application.Interfaces;
using TR.API.Search.Infrastructure.Configuration;
using TR.API.Search.Infrastructure.Health;
using TR.API.Search.Infrastructure.Persistence;

namespace TR.API.Search.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ElasticsearchOptions.SectionName);
        var options = new ElasticsearchOptions();
        section.Bind(options);

        var envUri = configuration["ELASTICSEARCH__URI"] ?? configuration["Elasticsearch:Uri"];
        if (!string.IsNullOrWhiteSpace(envUri))
        {
            options.Uri = envUri;
        }

        options.Validate();

        services.Configure<ElasticsearchOptions>(opt =>
        {
            opt.Uri = options.Uri;
            opt.IndexName = options.IndexName;
            opt.RequestTimeoutSeconds = options.RequestTimeoutSeconds;
            opt.RetryCount = options.RetryCount;
        });

        services.AddSingleton(sp =>
        {
            var settings = new ElasticsearchClientSettings(new Uri(options.Uri))
                .DefaultIndex(options.IndexName)
                .RequestTimeout(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));

            return new ElasticsearchClient(settings);
        });

        services.AddSingleton<IElasticsearchSearchReader, DefaultElasticsearchSearchReader>();
        services.AddScoped<IEventSearchEngine, ElasticsearchSearchEngine>();
        services.AddScoped<IElasticsearchHealthChecker, ElasticsearchHealthChecker>();

        return services;
    }
}
