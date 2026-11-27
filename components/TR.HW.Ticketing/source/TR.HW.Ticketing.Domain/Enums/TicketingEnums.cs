namespace TR.HW.Ticketing.Domain.Enums;

public enum TicketStatus
{
    Active = 1,
    Used = 2,
    Revoked = 3,
    Blocked = 4
}

public enum AllocationType
{
    NumberedSeat = 1,
    GeneralAdmission = 2
}

public enum CheckinStatus
{
    Approved = 1,
    AlreadyUsed = 2,
    RevokedTicket = 3,
    DeviceUnauthorized = 4
}
