using Carter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using TicketReserver.Diagnostics;
using TR.API.Reservation.Application;
using TR.API.Reservation.Infrastructure;
using TR.API.Reservation.WebApi.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with Trace context
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service_name", "reservation-api")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Configure OpenTelemetry (Tracing & Metrics)
var otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "reservation-api";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: serviceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource(TicketReserverDiagnostics.KafkaActivitySourceName)
            .AddSource(TicketReserverDiagnostics.HttpActivitySourceName);

        if (!string.IsNullOrEmpty(otelEndpoint))
        {
            tracing.AddOtlpExporter(opt => opt.Endpoint = new Uri(otelEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(TicketReserverDiagnostics.MeterName);

        if (!string.IsNullOrEmpty(otelEndpoint))
        {
            metrics.AddOtlpExporter(opt => opt.Endpoint = new Uri(otelEndpoint));
        }
    });

// Register application & infrastructure services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add Carter & OpenAPI
builder.Services.AddCarter();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Global Exception Handler Middleware
app.UseMiddleware<ExceptionHandlerMiddleware>();

// Map Carter Endpoints
app.MapCarter();

app.Run();

// Required for WebApplicationFactory in integration/unit tests
public partial class Program { }
