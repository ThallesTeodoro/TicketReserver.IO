using StackExchange.Redis;
using TR.API.Reservation.Application.Ports;
using TR.API.Reservation.Infrastructure.Adapters;
using TR.API.Reservation.WebApi.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// Redis Setup
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

// Idempotency (Ports & Adapters)
builder.Services.AddScoped<IIdempotencyPort, RedisIdempotencyAdapter>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Custom Middlewares
app.UseMiddleware<IdempotencyMiddleware>();

app.Run();
