using Microsoft.Extensions.Hosting;
using Serilog;
using TR.HW.Repl.Application;
using TR.HW.Repl.Infrastructure;
using TR.HW.Repl.Worker;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting TicketReserver Search Replication Worker Host...");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, loggerConfig) =>
    {
        loggerConfig
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console();
    });

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddWorkerServices(builder.Configuration);

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
