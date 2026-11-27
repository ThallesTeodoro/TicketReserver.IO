namespace TR.HW.ResTimeout.Persistence;

public sealed record ReservationSeatItemData(
    Guid SeatId,
    string? SeatLabel);

public sealed record ReservationTimeoutData(
    Guid Id,
    string Status,
    Guid EventId,
    Guid CustomerId,
    IReadOnlyList<ReservationSeatItemData> Items);
