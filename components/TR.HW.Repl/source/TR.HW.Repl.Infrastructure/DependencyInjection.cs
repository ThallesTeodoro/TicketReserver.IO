using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Repl.Application.Interfaces;
using TR.HW.Repl.Infrastructure.Configuration;
using TR.HW.Repl.Infrastructure.Persistence;

namespace TR.HW.Repl.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ElasticsearchOptions>(
            configuration.GetSection(ElasticsearchOptions.SectionName));

        var esUri = configuration.GetSection(ElasticsearchOptions.SectionName)["Uri"]
            ?? configuration["ELASTICSEARCH_URL"]
            ?? "http://localhost:9200";

        var settings = new ElasticsearchClientSettings(new Uri(esUri))
            .DefaultIndex("events_search");

        services.AddSingleton(new ElasticsearchClient(settings));
        services.AddScoped<IElasticsearchEngine, DefaultElasticsearchEngine>();
        services.AddScoped<IEventSearchRepository, ElasticsearchEventRepository>();

        return services;
    }
}
