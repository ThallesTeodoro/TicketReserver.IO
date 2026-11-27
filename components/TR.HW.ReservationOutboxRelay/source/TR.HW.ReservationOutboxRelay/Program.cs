using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using TicketReserver.Diagnostics;
using TR.HW.ReservationOutboxRelay;
using TR.HW.ReservationOutboxRelay.Workers;

var builder = Host.CreateApplicationBuilder(args);

var otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "reservation-outbox-relay";

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service_name", serviceName)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: serviceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(TicketReserverDiagnostics.KafkaActivitySourceName);

        if (!string.IsNullOrEmpty(otelEndpoint))
        {
            tracing.AddOtlpExporter(opt => opt.Endpoint = new Uri(otelEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddRuntimeInstrumentation()
            .AddMeter(TicketReserverDiagnostics.MeterName);

        if (!string.IsNullOrEmpty(otelEndpoint))
        {
            metrics.AddOtlpExporter(opt => opt.Endpoint = new Uri(otelEndpoint));
        }
    });

builder.Services.AddRelayInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ReservationOutboxRelayWorker>();

var host = builder.Build();
host.Run();

// Necessário para testes de integração ou WebApplicationFactory
public partial class Program { }
