using System.Diagnostics;

namespace TR.HW.OutboxRelay.Domain;

public static class Telemetry
{
    public const string ServiceName = "TR.HW.OutboxRelay";
    public static readonly ActivitySource ActivitySource = new(ServiceName);
}
