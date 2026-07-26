using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Storefront.Api.Data;
using Storefront.Api.Models;
using Storefront.Api.Utility;

namespace Storefront.Api.Services
{
    public interface IMediaAssetService
    {
        /// <summary>
        /// Stores an uploaded image and returns its asset, reusing an existing row
        /// when the same bytes were uploaded before.
        /// </summary>
        /// <returns><c>null</c> when the content is not a supported image.</returns>
        Task<MediaAsset?> StoreAsync(IFormFile image, int userId, CancellationToken ct = default);

        Task<List<MediaAsset>> ListAsync(int page, int pageSize, CancellationToken ct = default);

        Task<int> CountAsync(CancellationToken ct = default);
    }

    public class MediaAssetService : IMediaAssetService
    {
        private static readonly Dictionary<string, string> MimeByExtension = new()
        {
            [".jpg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
        };

        private readonly AppDbContext _context;
        private readonly IPromotionImageStorage _storage;
        private readonly ILogger<MediaAssetService> _logger;

        public MediaAssetService(
            AppDbContext context,
            IPromotionImageStorage storage,
            ILogger<MediaAssetService> logger)
        {
            _context = context;
            _storage = storage;
            _logger = logger;
        }

        public async Task<MediaAsset?> StoreAsync(
            IFormFile image, int userId, CancellationToken ct = default)
        {
            if (image.Length < ImageValidator.HeaderLength)
                return null;

            // Read once into memory: the same bytes are needed for the hash, the
            // format check and the write, and the upload size is already capped.
            using var buffer = new MemoryStream();
            await using (var source = image.OpenReadStream())
            {
                await source.CopyToAsync(buffer, ct);
            }

            var bytes = buffer.ToArray();

            var extension = ImageValidator.DetectExtension(bytes);
            if (extension == null)
                return null;

            var contentHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            // Deduplicate: the same artwork reused across campaigns should not
            // accumulate copies on disk.
            var existing = await _context.MediaAssets
                .FirstOrDefaultAsync(a => a.ContentHash == contentHash, ct);

            if (existing != null)
            {
                if (existing.IsMissing && _storage.Exists(existing.FilePath))
                {
                    existing.IsMissing = false;
                    await _context.SaveChangesAsync(ct);
                }

                _logger.LogDebug("Reusing media asset {AssetId} for an identical upload.", existing.Id);
                return existing;
            }

            var filePath = await _storage.WriteAsync(bytes, extension, ct);

            var asset = new MediaAsset
            {
                FilePath = filePath,
                ContentHash = contentHash,
                MimeType = MimeByExtension[extension],
                ByteSize = bytes.Length,
                OriginalFileName = Path.GetFileName(image.FileName),
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                IsMissing = false,
            };

            _context.MediaAssets.Add(asset);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Two concurrent uploads of identical bytes: the unique index wins,
                // so drop the file just written and reuse the row that landed.
                _storage.Delete(filePath);

                var winner = await _context.MediaAssets
                    .FirstOrDefaultAsync(a => a.ContentHash == contentHash, ct);

                if (winner != null)
                    return winner;

                throw;
            }

            return asset;
        }

        public Task<List<MediaAsset>> ListAsync(
            int page, int pageSize, CancellationToken ct = default) =>
            _context.MediaAssets
                .AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

        public Task<int> CountAsync(CancellationToken ct = default) =>
            _context.MediaAssets.CountAsync(ct);
    }
}
