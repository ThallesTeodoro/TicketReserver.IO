using Hangfire;
using Hangfire.PostgreSql;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using TR.HW.OutboxRelay.Application.Common.Config;
using TR.HW.OutboxRelay.Application.DependencyInjection;
using TR.HW.OutboxRelay.Domain;
using TR.HW.OutboxRelay.Infrastructure.DependencyInjection;
using TR.HW.OutboxRelay.Worker.Config;
using TR.HW.OutboxRelay.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

// Infrastructure Settings
var infraSettings = new InfrastructureSettings();
builder.Configuration.GetSection(InfrastructureSettings.SectionName).Bind(infraSettings);
builder.Services.Configure<InfrastructureSettings>(builder.Configuration.GetSection(InfrastructureSettings.SectionName));

// Telemetry Setup (OpenTelemetry)
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource(Telemetry.ActivitySource.Name)
               .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(Telemetry.ServiceName))
               .AddHttpClientInstrumentation()
               .AddNpgsql()
               .AddOtlpExporter(opt => opt.Endpoint = new Uri(infraSettings.OtelExporterEndpoint));
    });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(Telemetry.ServiceName));
    logging.AddOtlpExporter(opt => opt.Endpoint = new Uri(infraSettings.OtelExporterEndpoint));
});

// DB Setup
builder.Services.AddNpgsqlDataSource(infraSettings.PostgresConnection);

// Hangfire Setup
builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(infraSettings.PostgresConnection));
});

builder.Services.AddHangfireServer();

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(infraSettings.PostgresConnection)
    .AddRedis(infraSettings.RedisConnection);

// Layers Setup
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// Jobs
builder.Services.AddScoped<OutboxRelayJob>();

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

var host = builder.Build();

// Schedule Recurring Job
using (var scope = host.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    var outboxSettings = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OutboxSettings>>().Value;
    
    recurringJobManager.AddOrUpdate<OutboxRelayJob>(
        "outbox-relay",
        job => job.ExecuteAsync(),
        outboxSettings.CronExpression
    );
}

host.Run();
