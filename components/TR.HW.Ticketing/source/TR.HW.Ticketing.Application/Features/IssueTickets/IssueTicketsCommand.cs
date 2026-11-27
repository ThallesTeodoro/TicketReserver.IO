namespace TR.HW.Ticketing.Application.Features.IssueTickets;

using TR.HW.Ticketing.Application.Common.Messaging;

public sealed record TicketIssuanceItemDto(
    Guid TicketTierId,
    Guid? SeatId,
    string? SeatLabel,
    decimal UnitPrice);

public sealed record IssueTicketsCommand(
    Guid ReservationId,
    Guid PaymentId,
    Guid CustomerId,
    Guid EventId,
    IReadOnlyList<TicketIssuanceItemDto> Items,
    string? MessageId = null,
    string? Traceparent = null) : ICommand<IssueTicketsResult>;

public sealed record IssuedTicketSummary(
    Guid TicketId,
    string TicketCode,
    string QrCodeHash,
    Guid? SeatId,
    string? SeatLabel,
    decimal PricePaid);

public sealed record IssueTicketsResult(
    bool IsSuccess,
    string Status,
    IReadOnlyList<IssuedTicketSummary> IssuedTickets,
    string? FailureReason = null);
