using TR.API.Search.Application.Common.Messaging;
using TR.API.Search.Application.Features.Events.Models;

namespace TR.API.Search.Application.Features.Events.GetEventDetails;

public sealed record GetEventByIdQuery(Guid EventId) : IQuery<EventSearchItemDto?>;
