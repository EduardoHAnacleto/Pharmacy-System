using Microsoft.EntityFrameworkCore;
using PharmacyWorkerAPI.Data;
using PharmacyWorkerAPI.Hubs;
using PharmacyWorkerAPI.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ===============================
// CONFIGURATION
// ===============================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. Set the "
        + "ConnectionStrings__DefaultConnection environment variable.");
}

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrWhiteSpace(redisConnectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Redis is not configured. Set the "
        + "ConnectionStrings__Redis environment variable.");
}

// Comma-separated list, e.g. "https://shop.example.com,http://localhost:5173".
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// ===============================
// SERVICES
// ===============================
builder.Services.AddControllers();

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 30 * 1024 * 1024; // 30 MB
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(redisConnectionString);
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});
builder.Services.AddSingleton<RedisService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            // No origins configured: allow same-origin only. In the shipped
            // deployment nginx serves the SPA and proxies /api, so browsers
            // never issue a cross-origin request in the first place.
            policy.WithOrigins(Array.Empty<string>());
            return;
        }

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Database. The server version is configured rather than auto-detected:
// AutoDetect opens a connection while the service collection is being built,
// which turns a database that is not yet accepting connections into a failed
// startup instead of a retried query.
var serverVersion = builder.Configuration["Database:ServerVersion"] ?? "8.0";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.Parse(serverVersion),
        mysqlOptions =>
        {
            mysqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null
            );
            mysqlOptions.CommandTimeout(60);
        }
    )
);

var app = builder.Build();

// ===============================
// PIPELINE
// ===============================
app.UseRouting();
app.UseCors("FrontendPolicy");
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.MapHub<PromotionsHub>("/promotionsHub")
    .RequireCors("FrontendPolicy");

app.Run();
