using Serilog;
using TR.HW.ReservationOutboxRelay;
using TR.HW.ReservationOutboxRelay.Workers;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddRelayInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ReservationOutboxRelayWorker>();

var host = builder.Build();
host.Run();

// Necessário para testes de integração ou WebApplicationFactory
public partial class Program { }
