namespace TR.API.Search.Infrastructure.Configuration;

public sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    public string Uri { get; set; } = "http://localhost:9200";
    public string IndexName { get; set; } = "events_search";
    public int RequestTimeoutSeconds { get; set; } = 10;
    public int RetryCount { get; set; } = 3;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Uri))
        {
            throw new ArgumentNullException(nameof(Uri), "Elasticsearch URI must be configured.");
        }

        if (string.IsNullOrWhiteSpace(IndexName))
        {
            throw new ArgumentNullException(nameof(IndexName), "Elasticsearch IndexName must be configured.");
        }
    }
}
