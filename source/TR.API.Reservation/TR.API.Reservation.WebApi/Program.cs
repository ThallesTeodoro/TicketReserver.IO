using Carter;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using StackExchange.Redis;
using TR.API.Reservation.Application.UseCases.CreateReservation;
using TR.API.Reservation.Application.Ports;
using TR.API.Reservation.Domain.Repositories;
using TR.API.Reservation.Infrastructure.Adapters;
using TR.API.Reservation.Infrastructure.Repositories;
using TR.API.Reservation.WebApi.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Telemetry Setup (OpenTelemetry)
const string ServiceName = "TR.API.Reservation";
var otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource(ServiceName)
               .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ServiceName))
               .AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddNpgsql()
               .AddOtlpExporter(opt => opt.Endpoint = new Uri(otelEndpoint));
    });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ServiceName));
    logging.AddOtlpExporter(opt => opt.Endpoint = new Uri(otelEndpoint));
});

// Add services to the container.
builder.Services.AddOpenApi();

// DB Setup
var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Database=reservation_db;Username=postgres;Password=postgres";
builder.Services.AddNpgsqlDataSource(postgresConnectionString);

// Redis Setup
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(postgresConnectionString)
    .AddRedis(redisConnectionString);

// Resilience (Polly)
builder.Services.AddResiliencePipeline("default", pipelineBuilder =>
{
    pipelineBuilder.AddRetry(new Polly.Retry.RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true
    });
});

// Idempotency (Ports & Adapters)
builder.Services.AddScoped<IIdempotencyPort, RedisIdempotencyAdapter>();

// Application Services
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<CreateReservationUseCase>();

// Carter Setup
builder.Services.AddCarter();

// Exception Handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

// Health Check Endpoints
app.MapHealthChecks("/healthz");

// Custom Middlewares
app.UseMiddleware<IdempotencyMiddleware>();

// Endpoints
app.MapCarter();

app.Run();
