namespace TR.HW.Repl.Infrastructure.Configuration;

public sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    public string Uri { get; set; } = "http://localhost:9200";
    public string IndexName { get; set; } = "events_search";
    public int RetryCount { get; set; } = 3;
}
