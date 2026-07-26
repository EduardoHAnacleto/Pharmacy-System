using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Storefront.Api.Data
{
    /// <summary>
    /// Builds an <see cref="AppDbContext"/> for the `dotnet ef` tooling.
    /// </summary>
    /// <remarks>
    /// Without this, the tooling constructs the application host to find the
    /// context — and the host deliberately throws when the connection string or
    /// the JWT signing key are absent, which is exactly the situation on a
    /// developer machine or a CI runner. Design-time commands never open a
    /// connection, so the placeholder below is only there to satisfy the
    /// provider's parser.
    /// </remarks>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        private const string PlaceholderConnection =
            "server=localhost;port=3306;database=pharmacy_db;user=root;password=root";

        public AppDbContext CreateDbContext(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
                connectionString = PlaceholderConnection;

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseMySql(connectionString, ServerVersion.Parse("8.0"));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
