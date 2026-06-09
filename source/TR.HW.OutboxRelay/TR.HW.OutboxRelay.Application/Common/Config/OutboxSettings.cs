namespace TR.HW.OutboxRelay.Application.Common.Config;

/// <summary>
/// Configurações para o processo de Relay do Outbox.
/// </summary>
public class OutboxSettings
{
    public const string SectionName = "OutboxRelay";

    /// <summary>
    /// Quantidade máxima de mensagens processadas por lote.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Tempo máximo (em minutos) para considerar uma mensagem em processamento como travada.
    /// </summary>
    public int TimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Limite máximo de tentativas de reenvio antes de marcar como erro permanente.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Expressão Cron para agendamento do Job.
    /// </summary>
    public string CronExpression { get; set; } = "*/15 * * * * *";
}
