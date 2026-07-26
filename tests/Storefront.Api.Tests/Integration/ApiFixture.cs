using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Api.Data;
using Testcontainers.MySql;
using Testcontainers.Redis;
using Xunit;

namespace Storefront.Api.Tests.Integration;

/// <summary>
/// Boots the real API against throwaway MySQL and Redis containers.
/// </summary>
/// <remarks>
/// Real dependencies rather than an in-memory provider: the defects worth
/// catching here are provider-specific — column types, the composite index,
/// unique constraints, cache key versioning — and an in-memory database has
/// opinions about none of them.
/// <para>
/// Requires a working Docker daemon. Containers are built inside
/// <see cref="InitializeAsync"/> rather than in field initialisers because
/// Testcontainers validates the Docker endpoint while building the
/// configuration: doing it eagerly throws during fixture construction, which
/// xUnit reports as a failure in every test of the collection instead of a skip.
/// </para>
/// </remarks>
public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SigningKey = "integration-test-signing-key-32-chars-min";

    public const string AdminUsername = "testadmin";
    public const string AdminPassword = "test-admin-password";

    private MySqlContainer? _mysql;
    private RedisContainer? _redis;

    /// <summary>False when there is no usable Docker daemon; tests then skip.</summary>
    public bool DockerAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _mysql = new MySqlBuilder()
                .WithImage("mysql:8.0")
                .WithDatabase("pharmacy_test")
                .WithUsername("pharmacy")
                .WithPassword("pharmacy")
                .Build();

            _redis = new RedisBuilder()
                .WithImage("redis:7")
                .Build();

            await _mysql.StartAsync();
            await _redis.StartAsync();

            DockerAvailable = true;
        }
        catch (Exception)
        {
            DockerAvailable = false;
            return;
        }

        // Migrations are applied through a standalone context, deliberately not via
        // Services. Touching Services builds the host, which runs the application's
        // startup — including the admin seeder — and that would run against a
        // database with no tables yet. The seeder swallows its own failure, so the
        // symptom was a later login returning 401 in whichever test happened to run
        // before some other host seeded the user.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(_mysql.GetConnectionString(), ServerVersion.Parse("8.0"))
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.MigrateAsync();

        // Now that the schema exists, building the host seeds successfully.
        _ = Services;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Only reached once the containers are up: every test guards on
        // DockerAvailable before it asks for a client.
        builder.UseSetting("ConnectionStrings:DefaultConnection", _mysql!.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis!.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("AdminSeed:Username", AdminUsername);
        builder.UseSetting("AdminSeed:Password", AdminPassword);
        builder.UseSetting("Cors:AllowedOrigins", "http://localhost");

        // Tests log in repeatedly from one address, so the production limit would
        // throttle the suite itself. The test that asserts throttling lowers this
        // for its own host via WithWebHostBuilder.
        builder.UseSetting("RateLimit:LoginPermitLimit", "10000");

        builder.UseEnvironment("Production");
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        if (_mysql != null)
            await _mysql.DisposeAsync();

        if (_redis != null)
            await _redis.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
