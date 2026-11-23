using Serilog;
using TR.HW.ResCompensator.Worker;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddReservationCompensatorWorkerServices(builder.Configuration);

var host = builder.Build();
host.Run();

public partial class Program { }
