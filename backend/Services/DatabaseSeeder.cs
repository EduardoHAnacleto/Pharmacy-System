using Microsoft.EntityFrameworkCore;
using Storefront.Api.Data;
using Storefront.Api.Models;

namespace Storefront.Api.Services
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

                await SeedStoreSettingsAsync(scope.ServiceProvider, context, logger, ct);
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
        // STORE SETTINGS
        // ===============================

        /// <summary>
        /// Creates the single configuration row, once.
        /// </summary>
        /// <remarks>
        /// The initial values come from <c>Store:*</c> configuration when present,
        /// so a fresh deployment for a new shop can arrive with its own name and
        /// currency already set from the environment instead of the operator having
        /// to open the admin screen before the site is presentable.
        /// <para>
        /// Only ever inserts. Restarting the container must not undo edits an
        /// operator has since made in the admin screen.
        /// </para>
        /// </remarks>
        private static async Task SeedStoreSettingsAsync(
            IServiceProvider services, AppDbContext context, ILogger logger, CancellationToken ct)
        {
            if (await context.StoreSettings.AnyAsync(ct))
                return;

            var configuration = services.GetRequiredService<IConfiguration>();
            var settings = new StoreSettings { Id = 1 };

            var name = configuration["Store:Name"];
            if (!string.IsNullOrWhiteSpace(name))
                settings.StoreName = name;

            var currency = configuration["Store:Currency"];
            if (!string.IsNullOrWhiteSpace(currency))
                settings.Currency = currency.ToUpperInvariant();

            var locale = configuration["Store:Locale"];
            if (!string.IsNullOrWhiteSpace(locale))
                settings.Locale = locale;

            var country = configuration["Store:CountryCode"];
            if (!string.IsNullOrWhiteSpace(country))
                settings.CountryCode = country.ToUpperInvariant();

            var timeZone = configuration["Store:TimeZone"];
            if (!string.IsNullOrWhiteSpace(timeZone))
                settings.TimeZone = timeZone;

            var whatsApp = configuration["Store:WhatsAppNumber"];
            if (!string.IsNullOrWhiteSpace(whatsApp))
                settings.WhatsAppNumber = whatsApp;

            // No built-in default: a shop's logo is its own. The existing pharmacy's
            // artwork ships in the frontend's public directory, so setting this to
            // /logoFarma.png restores exactly what it had.
            var logoUrl = configuration["Store:LogoUrl"];
            if (!string.IsNullOrWhiteSpace(logoUrl))
                settings.LogoUrl = logoUrl;

            settings.UpdatedAt = DateTime.UtcNow;

            context.StoreSettings.Add(settings);
            await context.SaveChangesAsync(ct);

            logger.LogInformation("Seeded store settings for {StoreName}.", settings.StoreName);
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
