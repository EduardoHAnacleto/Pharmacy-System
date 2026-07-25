using Microsoft.EntityFrameworkCore;
using PharmacyWorkerAPI.Data;
using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.Services
{
    /// <summary>
    /// Creates the first admin account from configuration on startup.
    /// </summary>
    /// <remarks>
    /// Seeding from the environment rather than a SQL insert means no password —
    /// and no digest of one — is ever committed to the repository. When the
    /// settings are absent nothing is created: an application with no usable
    /// login is a smaller problem than one with a default login everybody knows.
    /// </remarks>
    public static class AdminUserSeeder
    {
        public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
        {
            using var scope = services.CreateScope();

            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(AdminUserSeeder));

            var username = configuration["AdminSeed:Username"];
            var password = configuration["AdminSeed:Password"];

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogInformation(
                    "AdminSeed:Username/Password not configured; skipping admin seeding.");
                return;
            }

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Seeding must never prevent the API from starting. The storefront is
            // read-only and public, so it stays useful even when the users table is
            // missing — which is exactly the state of a deployment created before
            // Phase 1, until database/upgrades/001_add_auth_tables.sql is applied.
            try
            {
                // Only ever seeds into an empty users table, so restarting the
                // container never resets a password an operator has since changed.
                if (await context.Users.AnyAsync(ct))
                {
                    logger.LogDebug("Users already exist; skipping admin seeding.");
                    return;
                }

                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

                context.Users.Add(new User
                {
                    Username = username,
                    Email = configuration["AdminSeed:Email"],
                    PasswordHash = hasher.Hash(password),
                    Role = Roles.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                });

                await context.SaveChangesAsync(ct);

                logger.LogInformation("Seeded initial admin user {Username}.", username);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Admin seeding failed. The admin area will reject every login until "
                    + "this is resolved — check that database/upgrades/001_add_auth_tables.sql "
                    + "has been applied.");
            }
        }
    }
}
