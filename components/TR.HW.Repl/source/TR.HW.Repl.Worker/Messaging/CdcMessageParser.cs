using System.Globalization;
using System.Text.Json;
using TR.HW.Repl.Application.Features.Replication;
using TR.HW.Repl.Domain.Models;

namespace TR.HW.Repl.Worker.Messaging;

public static class CdcMessageParser
{
    public static (CdcOperation Operation, JsonElement Data, long TimestampMs) ExtractCdcPayload(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var payloadElement = root.TryGetProperty("payload", out var p) ? p : root;

        var opStr = payloadElement.TryGetProperty("op", out var opProp) ? opProp.GetString() : "c";
        var op = opStr?.ToLowerInvariant() switch
        {
            "c" => CdcOperation.Create,
            "u" => CdcOperation.Update,
            "d" => CdcOperation.Delete,
            "r" => CdcOperation.Read,
            _ => CdcOperation.Create
        };

        long tsMs = 0;
        if (payloadElement.TryGetProperty("ts_ms", out var tsProp))
        {
            if (tsProp.ValueKind == JsonValueKind.Number)
            {
                tsMs = tsProp.GetInt64();
            }
        }

        JsonElement dataElement;
        if (op == CdcOperation.Delete)
        {
            if (payloadElement.TryGetProperty("before", out var before) && before.ValueKind == JsonValueKind.Object)
            {
                dataElement = before.Clone();
            }
            else
            {
                dataElement = payloadElement.Clone();
            }
        }
        else
        {
            if (payloadElement.TryGetProperty("after", out var after) && after.ValueKind == JsonValueKind.Object)
            {
                dataElement = after.Clone();
            }
            else
            {
                dataElement = payloadElement.Clone();
            }
        }

        return (op, dataElement, tsMs);
    }

    public static ReplicateEventChangeCommand ParseEventCommand(string json, string? key = null)
    {
        var (op, data, tsMs) = ExtractCdcPayload(json);

        var id = GetGuidProperty(data, "id")
            ?? (Guid.TryParse(key, out var keyGuid) ? keyGuid : Guid.Empty);

        var title = GetStringProperty(data, "title");
        var slug = GetStringProperty(data, "slug");
        var description = GetStringProperty(data, "description");
        var category = GetStringProperty(data, "category");
        var status = GetStringProperty(data, "status");
        var ageRestriction = GetIntProperty(data, "age_restriction", "ageRestriction");
        var bannerUrl = GetStringProperty(data, "banner_url", "bannerUrl");
        var salesStartAt = GetDateTimeOffsetProperty(data, "sales_start_at", "salesStartAt");
        var salesEndAt = GetDateTimeOffsetProperty(data, "sales_end_at", "salesEndAt");
        var eventStartAt = GetDateTimeOffsetProperty(data, "event_start_at", "eventStartAt");
        var eventEndAt = GetDateTimeOffsetProperty(data, "event_end_at", "eventEndAt");
        var venueId = GetGuidProperty(data, "venue_id", "venueId");
        var venueName = GetStringProperty(data, "venue_name", "venueName");
        var venueCity = GetStringProperty(data, "venue_city", "venueCity");
        var venueState = GetStringProperty(data, "venue_state", "venueState");
        var venueCountry = GetStringProperty(data, "venue_country", "venueCountry");
        var venueLat = GetDoubleProperty(data, "venue_latitude", "venueLatitude", "latitude");
        var venueLon = GetDoubleProperty(data, "venue_longitude", "venueLongitude", "longitude");
        var tags = GetStringArrayProperty(data, "tags");

        return new ReplicateEventChangeCommand(
            EventId: id,
            Operation: op,
            Title: title,
            Slug: slug,
            Description: description,
            Category: category,
            Status: status,
            AgeRestriction: ageRestriction,
            BannerUrl: bannerUrl,
            SalesStartAt: salesStartAt,
            SalesEndAt: salesEndAt,
            EventStartAt: eventStartAt,
            EventEndAt: eventEndAt,
            VenueId: venueId,
            VenueName: venueName,
            VenueCity: venueCity,
            VenueState: venueState,
            VenueCountry: venueCountry,
            VenueLatitude: venueLat,
            VenueLongitude: venueLon,
            Tags: tags,
            SourceTimestampMs: tsMs
        );
    }

    public static ReplicateTicketTierChangeCommand ParseTicketTierCommand(string json, string? key = null)
    {
        var (op, data, tsMs) = ExtractCdcPayload(json);

        var tierId = GetGuidProperty(data, "id")
            ?? (Guid.TryParse(key, out var keyGuid) ? keyGuid : Guid.Empty);

        var eventId = GetGuidProperty(data, "event_id", "eventId") ?? Guid.Empty;
        var tierName = GetStringProperty(data, "name", "tier_name", "tierName");
        var price = GetDecimalProperty(data, "price");
        var status = GetStringProperty(data, "status");

        return new ReplicateTicketTierChangeCommand(
            TierId: tierId,
            EventId: eventId,
            Operation: op,
            TierName: tierName,
            Price: price,
            Status: status,
            SourceTimestampMs: tsMs
        );
    }

    private static string? GetStringProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            if (element.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
    }

    private static Guid? GetGuidProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            if (element.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                if (Guid.TryParse(prop.GetString(), out var guid))
                {
                    return guid;
                }
            }
        }
        return null;
    }

    private static int? GetIntProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var val))
                    return val;
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var valStr))
                    return valStr;
            }
        }
        return null;
    }

    private static double? GetDoubleProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var val))
                    return val;
                if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var valStr))
                    return valStr;
            }
        }
        return null;
    }

    private static decimal? GetDecimalProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var val))
                    return val;
                if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var valStr))
                    return valStr;
            }
        }
        return null;
    }

    private static DateTimeOffset? GetDateTimeOffsetProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(prop.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return dt;
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var microOrMillis))
                {
                    // Postgres microseconds or millis
                    return microOrMillis > 10_000_000_000_000
                        ? DateTimeOffset.FromUnixTimeMilliseconds(microOrMillis / 1000)
                        : DateTimeOffset.FromUnixTimeMilliseconds(microOrMillis);
                }
            }
        }
        return null;
    }

    private static IReadOnlyCollection<string>? GetStringArrayProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var propName in propertyNames)
        {
            if (element.TryGetProperty(propName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var item in prop.EnumerateArray())
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            list.Add(s);
                        }
                    }
                    return list;
                }
                if (prop.ValueKind == JsonValueKind.String)
                {
                    var str = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(str))
                    {
                        // Could be comma-separated or json string array
                        if (str.StartsWith("[") && str.EndsWith("]"))
                        {
                            try
                            {
                                using var innerDoc = JsonDocument.Parse(str);
                                return innerDoc.RootElement.EnumerateArray()
                                    .Select(x => x.GetString())
                                    .Where(x => !string.IsNullOrWhiteSpace(x))
                                    .Select(x => x!)
                                    .ToList();
                            }
                            catch
                            {
                                // fallback
                            }
                        }
                        return str.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    }
                }
            }
        }
        return null;
    }
}
