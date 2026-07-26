using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;
using Storefront.Api.Data;
using Storefront.Api.Hubs;
using Storefront.Api.Infrastructure;
using Storefront.Api.Options;
using Storefront.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ===============================
// LOGGING
// ===============================
// Structured logging, configurable from appsettings without a rebuild.
builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext());

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

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                 ?? new JwtOptions();

// A weak or absent signing key means anyone can mint an admin token, so refuse
// to start rather than come up insecure.
if (jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be at least 32 characters. Set the Jwt__SigningKey "
        + "environment variable to a random secret.");
}

// ===============================
// SERVICES
// ===============================
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

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
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Storefront API",
        Version = "v1",
        Description =
            "Promotion storefront with WhatsApp order handoff. Reads are public; "
            + "writes require an Admin bearer token. No personal customer data is "
            + "accepted or stored by any endpoint.",
    });

    // Pulls the ///-comments through, so the generated reference carries the
    // reasoning that lives next to the code rather than a list of bare routes.
    var xmlPath = Path.Combine(
        AppContext.BaseDirectory,
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");

    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    // Lets the Swagger UI exercise the authenticated endpoints, which is the
    // difference between documentation and a usable console.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the accessToken returned by POST /api/v1/auth/login.",
    });

    // Referenced by id rather than by a second inline definition, and built from a
    // factory taking the document: Microsoft.OpenApi 2.x resolves references against
    // the host document, so the requirement cannot be constructed before it exists.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
});

// Problem details for both thrown exceptions and framework-produced responses.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Auth
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<AuthService>();

// Domain
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<IMediaAssetService, MediaAssetService>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IStoreSettingsService, StoreSettingsService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddSingleton<IPromotionImageStorage, PromotionImageStorage>();

// Records status transitions and reconciles missing image files.
builder.Services.AddHostedService<PromotionMaintenanceService>();

// ===============================
// OBSERVABILITY
// ===============================
// Traces and metrics for requests and outbound calls. Serilog already answers
// "what happened"; this answers "where did the time go", which is the question a
// slow storefront actually raises.
//
// Exported over OTLP only when an endpoint is configured. Without one the
// instrumentation still runs and costs almost nothing, so there is no separate
// build or code path for a deployment that has no collector — which is every
// deployment until someone stands one up.
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: builder.Configuration["OpenTelemetry:ServiceName"] ?? "storefront-api",
        serviceVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString()))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                // Health probes would otherwise dominate the trace volume while
                // saying nothing about the application.
                options.Filter = context =>
                    !context.Request.Path.StartsWithSegments("/health");
            })
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
    });

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

// Rate limiting. Brute-forcing the login is now bounded server-side; the old
// limit lived in the browser's localStorage and could be cleared at will.
var rateLimitOptions =
    builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
    ?? new RateLimitOptions();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.LoginPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.LoginWindowSeconds),
                QueueLimit = 0,
            }));

    // Telemetry ingestion is public and batched, so the ceiling is generous but
    // still bounded — an open write endpoint is otherwise a way to fill a disk.
    options.AddPolicy("events", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.EventsPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.EventsWindowSeconds),
                QueueLimit = 0,
            }));

    // Orders are also public. A tighter limit keeps a script from polluting the
    // sales figures the dashboard reports on.
    options.AddPolicy("orders", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.OrdersPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.OrdersWindowSeconds),
                QueueLimit = 0,
            }));
});

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(redisConnectionString);
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});
builder.Services.AddSingleton<RedisService>();

// Health checks. "ready" is the gate for orchestration; "live" only reports that
// the process is up, so a database blip does not trigger a restart loop.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);

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
// First in the pipeline so it also catches failures from the middleware below.
app.UseExceptionHandler();

app.UseSerilogRequestLogging();

app.UseRouting();
app.UseCors("FrontendPolicy");
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<PromotionsHub>("/promotionsHub")
    .RequireCors("FrontendPolicy");

// Liveness: the process responds. Deliberately has no dependency checks.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false,
});

// Readiness: dependencies are usable.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

await DatabaseSeeder.SeedAsync(app.Services);

// Links promotions created before the media library existed to an asset, which
// requires hashing files on disk and so cannot be a SQL migration.
await MediaBackfillService.RunAsync(app.Services);

app.Run();

/// <summary>
/// Top-level statements generate an internal Program class. Declaring it public
/// lets the integration tests boot the real application through
/// WebApplicationFactory instead of re-declaring the pipeline in test code.
/// </summary>
public partial class Program { }
