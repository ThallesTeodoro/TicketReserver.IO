using Carter;
using Serilog;
using TR.API.Reservation.Application;
using TR.API.Reservation.Infrastructure;
using TR.API.Reservation.WebApi.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

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
