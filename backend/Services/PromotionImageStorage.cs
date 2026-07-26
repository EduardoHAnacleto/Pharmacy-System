namespace Storefront.Api.Services
{
    /// <summary>
    /// File storage for promotion images. Knows nothing about promotions or
    /// validation — <see cref="MediaAssetService"/> owns both.
    /// </summary>
    public interface IPromotionImageStorage
    {
        /// <summary>Writes bytes under a generated name and returns the public relative URL.</summary>
        Task<string> WriteAsync(byte[] content, string extension, CancellationToken ct = default);

        /// <summary>Whether the file behind a stored relative URL is present.</summary>
        bool Exists(string? imagePath);

        /// <summary>Reads a stored file, or null when it is absent or out of bounds.</summary>
        Task<byte[]?> ReadAsync(string? imagePath, CancellationToken ct = default);

        /// <summary>Deletes a file, refusing anything outside the uploads directory.</summary>
        void Delete(string? imagePath);
    }

    public class PromotionImageStorage : IPromotionImageStorage
    {
        private const string PublicPrefix = "/images/promotions";

        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<PromotionImageStorage> _logger;

        public PromotionImageStorage(
            IWebHostEnvironment environment, ILogger<PromotionImageStorage> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        /// <summary>Web root, falling back to ./wwwroot when the host resolved none.</summary>
        private string WebRoot =>
            _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        /// <summary>Absolute, normalised directory that promotion images are confined to.</summary>
        private string UploadsRoot =>
            Path.GetFullPath(Path.Combine(WebRoot, "images", "promotions"));

        public async Task<string> WriteAsync(
            byte[] content, string extension, CancellationToken ct = default)
        {
            var uploadsFolder = UploadsRoot;
            Directory.CreateDirectory(uploadsFolder);

            // The extension comes from the detected format, never from a
            // client-supplied file name.
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await File.WriteAllBytesAsync(filePath, content, ct);

            return $"{PublicPrefix}/{fileName}";
        }

        public bool Exists(string? imagePath)
        {
            var resolved = ResolveContained(imagePath);

            return resolved != null && File.Exists(resolved);
        }

        public async Task<byte[]?> ReadAsync(string? imagePath, CancellationToken ct = default)
        {
            var resolved = ResolveContained(imagePath);

            if (resolved == null || !File.Exists(resolved))
                return null;

            return await File.ReadAllBytesAsync(resolved, ct);
        }

        public void Delete(string? imagePath)
        {
            var resolved = ResolveContained(imagePath);

            if (resolved == null)
                return;

            if (File.Exists(resolved))
                File.Delete(resolved);
        }

        /// <summary>
        /// Maps a stored relative URL to an absolute path, returning null when it
        /// escapes the uploads directory. ImagePath comes from the database, but a
        /// value being stored there is not a reason to trust it as a filesystem path.
        /// </summary>
        private string? ResolveContained(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return null;

            var relative = imagePath
                .TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);

            var uploadsRoot = UploadsRoot;
            var resolved = Path.GetFullPath(Path.Combine(WebRoot, relative));

            if (!resolved.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Refusing to touch {ImagePath}: resolves outside the uploads directory.",
                    imagePath);
                return null;
            }

            return resolved;
        }
    }
}
