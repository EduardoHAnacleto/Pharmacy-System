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
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly RedisService _redis;
        private readonly IHubContext<PromotionsHub> _hubContext;
        private readonly IConfiguration _configuration;

        public ItemPromotionController(
            AppDbContext context,
            IWebHostEnvironment environment,
            RedisService redis,
            IHubContext<PromotionsHub> hubContext,
            IConfiguration configuration)
        {
            _context = context;
            _environment = environment;
            _redis = redis;
            _hubContext = hubContext;

            var webRoot = _environment.WebRootPath
              ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            var uploadsPath = Path.Combine(webRoot, "images", "promotions");
            Directory.CreateDirectory(uploadsPath);
            _configuration = configuration;

        }

        // ===============================
        // CREATE PROMOTION
        // ===============================
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] ItemPromotionCreateRequestDto dto)
        {
            // ---------- Basic validations ----------
            if (dto.Image == null || dto.Image.Length == 0)
                return BadRequest("Imagem é obrigatória.");

            if (dto.Price >= dto.PriceBefore)
                return BadRequest("Preço promocional deve ser menor que o preço original.");

            if (dto.DateStart > dto.DateEnd)
                return BadRequest("Data inicial deve ser menor ou igual à data final.");

            // ---------- Image Upload  ----------
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(dto.Image.ContentType))
                return BadRequest("Formato de imagem inválido.");

            var uploadsFolder = Path.Combine(
                _environment.WebRootPath,
                "images",
                "promotions"
            );

            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.Image.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
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

                CreatedByUserId = dto.CreatedByUserId,
                CreatedByUserName = dto.CreatedByUserName,
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

            var publicBaseUrl = _configuration["PublicBaseUrl"];
            var promotion = await _context.ItemPromotions.FindAsync(id);

            if (promotion == null)
                return NotFound();

            var response = new ItemPromotionResponseDto
            {
                Id = promotion.Id,
                Name = promotion.Name,
                Price = promotion.Price,
                PriceBefore = promotion.PriceBefore,
                ImageUrl = $"{publicBaseUrl}{promotion.ImagePath}",

                DateStart = promotion.DateStart,
                DateEnd = promotion.DateEnd,

                IsActive = promotion.IsActive,
                CategoryId = promotion.CategoryId,
                ProductType = promotion.ProductType,
                CreatedByUserId = promotion.CreatedByUserId,
                CreatedByUserName = promotion.CreatedByUserName
            };

            await _redis.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));

            return Ok(response);
        }

        // ===============================
        // DELETE PROMOTION
        // ===============================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var promotion = await _context.ItemPromotions.FindAsync(id);

            if (promotion == null)
                return NotFound();

            // Remover imagem física
            if (!string.IsNullOrWhiteSpace(promotion.ImagePath))
            {
                var imagePath = Path.Combine(
                    _environment.WebRootPath,
                    promotion.ImagePath.TrimStart('/')
                        .Replace("/", Path.DirectorySeparatorChar.ToString())
                );

                if (System.IO.File.Exists(imagePath))
                    System.IO.File.Delete(imagePath);
            }

            _context.ItemPromotions.Remove(promotion);
            await _context.SaveChangesAsync();

            await _redis.InvalidateByPrefixAsync("item-promotions");
            await _hubContext.Clients.All.SendAsync("PromotionsChanged");
            return NoContent();
        }

        // ===============================
        // GET ALL PROMOTIONS ORDERED BY END DATE
        // ===============================
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var cacheKey = "item-promotions:all:by-end-date";

            var cached = await _redis. GetAsync<List<ItemPromotionResponseDto>>(cacheKey);
            if (cached != null)
                return Ok(cached);

            var publicBaseUrl = _configuration["PublicBaseUrl"];
            var promotions = await _context.ItemPromotions
                .OrderBy(p => p.DateEnd)
                .Select(p => new ItemPromotionResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    PriceBefore = p.PriceBefore,
                    ImageUrl = $"{publicBaseUrl}{p.ImagePath}",

                    DateStart = p.DateStart,
                    DateEnd = p.DateEnd,

                    IsActive = p.IsActive,
                    CategoryId = p.CategoryId,
                    ProductType = p.ProductType,
                    CreatedByUserId = p.CreatedByUserId,
                    CreatedByUserName = p.CreatedByUserName
                })
                .ToListAsync();

            await _redis.SetAsync(cacheKey, promotions, TimeSpan.FromMinutes(5));

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

            var publicBaseUrl = _configuration["PublicBaseUrl"];
            var promotions = await _context.ItemPromotions
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
                    ImageUrl = $"{publicBaseUrl}{p.ImagePath}",

                    DateStart = p.DateStart,
                    DateEnd = p.DateEnd,

                    IsActive = p.IsActive,
                    CategoryId = p.CategoryId,
                    ProductType = p.ProductType,
                    CreatedByUserId = p.CreatedByUserId,
                    CreatedByUserName = p.CreatedByUserName
                })
                .ToListAsync();

            await _redis.SetAsync(cacheKey, promotions, TimeSpan.FromMinutes(5));

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

            IQueryable<ItemPromotion> query = _context.ItemPromotions;

            // ---------- filtera ----------
            if (minCreatedAt.HasValue)
            {
                query = query.Where(p => p.CreatedAt >= minCreatedAt.Value);
            }

            // ---------- ordenation ----------
            query = query.OrderByDescending(p => p.CreatedAt);

            var publicBaseUrl = _configuration["PublicBaseUrl"];
            var promotions = await query
                .Select(p => new ItemPromotionResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    PriceBefore = p.PriceBefore,
                    ImageUrl = $"{publicBaseUrl}{p.ImagePath}",

                    DateStart = p.DateStart,
                    DateEnd = p.DateEnd,

                    IsActive = p.IsActive,
                    CategoryId = p.CategoryId,
                    ProductType = p.ProductType,
                    CreatedByUserId = p.CreatedByUserId,
                    CreatedByUserName = p.CreatedByUserName
                })
                .ToListAsync();

            await _redis.SetAsync(cacheKey, promotions, TimeSpan.FromMinutes(5));

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

            var userTimeZone = Utilities.GetTimeZone(timeZone);
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTimeZone);


            var cacheKey = $"item-promotions:active:page:{page}:size:{pageSize}";

            var cached = await _redis.GetAsync<PagedResultDto<ItemPromotionResponseDto>>(cacheKey);
            if (cached != null)
                return Ok(cached);

            var query = _context.ItemPromotions
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.DateStart <= nowLocal &&
                    p.DateEnd >= nowLocal
                );

            var totalItems = await query.CountAsync();

            var publicBaseUrl = _configuration["PublicBaseUrl"];
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
                    ImageUrl = $"{publicBaseUrl}{p.ImagePath}",
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

            await _redis.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));

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

            var now = DateTime.UtcNow;

            var categories = await _context.Categories
                .Select(p => new CategoryDto
                {
                    Name = p.Name
                })
                .Distinct()
                .ToListAsync();          

            await _redis.SetAsync(cacheKey, categories, TimeSpan.FromMinutes(5));

            return Ok(categories);
        }
    }
}
