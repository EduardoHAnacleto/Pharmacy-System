using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PharmacyWorkerAPI.Data;
using PharmacyWorkerAPI.DTOs;
using PharmacyWorkerAPI.DTOs.ItemPromotion;
using PharmacyWorkerAPI.Hubs;
using PharmacyWorkerAPI.Mapping;
using PharmacyWorkerAPI.Models;
using PharmacyWorkerAPI.Utility;

namespace PharmacyWorkerAPI.Services
{
    /// <summary>Outcome of a create attempt, distinguishing rejection from failure.</summary>
    public record CreatePromotionResult(
        bool Succeeded,
        ItemPromotionResponseDto? Promotion = null,
        string? Error = null)
    {
        public static CreatePromotionResult Rejected(string error) => new(false, Error: error);
    }

    public interface IPromotionService
    {
        Task<CreatePromotionResult> CreateAsync(
            ItemPromotionCreateRequestDto dto, int userId, string userName, CancellationToken ct = default);

        Task<ItemPromotionResponseDto?> GetByIdAsync(int id, CancellationToken ct = default);

        Task<bool> DeleteAsync(int id, CancellationToken ct = default);

        Task<List<ItemPromotionResponseDto>> GetAllAsync(CancellationToken ct = default);

        Task<List<ItemPromotionResponseDto>> GetActiveAsync(CancellationToken ct = default);

        Task<List<ItemPromotionResponseDto>> GetCreatedAfterAsync(
            DateTime? minCreatedAt, CancellationToken ct = default);

        Task<PagedResultDto<ItemPromotionResponseDto>> GetActivePagedAsync(
            int page, int pageSize, string? timeZone, CancellationToken ct = default);

        Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default);
    }

    public class PromotionService : IPromotionService
    {
        /// <summary>Cache scope invalidated whenever a promotion changes.</summary>
        private const string PromotionScope = "item-promotions";
        private const string CategoryScope = "categories";

        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        private readonly AppDbContext _context;
        private readonly RedisService _cache;
        private readonly IPromotionImageStorage _images;
        private readonly IHubContext<PromotionsHub> _hub;
        private readonly ILogger<PromotionService> _logger;

        public PromotionService(
            AppDbContext context,
            RedisService cache,
            IPromotionImageStorage images,
            IHubContext<PromotionsHub> hub,
            ILogger<PromotionService> logger)
        {
            _context = context;
            _cache = cache;
            _images = images;
            _hub = hub;
            _logger = logger;
        }

        // ===============================
        // CREATE
        // ===============================
        public async Task<CreatePromotionResult> CreateAsync(
            ItemPromotionCreateRequestDto dto, int userId, string userName, CancellationToken ct = default)
        {
            if (dto.Image == null || dto.Image.Length == 0)
                return CreatePromotionResult.Rejected("Imagem é obrigatória.");

            if (dto.Price >= dto.PriceBefore)
                return CreatePromotionResult.Rejected(
                    "Preço promocional deve ser menor que o preço original.");

            if (dto.DateStart > dto.DateEnd)
                return CreatePromotionResult.Rejected(
                    "Data inicial deve ser menor ou igual à data final.");

            if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId, ct))
                return CreatePromotionResult.Rejected("Categoria inválida.");

            var imageUrl = await _images.SaveAsync(dto.Image, ct);
            if (imageUrl == null)
                return CreatePromotionResult.Rejected(
                    "Formato de imagem inválido. Envie JPEG, PNG ou WebP.");

            var promotion = new ItemPromotion
            {
                Name = dto.Name,
                Price = dto.Price,
                PriceBefore = dto.PriceBefore,
                ImagePath = imageUrl,

                DateStart = dto.DateStart,
                DateEnd = dto.DateEnd,

                IsActive = dto.IsActive,
                CategoryId = dto.CategoryId,
                ProductType = dto.ProductType,

                // Audit fields come from the authenticated caller, never the request.
                CreatedByUserId = userId,
                CreatedByUserName = userName,
                CreatedAt = DateTime.UtcNow,
            };

            _context.ItemPromotions.Add(promotion);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Do not leave an orphaned file behind when the row never landed.
                _images.Delete(imageUrl);
                throw;
            }

            await NotifyChangedAsync();

            return new CreatePromotionResult(true, promotion.ToDto());
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
                    .OrderBy(p => p.DateEnd)
                    .Select(ItemPromotionMapping.ToResponseDto)
                    .ToListAsync(ct));

        public Task<List<ItemPromotionResponseDto>> GetActiveAsync(CancellationToken ct = default) =>
            _cache.GetOrSetAsync(PromotionScope, "active:all", CacheTtl, async () =>
            {
                var now = DateTime.UtcNow;

                return await _context.ItemPromotions
                    .AsNoTracking()
                    .Where(p => p.IsActive && p.DateStart <= now && p.DateEnd >= now)
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
            int page, int pageSize, string? timeZone, CancellationToken ct = default)
        {
            if (page <= 0) page = 1;
            if (pageSize is <= 0 or > 50) pageSize = 12;

            var userTimeZone = Utilities.GetTimeZone(timeZone, _logger);

            // The window depends on the caller's time zone, so the key must include
            // it — otherwise the first caller's result is served to everyone.
            var key = $"active:tz:{userTimeZone.Id}:page:{page}:size:{pageSize}";

            return _cache.GetOrSetAsync(PromotionScope, key, CacheTtl, async () =>
            {
                var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTimeZone);

                var query = _context.ItemPromotions
                    .AsNoTracking()
                    .Where(p => p.IsActive && p.DateStart <= nowLocal && p.DateEnd >= nowLocal);

                var totalItems = await query.CountAsync(ct);

                var items = await query
                    .OrderBy(p => p.DateEnd)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(ItemPromotionMapping.ToResponseDto)
                    .ToListAsync(ct);

                return new PagedResultDto<ItemPromotionResponseDto>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = totalItems,
                    HasMore = page * pageSize < totalItems,
                };
            });
        }

        public Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default) =>
            _cache.GetOrSetAsync(CategoryScope, "all", CacheTtl, async () =>
                await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new CategoryDto { Id = c.Id, Name = c.Name })
                    .ToListAsync(ct));

        // ===============================
        // DELETE
        // ===============================
        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var promotion = await _context.ItemPromotions.FindAsync([id], ct);

            if (promotion == null)
                return false;

            _images.Delete(promotion.ImagePath);

            _context.ItemPromotions.Remove(promotion);
            await _context.SaveChangesAsync(ct);

            await NotifyChangedAsync();

            return true;
        }

        private async Task NotifyChangedAsync()
        {
            await _cache.InvalidateScopeAsync(PromotionScope);
            await _hub.Clients.All.SendAsync("PromotionsChanged");
        }
    }
}
