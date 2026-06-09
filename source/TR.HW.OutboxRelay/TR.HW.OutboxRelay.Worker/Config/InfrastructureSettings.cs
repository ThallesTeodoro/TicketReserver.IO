namespace TR.HW.OutboxRelay.Worker.Config;

/// <summary>
/// Configurações de infraestrutura para o Worker.
/// </summary>
public class InfrastructureSettings
{
    public const string SectionName = "Infrastructure";

    /// <summary>
    /// String de conexão com o banco de dados PostgreSQL.
    /// </summary>
    public string PostgresConnection { get; set; } = string.Empty;

    /// <summary>
    /// String de conexão com o servidor Redis.
    /// </summary>
    public string RedisConnection { get; set; } = string.Empty;

    /// <summary>
    /// Endpoint do coletor OpenTelemetry (OTLP).
    /// </summary>
    public string OtelExporterEndpoint { get; set; } = "http://localhost:4317";
}
