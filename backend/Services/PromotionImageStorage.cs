using PharmacyWorkerAPI.Utility;

namespace PharmacyWorkerAPI.Services
{
    public interface IPromotionImageStorage
    {
        /// <summary>
        /// Validates and stores an uploaded image, returning the public relative URL.
        /// </summary>
        /// <returns>
        /// The URL on success, or <c>null</c> when the content is not a supported image.
        /// </returns>
        Task<string?> SaveAsync(IFormFile image, CancellationToken ct = default);

        /// <summary>Deletes a stored image, ignoring anything outside the uploads directory.</summary>
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

        public async Task<string?> SaveAsync(IFormFile image, CancellationToken ct = default)
        {
            if (image.Length < ImageValidator.HeaderLength)
                return null;

            // The Content-Type header and the file name are both supplied by the
            // client, so the format is decided from the bytes instead.
            var header = new byte[ImageValidator.HeaderLength];
            await using (var probe = image.OpenReadStream())
            {
                await probe.ReadExactlyAsync(header.AsMemory(0, header.Length), ct);
            }

            var extension = ImageValidator.DetectExtension(header);
            if (extension == null)
                return null;

            var uploadsFolder = UploadsRoot;
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using (var stream = new FileStream(filePath, FileMode.CreateNew))
            {
                await image.CopyToAsync(stream, ct);
            }

            return $"{PublicPrefix}/{fileName}";
        }

        public void Delete(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return;

            var relative = imagePath
                .TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);

            var uploadsRoot = UploadsRoot;
            var resolved = Path.GetFullPath(Path.Combine(WebRoot, relative));

            // ImagePath comes from the database, but a value being stored there is
            // not a reason to hand an arbitrary path to File.Delete.
            var isContained = resolved.StartsWith(
                uploadsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);

            if (!isContained)
            {
                _logger.LogWarning(
                    "Refusing to delete {ImagePath}: resolves outside the uploads directory.",
                    imagePath);
                return;
            }

            if (File.Exists(resolved))
                File.Delete(resolved);
        }
    }
}
