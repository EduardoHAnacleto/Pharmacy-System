using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PharmacyWorkerAPI.Data;
using Testcontainers.MySql;
using Testcontainers.Redis;
using Xunit;

namespace PharmacyWorkerAPI.Tests.Integration;

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

        // Create the schema exactly the way production does — by applying migrations.
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
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
