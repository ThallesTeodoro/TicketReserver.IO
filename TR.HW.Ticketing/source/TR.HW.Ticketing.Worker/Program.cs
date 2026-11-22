using Serilog;
using TR.HW.Ticketing.Worker;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddTicketingWorkerServices(builder.Configuration);

var host = builder.Build();
host.Run();

public partial class Program { }
