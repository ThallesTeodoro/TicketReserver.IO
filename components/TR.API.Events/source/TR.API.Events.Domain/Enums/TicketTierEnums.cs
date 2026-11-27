namespace TR.API.Events.Domain.Enums;

public enum TicketTierType
{
    STANDARD,
    HALF_PRICE,
    VIP_EXPERIENCE,
    EARLY_BIRD,
    SPONSOR
}

public enum TicketTierStatus
{
    SCHEDULED,
    ACTIVE,
    SOLD_OUT,
    EXPIRED
}
