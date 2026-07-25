using Microsoft.EntityFrameworkCore;
using PharmacyWorkerAPI.Data;
using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.Services
{
    /// <summary>
    /// Populates reference data and the first admin account on startup.
    /// </summary>
    /// <remarks>
    /// Runs at startup rather than as migration seed data so it stays idempotent
    /// and free of baked-in timestamps, and so it works regardless of whether a
    /// database was created by migrations or baselined from the schema that
    /// preceded them. Schema changes belong in migrations; this is data.
    /// </remarks>
    public static class DatabaseSeeder
    {
        private static readonly string[] DefaultCategories =
        [
            "Generico",
            "Similar",
            "Higiene",
            "Cosméticos",
            "Suplementos",
        ];

        public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
        {
            using var scope = services.CreateScope();

            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(DatabaseSeeder));

            // Seeding must never prevent the API from starting. The storefront is
            // public and read-only, so it stays useful even when the database is
            // mid-migration or unreachable.
            try
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await SeedCategoriesAsync(context, logger, ct);
                await SeedAdminUserAsync(scope.ServiceProvider, context, logger, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Database seeding failed. If this is a fresh deployment, check that "
                    + "migrations have been applied by the migrator service.");
            }
        }

        // ===============================
        // CATEGORIES
        // ===============================
        private static async Task SeedCategoriesAsync(
            AppDbContext context, ILogger logger, CancellationToken ct)
        {
            if (await context.Categories.AnyAsync(ct))
                return;

            context.Categories.AddRange(DefaultCategories.Select(name => new Category
            {
                Name = name,
                CreatedAt = DateTime.UtcNow,
            }));

            await context.SaveChangesAsync(ct);

            logger.LogInformation("Seeded {Count} default categories.", DefaultCategories.Length);
        }

        // ===============================
        // FIRST ADMIN
        // ===============================
        private static async Task SeedAdminUserAsync(
            IServiceProvider services, AppDbContext context, ILogger logger, CancellationToken ct)
        {
            var configuration = services.GetRequiredService<IConfiguration>();

            var username = configuration["AdminSeed:Username"];
            var password = configuration["AdminSeed:Password"];

            // Nothing is created when unconfigured: an application with no usable
            // login is a smaller problem than one with a default login everybody
            // knows. No password or digest is ever committed to the repository.
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogInformation(
                    "AdminSeed:Username/Password not configured; skipping admin seeding.");
                return;
            }

            // Only ever seeds into an empty users table, so restarting the container
            // never resets a password an operator has since changed.
            if (await context.Users.AnyAsync(ct))
            {
                logger.LogDebug("Users already exist; skipping admin seeding.");
                return;
            }

            var hasher = services.GetRequiredService<IPasswordHasher>();

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
    }
}
