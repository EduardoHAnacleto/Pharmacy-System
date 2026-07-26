using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Storefront.Api.Data;
using Storefront.Api.Models;

namespace Storefront.Api.Services
{
    /// <summary>
    /// Creates media assets for promotions that predate the media library.
    /// </summary>
    /// <remarks>
    /// Cannot be done in SQL: linking a promotion to an asset requires hashing the
    /// file on disk. Runs once at startup, is idempotent, and treats a file that is
    /// no longer present as a first-class outcome rather than an error — that is the
    /// state left behind by uploads having had no volume before Phase 0, and the
    /// admin surfaces the count so it can be dealt with.
    /// </remarks>
    public static class MediaBackfillService
    {
        public static async Task RunAsync(IServiceProvider services, CancellationToken ct = default)
        {
            using var scope = services.CreateScope();

            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(MediaBackfillService));

            try
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var storage = scope.ServiceProvider.GetRequiredService<IPromotionImageStorage>();

                var pending = await context.ItemPromotions
                    .Where(p => p.MediaAssetId == null)
                    .ToListAsync(ct);

                if (pending.Count == 0)
                    return;

                logger.LogInformation(
                    "Backfilling media assets for {Count} promotions.", pending.Count);

                var missing = 0;

                foreach (var promotion in pending)
                {
                    var asset = await ResolveAssetAsync(context, storage, promotion, ct);

                    promotion.MediaAssetId = asset.Id;

                    if (asset.IsMissing)
                        missing++;
                }

                await context.SaveChangesAsync(ct);

                if (missing > 0)
                {
                    logger.LogWarning(
                        "{Count} promotions reference an image file that is no longer on disk. "
                        + "They are flagged so the admin can re-upload artwork.",
                        missing);
                }
            }
            catch (Exception ex)
            {
                // The storefront is public and read-only; it must keep serving even
                // if this could not run.
                logger.LogError(ex, "Media backfill failed.");
            }
        }

        private static async Task<MediaAsset> ResolveAssetAsync(
            AppDbContext context,
            IPromotionImageStorage storage,
            ItemPromotion promotion,
            CancellationToken ct)
        {
            var bytes = storage.Exists(promotion.ImagePath)
                ? await ReadAsync(storage, promotion.ImagePath, ct)
                : null;

            if (bytes == null)
            {
                // One placeholder per orphaned path, keyed by a hash of the path
                // itself so repeated runs do not create duplicates.
                var pathHash = HashOf(System.Text.Encoding.UTF8.GetBytes(promotion.ImagePath ?? string.Empty));

                var placeholder = await context.MediaAssets
                    .FirstOrDefaultAsync(a => a.ContentHash == pathHash, ct);

                if (placeholder != null)
                    return placeholder;

                placeholder = new MediaAsset
                {
                    FilePath = promotion.ImagePath ?? string.Empty,
                    ContentHash = pathHash,
                    MimeType = "application/octet-stream",
                    ByteSize = 0,
                    CreatedByUserId = promotion.CreatedByUserId,
                    CreatedAt = promotion.CreatedAt,
                    IsMissing = true,
                };

                context.MediaAssets.Add(placeholder);
                await context.SaveChangesAsync(ct);

                return placeholder;
            }

            var contentHash = HashOf(bytes);

            var existing = await context.MediaAssets
                .FirstOrDefaultAsync(a => a.ContentHash == contentHash, ct);

            if (existing != null)
                return existing;

            var asset = new MediaAsset
            {
                FilePath = promotion.ImagePath,
                ContentHash = contentHash,
                MimeType = MimeFor(promotion.ImagePath),
                ByteSize = bytes.Length,
                CreatedByUserId = promotion.CreatedByUserId,
                CreatedAt = promotion.CreatedAt,
                IsMissing = false,
            };

            context.MediaAssets.Add(asset);
            await context.SaveChangesAsync(ct);

            return asset;
        }

        private static async Task<byte[]?> ReadAsync(
            IPromotionImageStorage storage, string imagePath, CancellationToken ct)
        {
            try
            {
                return await storage.ReadAsync(imagePath, ct);
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static string HashOf(byte[] bytes) =>
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        private static string MimeFor(string imagePath) =>
            Path.GetExtension(imagePath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "application/octet-stream",
            };
    }
}
