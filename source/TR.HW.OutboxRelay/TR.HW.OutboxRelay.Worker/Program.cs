using Hangfire;
using Hangfire.PostgreSql;
using Npgsql;
using Polly;
using TR.HW.OutboxRelay.Application.Ports;
using TR.HW.OutboxRelay.Application.UseCases.RelayMessages;
using TR.HW.OutboxRelay.Infrastructure.Adapters.Data;
using TR.HW.OutboxRelay.Infrastructure.Adapters.Kafka;
using TR.HW.OutboxRelay.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

// DB Setup
var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres") 
    ?? throw new InvalidOperationException("Postgres connection string not found.");

builder.Services.AddNpgsqlDataSource(postgresConnectionString);

// Hangfire Setup
builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(postgresConnectionString));
});

builder.Services.AddHangfireServer();

// Ports & Adapters
builder.Services.AddSingleton<IKafkaPort, KafkaAdapter>();
builder.Services.AddScoped<IOutboxPort, OutboxRepository>();

// Application Services
builder.Services.AddScoped<RelayMessagesUseCase>();

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
    var cronExpression = builder.Configuration["OutboxRelay:CronExpression"] ?? "*/15 * * * * *";
    
    recurringJobManager.AddOrUpdate<OutboxRelayJob>(
        "outbox-relay",
        job => job.ExecuteAsync(),
        cronExpression
    );
}

host.Run();
