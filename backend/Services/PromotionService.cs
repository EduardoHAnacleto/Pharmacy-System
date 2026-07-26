using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Storefront.Api.Data;
using Storefront.Api.DTOs;
using Storefront.Api.DTOs.ItemPromotion;
using Storefront.Api.Hubs;
using Storefront.Api.Mapping;
using Storefront.Api.Models;
using Storefront.Api.Utility;

namespace Storefront.Api.Services
{
    /// <summary>Outcome of a write, distinguishing rejection from not-found.</summary>
    public record PromotionResult(
        bool Succeeded,
        ItemPromotionResponseDto? Promotion = null,
        string? Error = null,
        bool NotFound = false)
    {
        public static PromotionResult Rejected(string error) => new(false, Error: error);

        public static PromotionResult Missing() => new(false, NotFound: true);
    }

    public interface IPromotionService
    {
        Task<PromotionResult> CreateAsync(
            ItemPromotionCreateRequestDto dto, int userId, string userName, CancellationToken ct = default);

        Task<PromotionResult> UpdateAsync(
            int id, ItemPromotionUpdateRequestDto dto, int userId, CancellationToken ct = default);

        /// <summary>Retires a promotion, keeping the row and its image.</summary>
        Task<PromotionResult> ArchiveAsync(int id, int userId, CancellationToken ct = default);

        /// <summary>Clones an archived promotion into a new one, reusing its image.</summary>
        Task<PromotionResult> ReactivateAsync(
            int id, ReactivatePromotionRequestDto dto, int userId, string userName,
            CancellationToken ct = default);

        Task<ItemPromotionResponseDto?> GetByIdAsync(int id, CancellationToken ct = default);

        Task<PagedResultDto<ItemPromotionResponseDto>> GetByStatusAsync(
            string? status, int page, int pageSize, CancellationToken ct = default);

        Task<List<ItemPromotionResponseDto>> GetAllAsync(CancellationToken ct = default);

        Task<List<ItemPromotionResponseDto>> GetActiveAsync(CancellationToken ct = default);

        Task<List<ItemPromotionResponseDto>> GetCreatedAfterAsync(
            DateTime? minCreatedAt, CancellationToken ct = default);

        Task<PagedResultDto<ItemPromotionResponseDto>> GetActivePagedAsync(
            int page, int pageSize, string? timeZone, PromotionFilterDto? filter = null,
            CancellationToken ct = default);

        Task<List<PromotionStatusHistoryDto>?> GetHistoryAsync(int id, CancellationToken ct = default);

        Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default);

        Task<int> CountMissingImagesAsync(CancellationToken ct = default);
    }

    public class PromotionService : IPromotionService
    {
        private const string PromotionScope = "item-promotions";
        private const string CategoryScope = "categories";

        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        private readonly AppDbContext _context;
        private readonly RedisService _cache;
        private readonly IMediaAssetService _media;
        private readonly IAuditLogger _audit;
        private readonly IHubContext<PromotionsHub> _hub;
        private readonly ILogger<PromotionService> _logger;

        public PromotionService(
            AppDbContext context,
            RedisService cache,
            IMediaAssetService media,
            IAuditLogger audit,
            IHubContext<PromotionsHub> hub,
            ILogger<PromotionService> logger)
        {
            _context = context;
            _cache = cache;
            _media = media;
            _audit = audit;
            _hub = hub;
            _logger = logger;
        }

        // ===============================
        // CREATE
        // ===============================
        public async Task<PromotionResult> CreateAsync(
            ItemPromotionCreateRequestDto dto, int userId, string userName, CancellationToken ct = default)
        {
            if (dto.Image == null || dto.Image.Length == 0)
                return PromotionResult.Rejected("Imagem é obrigatória.");

            var rejection = ValidateWindowAndPrices(dto.Price, dto.PriceBefore, dto.DateStart, dto.DateEnd);
            if (rejection != null)
                return PromotionResult.Rejected(rejection);

            if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId, ct))
                return PromotionResult.Rejected("Categoria inválida.");

            var asset = await _media.StoreAsync(dto.Image, userId, ct);
            if (asset == null)
                return PromotionResult.Rejected("Formato de imagem inválido. Envie JPEG, PNG ou WebP.");

            var status = DeriveStatus(dto.Publish, dto.DateStart, dto.DateEnd, DateTime.UtcNow);

            var promotion = new ItemPromotion
            {
                Name = dto.Name,
                Price = dto.Price,
                PriceBefore = dto.PriceBefore,
                ImagePath = asset.FilePath,
                MediaAssetId = asset.Id,

                DateStart = dto.DateStart,
                DateEnd = dto.DateEnd,

                Status = status,
                CategoryId = dto.CategoryId,
                ProductType = dto.ProductType,

                // Audit fields come from the authenticated caller, never the request.
                CreatedByUserId = userId,
                CreatedByUserName = userName,
                CreatedAt = DateTime.UtcNow,
            };

            _context.ItemPromotions.Add(promotion);
            await _context.SaveChangesAsync(ct);

            await RecordTransitionAsync(promotion, null, status, userId, "created", ct);
            await _audit.RecordAsync(userId, userName, "create", nameof(ItemPromotion), promotion.Id, null, ct);
            await NotifyChangedAsync();

            return new PromotionResult(true, promotion.ToDto());
        }

        // ===============================
        // UPDATE
        // ===============================
        public async Task<PromotionResult> UpdateAsync(
            int id, ItemPromotionUpdateRequestDto dto, int userId, CancellationToken ct = default)
        {
            var promotion = await _context.ItemPromotions.FindAsync([id], ct);

            if (promotion == null)
                return PromotionResult.Missing();

            if (promotion.Status == PromotionStatus.Archived)
                return PromotionResult.Rejected(
                    "Promoção arquivada não pode ser editada. Reative-a primeiro.");

            var rejection = ValidateWindowAndPrices(dto.Price, dto.PriceBefore, dto.DateStart, dto.DateEnd);
            if (rejection != null)
                return PromotionResult.Rejected(rejection);

            if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId, ct))
                return PromotionResult.Rejected("Categoria inválida.");

            var previousStatus = promotion.Status;

            promotion.Name = dto.Name;
            promotion.Price = dto.Price;
            promotion.PriceBefore = dto.PriceBefore;
            promotion.DateStart = dto.DateStart;
            promotion.DateEnd = dto.DateEnd;
            promotion.CategoryId = dto.CategoryId;
            promotion.ProductType = dto.ProductType;
            promotion.Status = DeriveStatus(dto.Publish, dto.DateStart, dto.DateEnd, DateTime.UtcNow);
            promotion.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            if (previousStatus != promotion.Status)
                await RecordTransitionAsync(promotion, previousStatus, promotion.Status, userId, "edited", ct);

            await _audit.RecordAsync(userId, null, "update", nameof(ItemPromotion), id, null, ct);
            await NotifyChangedAsync();

            return new PromotionResult(true, promotion.ToDto());
        }

        // ===============================
        // ARCHIVE
        // ===============================
        public async Task<PromotionResult> ArchiveAsync(
            int id, int userId, CancellationToken ct = default)
        {
            var promotion = await _context.ItemPromotions.FindAsync([id], ct);

            if (promotion == null)
                return PromotionResult.Missing();

            if (promotion.Status == PromotionStatus.Archived)
                return PromotionResult.Rejected("Promoção já está arquivada.");

            var previousStatus = promotion.Status;

            // Deliberately does not touch the image file. Deleting it was what made
            // every retired promotion a total loss, with no way to run it again.
            promotion.Status = PromotionStatus.Archived;
            promotion.ArchivedAt = DateTime.UtcNow;
            promotion.ArchivedByUserId = userId;

            await _context.SaveChangesAsync(ct);

            await RecordTransitionAsync(
                promotion, previousStatus, PromotionStatus.Archived, userId, "archived", ct);
            await _audit.RecordAsync(userId, null, "archive", nameof(ItemPromotion), id, null, ct);
            await NotifyChangedAsync();

            return new PromotionResult(true, promotion.ToDto());
        }

        // ===============================
        // REACTIVATE
        // ===============================
        public async Task<PromotionResult> ReactivateAsync(
            int id, ReactivatePromotionRequestDto dto, int userId, string userName,
            CancellationToken ct = default)
        {
            var source = await _context.ItemPromotions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (source == null)
                return PromotionResult.Missing();

            var price = dto.Price ?? source.Price;
            var priceBefore = dto.PriceBefore ?? source.PriceBefore;

            var rejection = ValidateWindowAndPrices(price, priceBefore, dto.DateStart, dto.DateEnd);
            if (rejection != null)
                return PromotionResult.Rejected(rejection);

            // A new row rather than flipping the old one back: the previous run
            // keeps its own dates and history, so the two can be compared.
            var clone = new ItemPromotion
            {
                Name = dto.Name ?? source.Name,
                Price = price,
                PriceBefore = priceBefore,

                // Same asset — no re-upload, and no second copy on disk.
                ImagePath = source.ImagePath,
                MediaAssetId = source.MediaAssetId,

                DateStart = dto.DateStart,
                DateEnd = dto.DateEnd,

                Status = DeriveStatus(dto.Publish, dto.DateStart, dto.DateEnd, DateTime.UtcNow),
                CategoryId = source.CategoryId,
                ProductType = source.ProductType,

                SourcePromotionId = source.Id,

                CreatedByUserId = userId,
                CreatedByUserName = userName,
                CreatedAt = DateTime.UtcNow,
            };

            _context.ItemPromotions.Add(clone);
            await _context.SaveChangesAsync(ct);

            await RecordTransitionAsync(
                clone, null, clone.Status, userId, $"reactivated from #{source.Id}", ct);
            await _audit.RecordAsync(
                userId, userName, "reactivate", nameof(ItemPromotion), clone.Id,
                $"{{\"sourcePromotionId\":{source.Id}}}", ct);
            await NotifyChangedAsync();

            return new PromotionResult(true, clone.ToDto());
        }

        // ===============================
        // READ
        // ===============================
        public Task<ItemPromotionResponseDto?> GetByIdAsync(int id, CancellationToken ct = default) =>
            _cache.GetOrSetAsync(PromotionScope, $"id:{id}", CacheTtl, async () =>
                await _context.ItemPromotions
                    .AsNoTracking()
                    .Where(p => p.Id == id)
                    .Select(ItemPromotionMapping.ToResponseDto)
                    .FirstOrDefaultAsync(ct));

        public Task<List<ItemPromotionResponseDto>> GetAllAsync(CancellationToken ct = default) =>
            _cache.GetOrSetAsync(PromotionScope, "all:by-end-date", CacheTtl, async () =>
                await _context.ItemPromotions
                    .AsNoTracking()
                    .Where(p => p.Status != PromotionStatus.Archived)
                    .OrderBy(p => p.DateEnd)
                    .Select(ItemPromotionMapping.ToResponseDto)
                    .ToListAsync(ct));

        public Task<PagedResultDto<ItemPromotionResponseDto>> GetByStatusAsync(
            string? status, int page, int pageSize, CancellationToken ct = default)
        {
            (page, pageSize) = ClampPaging(page, pageSize);

            var key = $"by-status:{status ?? "any"}:page:{page}:size:{pageSize}";

            return _cache.GetOrSetAsync(PromotionScope, key, CacheTtl, async () =>
            {
                var query = _context.ItemPromotions.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(p => p.Status == status);

                var totalItems = await query.CountAsync(ct);

                var items = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(ItemPromotionMapping.ToResponseDto)
                    .ToListAsync(ct);

                return Page(items, page, pageSize, totalItems);
            });
        }

        public Task<List<ItemPromotionResponseDto>> GetActiveAsync(CancellationToken ct = default) =>
            _cache.GetOrSetAsync(PromotionScope, "active:all", CacheTtl, async () =>
            {
                var now = DateTime.UtcNow;

                return await VisibleAt(now)
                    .OrderBy(p => p.DateEnd)
                    .Select(ItemPromotionMapping.ToResponseDto)
                    .ToListAsync(ct);
            });

        public Task<List<ItemPromotionResponseDto>> GetCreatedAfterAsync(
            DateTime? minCreatedAt, CancellationToken ct = default) =>
            _cache.GetOrSetAsync(
                PromotionScope, $"created-after:{minCreatedAt:yyyyMMddHHmm}", CacheTtl, async () =>
                {
                    var query = _context.ItemPromotions.AsNoTracking();

                    if (minCreatedAt.HasValue)
                        query = query.Where(p => p.CreatedAt >= minCreatedAt.Value);

                    return await query
                        .OrderByDescending(p => p.CreatedAt)
                        .Select(ItemPromotionMapping.ToResponseDto)
                        .ToListAsync(ct);
                });

        public Task<PagedResultDto<ItemPromotionResponseDto>> GetActivePagedAsync(
            int page, int pageSize, string? timeZone, PromotionFilterDto? filter = null,
            CancellationToken ct = default)
        {
            (page, pageSize) = ClampPaging(page, pageSize);

            var userTimeZone = Utilities.GetTimeZone(timeZone, _logger);
            var normalised = (filter ?? new PromotionFilterDto()).Normalised();

            // The window depends on the caller's time zone, and the result on every
            // filter, so all of them belong in the key — otherwise the first
            // caller's result is served to everyone.
            var key = $"active:tz:{userTimeZone.Id}:page:{page}:size:{pageSize}:{normalised.CacheKey()}";

            return _cache.GetOrSetAsync(PromotionScope, key, CacheTtl, async () =>
            {
                var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTimeZone);

                var query = ApplyFilter(VisibleAt(nowLocal), normalised);

                var totalItems = await query.CountAsync(ct);

                var items = await Sort(query, normalised.Sort)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(ItemPromotionMapping.ToResponseDto)
                    .ToListAsync(ct);

                return Page(items, page, pageSize, totalItems);
            });
        }

        /// <summary>
        /// Narrows a storefront query by search term, category and price range.
        /// </summary>
        /// <remarks>
        /// <c>Contains</c> translates to a <c>LIKE '%term%'</c>, which cannot use the
        /// name index. That is the right trade at this size — a shop's whole catalogue
        /// is hundreds of rows, and a MySQL full-text index would add a schema
        /// dependency for a scan that costs nothing yet. Worth revisiting if a
        /// catalogue reaches tens of thousands of rows.
        /// </remarks>
        private static IQueryable<ItemPromotion> ApplyFilter(
            IQueryable<ItemPromotion> query, PromotionFilterDto filter)
        {
            if (!string.IsNullOrEmpty(filter.Search))
                query = query.Where(p => p.Name.Contains(filter.Search));

            if (filter.CategoryId is int categoryId)
                query = query.Where(p => p.CategoryId == categoryId);

            if (filter.MinPrice is decimal min)
                query = query.Where(p => p.Price >= min);

            if (filter.MaxPrice is decimal max)
                query = query.Where(p => p.Price <= max);

            return query;
        }

        private static IQueryable<ItemPromotion> Sort(
            IQueryable<ItemPromotion> query, string? sort) => sort switch
            {
                PromotionSort.PriceAscending => query.OrderBy(p => p.Price).ThenBy(p => p.Id),
                PromotionSort.PriceDescending => query.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
                PromotionSort.Newest => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
                PromotionSort.Name => query.OrderBy(p => p.Name).ThenBy(p => p.Id),

                // Default: whatever expires soonest, so the grid leads with the
                // promotions a visitor has least time left to act on.
                _ => query.OrderBy(p => p.DateEnd).ThenBy(p => p.Id),
            };

        public async Task<List<PromotionStatusHistoryDto>?> GetHistoryAsync(
            int id, CancellationToken ct = default)
        {
            if (!await _context.ItemPromotions.AnyAsync(p => p.Id == id, ct))
                return null;

            return await _context.PromotionStatusHistory
                .AsNoTracking()
                .Where(h => h.PromotionId == id)
                .OrderBy(h => h.ChangedAt)
                .Select(h => new PromotionStatusHistoryDto
                {
                    FromStatus = h.FromStatus,
                    ToStatus = h.ToStatus,
                    ChangedByUserId = h.ChangedByUserId,
                    Reason = h.Reason,
                    ChangedAt = h.ChangedAt,
                })
                .ToListAsync(ct);
        }

        public Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
            _cache.GetOrSetAsync(CategoryScope, "all", CacheTtl, async () =>
                await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new CategoryDto { Id = c.Id, Name = c.Name })
                    .ToListAsync(ct));

        /// <summary>
        /// How many promotions point at an image file that is gone — the residue of
        /// uploads having had no volume before Phase 0.
        /// </summary>
        public Task<int> CountMissingImagesAsync(CancellationToken ct = default) =>
            _context.ItemPromotions
                .CountAsync(p => p.MediaAsset == null || p.MediaAsset.IsMissing, ct);

        // ===============================
        // HELPERS
        // ===============================

        private IQueryable<ItemPromotion> VisibleAt(DateTime now) =>
            _context.ItemPromotions
                .AsNoTracking()
                .Where(p =>
                    p.Status != PromotionStatus.Draft &&
                    p.Status != PromotionStatus.Archived &&
                    p.DateStart <= now &&
                    p.DateEnd >= now);

        private static string? ValidateWindowAndPrices(
            decimal price, decimal priceBefore, DateTime dateStart, DateTime dateEnd)
        {
            if (price >= priceBefore)
                return "Preço promocional deve ser menor que o preço original.";

            if (dateStart > dateEnd)
                return "Data inicial deve ser menor ou igual à data final.";

            return null;
        }

        /// <summary>
        /// Turns "publish or not" plus the window into a status, so callers never
        /// set an inconsistent one directly.
        /// </summary>
        private static string DeriveStatus(bool publish, DateTime start, DateTime end, DateTime now)
        {
            if (!publish)
                return PromotionStatus.Draft;

            if (now < start) return PromotionStatus.Scheduled;
            if (now > end) return PromotionStatus.Expired;

            return PromotionStatus.Active;
        }

        private static (int Page, int PageSize) ClampPaging(int page, int pageSize) =>
            (page <= 0 ? 1 : page, pageSize is <= 0 or > 50 ? 12 : pageSize);

        private static PagedResultDto<T> Page<T>(List<T> items, int page, int pageSize, int totalItems) =>
            new()
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                HasMore = page * pageSize < totalItems,
            };

        private async Task RecordTransitionAsync(
            ItemPromotion promotion, string? from, string to, int? userId, string reason,
            CancellationToken ct)
        {
            _context.PromotionStatusHistory.Add(new PromotionStatusHistory
            {
                PromotionId = promotion.Id,
                FromStatus = from,
                ToStatus = to,
                ChangedByUserId = userId,
                Reason = reason,
                ChangedAt = DateTime.UtcNow,
            });

            await _context.SaveChangesAsync(ct);
        }

        private async Task NotifyChangedAsync()
        {
            await _cache.InvalidateScopeAsync(PromotionScope);
            await _hub.Clients.All.SendAsync("PromotionsChanged");
        }
    }
}
