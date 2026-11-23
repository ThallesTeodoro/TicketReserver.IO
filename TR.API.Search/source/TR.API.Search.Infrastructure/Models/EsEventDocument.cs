using System.Text.Json.Serialization;

namespace TR.API.Search.Infrastructure.Models;

public sealed class EsEventDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("age_restriction")]
    public int AgeRestriction { get; set; }

    [JsonPropertyName("banner_url")]
    public string? BannerUrl { get; set; }

    [JsonPropertyName("schedule")]
    public EsSchedule? Schedule { get; set; }

    [JsonPropertyName("venue")]
    public EsVenue? Venue { get; set; }

    [JsonPropertyName("pricing")]
    public EsPricing? Pricing { get; set; }

    [JsonPropertyName("tiers_summary")]
    public List<EsTicketTier>? TiersSummary { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class EsSchedule
{
    [JsonPropertyName("sales_start_at")]
    public DateTimeOffset SalesStartAt { get; set; }

    [JsonPropertyName("sales_end_at")]
    public DateTimeOffset SalesEndAt { get; set; }

    [JsonPropertyName("event_start_at")]
    public DateTimeOffset EventStartAt { get; set; }

    [JsonPropertyName("event_end_at")]
    public DateTimeOffset EventEndAt { get; set; }
}

public sealed class EsVenue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public EsGeoLocation? Location { get; set; }
}

public sealed class EsGeoLocation
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }
}

public sealed class EsPricing
{
    [JsonPropertyName("min_price")]
    public decimal MinPrice { get; set; }

    [JsonPropertyName("max_price")]
    public decimal MaxPrice { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "BRL";
}

public sealed class EsTicketTier
{
    [JsonPropertyName("tier_id")]
    public string TierId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public sealed record EsSearchResult<T>(
    IReadOnlyCollection<EsSearchHit<T>> Hits,
    long TotalCount,
    IReadOnlyDictionary<string, long> CategoryFacets,
    IReadOnlyDictionary<string, long> CityFacets);

public sealed record EsSearchHit<T>(
    T Source,
    double? Score = null,
    double? DistanceKm = null);

public sealed record EsAutocompleteItem(
    string Id,
    string Title,
    string Category);
