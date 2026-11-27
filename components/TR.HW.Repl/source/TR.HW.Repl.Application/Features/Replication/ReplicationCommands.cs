using TR.HW.Repl.Application.Common.Messaging;
using TR.HW.Repl.Domain.Models;

namespace TR.HW.Repl.Application.Features.Replication;

public sealed record ReplicateEventChangeCommand(
    Guid EventId,
    CdcOperation Operation,
    string? Title = null,
    string? Slug = null,
    string? Description = null,
    string? Category = null,
    string? Status = null,
    int? AgeRestriction = null,
    string? BannerUrl = null,
    DateTimeOffset? SalesStartAt = null,
    DateTimeOffset? SalesEndAt = null,
    DateTimeOffset? EventStartAt = null,
    DateTimeOffset? EventEndAt = null,
    Guid? VenueId = null,
    string? VenueName = null,
    string? VenueCity = null,
    string? VenueState = null,
    string? VenueCountry = null,
    double? VenueLatitude = null,
    double? VenueLongitude = null,
    IReadOnlyCollection<string>? Tags = null,
    long SourceTimestampMs = 0
) : ICommand<ReplicationResult>;

public sealed record ReplicateTicketTierChangeCommand(
    Guid TierId,
    Guid EventId,
    CdcOperation Operation,
    string? TierName = null,
    decimal? Price = null,
    string? Status = null,
    long SourceTimestampMs = 0
) : ICommand<ReplicationResult>;

public sealed record ReplicationResult(bool Success, string? ErrorMessage = null);
