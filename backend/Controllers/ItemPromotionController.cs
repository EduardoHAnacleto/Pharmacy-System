using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PharmacyWorkerAPI.Data;
using PharmacyWorkerAPI.DTOs;
using PharmacyWorkerAPI.DTOs.ItemPromotion;
using PharmacyWorkerAPI.Hubs;
using PharmacyWorkerAPI.Models;
using PharmacyWorkerAPI.Services;
using PharmacyWorkerAPI.Utility;

namespace PharmacyWorkerAPI.Controllers
{
    [ApiController]
    [Route("api/item-promotions")]
    public class ItemPromotionController : ControllerBase
    {
        private const int DefaultMaxFileSizeBytes = 5 * 1024 * 1024;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly RedisService _redis;
        private readonly IHubContext<PromotionsHub> _hubContext;
        private readonly ILogger<ItemPromotionController> _logger;
        private readonly int _maxFileSizeBytes;

        public ItemPromotionController(
            AppDbContext context,
            IWebHostEnvironment environment,
            RedisService redis,
            IHubContext<PromotionsHub> hubContext,
            IConfiguration configuration,
            ILogger<ItemPromotionController> logger)
        {
            _context = context;
            _environment = environment;
            _redis = redis;
            _hubContext = hubContext;
            _logger = logger;
            _maxFileSizeBytes =
                configuration.GetValue<int?>("Uploads:MaxFileSizeBytes") ?? DefaultMaxFileSizeBytes;
        }

        // ===============================
        // CURRENT USER
        // ===============================

        /// <summary>Authenticated user's id. Write actions require [Authorize].</summary>
        private int CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        private string CurrentUserName => User.Identity?.Name ?? "unknown";

        // ===============================
        // PATHS
        // ===============================

        /// <summary>Web root, falling back to ./wwwroot when the host has not resolved one.</summary>
        private string WebRoot =>
            _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        /// <summary>Absolute, normalised directory that promotion images are confined to.</summary>
        private string UploadsRoot =>
            Path.GetFullPath(Path.Combine(WebRoot, "images", "promotions"));

        // ===============================
        // CREATE PROMOTION
        // ===============================
        [HttpPost]
        [Authorize(Roles = Roles.Admin)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] ItemPromotionCreateRequestDto dto)
        {
            // ---------- Basic validations ----------
            if (dto.Image == null || dto.Image.Length == 0)
                return BadRequest("Imagem é obrigatória.");

            if (dto.Image.Length > _maxFileSizeBytes)
                return BadRequest($"Imagem excede o tamanho máximo de {_maxFileSizeBytes / (1024 * 1024)} MB.");

            if (dto.Price >= dto.PriceBefore)
                return BadRequest("Preço promocional deve ser menor que o preço original.");

            if (dto.DateStart > dto.DateEnd)
                return BadRequest("Data inicial deve ser menor ou igual à data final.");

            // ---------- Image validation by content, not by client-supplied metadata ----------
            if (dto.Image.Length < ImageValidator.HeaderLength)
                return BadRequest("Formato de imagem inválido. Envie JPEG, PNG ou WebP.");

            var header = new byte[ImageValidator.HeaderLength];
            await using (var probe = dto.Image.OpenReadStream())
            {
                await probe.ReadExactlyAsync(header.AsMemory(0, header.Length));
            }

            var extension = ImageValidator.DetectExtension(header);
            if (extension == null)
                return BadRequest("Formato de imagem inválido. Envie JPEG, PNG ou WebP.");

            // ---------- Image upload ----------
            var uploadsFolder = UploadsRoot;
            Directory.CreateDirectory(uploadsFolder);

            // The extension comes from the detected format, never from the
            // client-supplied file name.
            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using (var stream = new FileStream(filePath, FileMode.CreateNew))
            {
                await dto.Image.CopyToAsync(stream);
            }

            var imageUrl = $"/images/promotions/{fileName}";

            // ---------- obj creation ----------
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

                // Audit fields come from the token, not from the request.
                CreatedByUserId = CurrentUserId,
                CreatedByUserName = CurrentUserName,
                CreatedAt = DateTime.UtcNow
            };

            _context.ItemPromotions.Add(promotion);
            await _context.SaveChangesAsync();

            // ---------- Response ----------
            var response = new ItemPromotionResponseDto
            {
                Id = promotion.Id,
                Name = promotion.Name,
                Price = promotion.Price,
                PriceBefore = promotion.PriceBefore,
                ImageUrl = promotion.ImagePath,

                DateStart = promotion.DateStart,
                DateEnd = promotion.DateEnd,

                IsActive = promotion.IsActive,
                CategoryId = promotion.CategoryId,
                ProductType = promotion.ProductType,
                CreatedByUserId = promotion.CreatedByUserId,
                CreatedByUserName = promotion.CreatedByUserName,
            };

            await _redis.InvalidateByPrefixAsync("item-promotions");
            await _hubContext.Clients.All.SendAsync("PromotionsChanged");

            return CreatedAtAction(nameof(GetById), new { id = promotion.Id }, response);
        }

        // ===============================
        // GET BY ID
        // ===============================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var cacheKey = $"item-promotions:id:{id}";

            var cached = await _redis.GetAsync<ItemPromotionResponseDto>(cacheKey);
            if (cached != null)
                return Ok(cached);

            var promotion = await _context.ItemPromotions.FindAsync(id);

            if (promotion == null)
                return NotFound();

            var response = new ItemPromotionResponseDto
            {
                Id = promotion.Id,
                Name = promotion.Name,
                Price = promotion.Price,
                PriceBefore = promotion.PriceBefore,
                ImageUrl = promotion.ImagePath,

                DateStart = promotion.DateStart,
                DateEnd = promotion.DateEnd,

                IsActive = promotion.IsActive,
                CategoryId = promotion.CategoryId,
                ProductType = promotion.ProductType,
                CreatedByUserId = promotion.CreatedByUserId,
                CreatedByUserName = promotion.CreatedByUserName
            };

            await _redis.SetAsync(cacheKey, response, CacheTtl);

            return Ok(response);
        }

        // ===============================
        // DELETE PROMOTION
        // ===============================
        [HttpDelete("{id:int}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var promotion = await _context.ItemPromotions.FindAsync(id);

            if (promotion == null)
                return NotFound();

            DeleteImageFile(promotion.ImagePath);

            _context.ItemPromotions.Remove(promotion);
            await _context.SaveChangesAsync();

            await _redis.InvalidateByPrefixAsync("item-promotions");
            await _hubContext.Clients.All.SendAsync("PromotionsChanged");
            return NoContent();
        }

        /// <summary>
        /// Deletes a promotion image, refusing any path that resolves outside the
        /// uploads directory. ImagePath comes from the database, but the value stored
        /// there is not a reason to hand an arbitrary path to File.Delete.
        /// </summary>
        private void DeleteImageFile(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return;

            var relative = imagePath
                .TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);

            var uploadsRoot = UploadsRoot;
            var resolved = Path.GetFullPath(Path.Combine(WebRoot, relative));

            var isContained = resolved.StartsWith(
                uploadsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);

            if (!isContained)
            {
                _logger.LogWarning(
                    "Refusing to delete {ImagePath}: resolves outside the uploads directory.",
                    imagePath);
                return;
            }

            if (System.IO.File.Exists(resolved))
                System.IO.File.Delete(resolved);
        }

        // ===============================
        // GET ALL PROMOTIONS ORDERED BY END DATE
        // ===============================
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var cacheKey = "item-promotions:all:by-end-date";

            var cached = await _redis.GetAsync<List<ItemPromotionResponseDto>>(cacheKey);
            if (cached != null)
                return Ok(cached);

            var promotions = await _context.ItemPromotions
                .AsNoTracking()
                .OrderBy(p => p.DateEnd)
                .Select(p => new ItemPromotionResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    PriceBefore = p.PriceBefore,
                    ImageUrl = p.ImagePath,

                    DateStart = p.DateStart,
                    DateEnd = p.DateEnd,

                    IsActive = p.IsActive,
                    CategoryId = p.CategoryId,
                    ProductType = p.ProductType,
                    CreatedByUserId = p.CreatedByUserId,
                    CreatedByUserName = p.CreatedByUserName
                })
                .ToListAsync();

            await _redis.SetAsync(cacheKey, promotions, CacheTtl);

            return Ok(promotions);
        }

        // ===============================
        // GET ACTIVE PROMOTIONS
        // ===============================
        [HttpGet("active/all")]
        public async Task<IActionResult> GetActive()
        {
            var cacheKey = "item-promotions:active";

            var cached = await _redis.GetAsync<List<ItemPromotionResponseDto>>(cacheKey);
            if (cached != null)
                return Ok(cached);

            var now = DateTime.UtcNow;

            var promotions = await _context.ItemPromotions
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.DateStart <= now &&
                    p.DateEnd >= now
                )
                .OrderBy(p => p.DateEnd)
                .Select(p => new ItemPromotionResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    PriceBefore = p.PriceBefore,
                    ImageUrl = p.ImagePath,

                    DateStart = p.DateStart,
                    DateEnd = p.DateEnd,

                    IsActive = p.IsActive,
                    CategoryId = p.CategoryId,
                    ProductType = p.ProductType,
                    CreatedByUserId = p.CreatedByUserId,
                    CreatedByUserName = p.CreatedByUserName
                })
                .ToListAsync();

            await _redis.SetAsync(cacheKey, promotions, CacheTtl);

            return Ok(promotions);
        }

        // ===============================
        // GET ALL PROMOTIONS
        // Filter by minimum CreatedAt
        // ===============================
        [HttpGet("created-after")]
        public async Task<IActionResult> GetAllCreatedAfter(
            [FromQuery] DateTime? minCreatedAt)
        {
            var cacheKey = $"item-promotions:created-after:{minCreatedAt:yyyyMMddHHmm}";

            var cached = await _redis.GetAsync<List<ItemPromotionResponseDto>>(cacheKey);
            if (cached != null)
                return Ok(cached);

            IQueryable<ItemPromotion> query = _context.ItemPromotions.AsNoTracking();

            // ---------- filter ----------
            if (minCreatedAt.HasValue)
            {
                query = query.Where(p => p.CreatedAt >= minCreatedAt.Value);
            }

            // ---------- ordering ----------
            query = query.OrderByDescending(p => p.CreatedAt);

            var promotions = await query
                .Select(p => new ItemPromotionResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    PriceBefore = p.PriceBefore,
                    ImageUrl = p.ImagePath,

                    DateStart = p.DateStart,
                    DateEnd = p.DateEnd,

                    IsActive = p.IsActive,
                    CategoryId = p.CategoryId,
                    ProductType = p.ProductType,
                    CreatedByUserId = p.CreatedByUserId,
                    CreatedByUserName = p.CreatedByUserName
                })
                .ToListAsync();

            await _redis.SetAsync(cacheKey, promotions, CacheTtl);

            return Ok(promotions);
        }

        // ===============================
        // GET ACTIVE PROMOTIONS (PAGED)
        // ===============================
        [HttpGet("active")]
        public async Task<IActionResult> GetActivePaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            string? timeZone = null)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 50) pageSize = 12;

            var userTimeZone = Utilities.GetTimeZone(timeZone, _logger);

            // The result set depends on the caller's time zone, so the cache key
            // must too — otherwise the first caller's window is served to everyone.
            var cacheKey =
                $"item-promotions:active:tz:{userTimeZone.Id}:page:{page}:size:{pageSize}";

            var cached = await _redis.GetAsync<PagedResultDto<ItemPromotionResponseDto>>(cacheKey);
            if (cached != null)
                return Ok(cached);

            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTimeZone);

            var query = _context.ItemPromotions
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.DateStart <= nowLocal &&
                    p.DateEnd >= nowLocal
                );

            var totalItems = await query.CountAsync();

            var promotions = await query
                .OrderBy(p => p.DateEnd)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ItemPromotionResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    PriceBefore = p.PriceBefore,
                    ImageUrl = p.ImagePath,
                    DateStart = p.DateStart,
                    DateEnd = p.DateEnd,
                    IsActive = p.IsActive,
                    CategoryId = p.CategoryId,
                    ProductType = p.ProductType,
                    CreatedByUserId = p.CreatedByUserId,
                    CreatedByUserName = p.CreatedByUserName,
                })
                .ToListAsync();

            var result = new PagedResultDto<ItemPromotionResponseDto>
            {
                Items = promotions,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                HasMore = (page * pageSize) < totalItems
            };

            await _redis.SetAsync(cacheKey, result, CacheTtl);

            return Ok(result);
        }

        // ===============================
        // GET CATEGORIES
        // ===============================
        [HttpGet("categories/all")]
        public async Task<IActionResult> GetAllCategories()
        {
            var cacheKey = "categories:all";

            var cached = await _redis.GetAsync<List<CategoryDto>>(cacheKey);
            if (cached != null)
                return Ok(cached);

            var categories = await _context.Categories
                .AsNoTracking()
                .Select(p => new CategoryDto
                {
                    Name = p.Name
                })
                .Distinct()
                .ToListAsync();

            await _redis.SetAsync(cacheKey, categories, CacheTtl);

            return Ok(categories);
        }
    }
}
