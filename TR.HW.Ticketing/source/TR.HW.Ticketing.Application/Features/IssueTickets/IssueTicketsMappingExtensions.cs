namespace TR.HW.Ticketing.Application.Features.IssueTickets;

using TR.HW.Ticketing.Domain.Entities;

public static class IssueTicketsMappingExtensions
{
    public static IssuedTicketSummary ToSummary(this Ticket ticket)
    {
        return new IssuedTicketSummary(
            TicketId: ticket.Id,
            TicketCode: ticket.TicketCode,
            QrCodeHash: ticket.QrCodeHash,
            SeatId: ticket.SeatId,
            SeatLabel: ticket.SeatLabel,
            PricePaid: ticket.PricePaid);
    }

    public static IssueTicketsResult ToIssueTicketsResult(this IReadOnlyList<Ticket> tickets)
    {
        var summaries = tickets.Select(t => t.ToSummary()).ToList();
        return new IssueTicketsResult(
            IsSuccess: true,
            Status: "ISSUED",
            IssuedTickets: summaries,
            FailureReason: null);
    }
}
