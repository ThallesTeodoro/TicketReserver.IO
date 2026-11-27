namespace TR.HW.ResCompensator.Infrastructure.Configuration;

public sealed class ReservationDatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public sealed class RedisCacheOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;
}
