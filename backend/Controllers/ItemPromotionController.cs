using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyWorkerAPI.DTOs;
using PharmacyWorkerAPI.DTOs.ItemPromotion;
using PharmacyWorkerAPI.Models;
using PharmacyWorkerAPI.Services;

namespace PharmacyWorkerAPI.Controllers
{
    /// <summary>
    /// Promotion endpoints. Reads are public — this is a storefront; writes require
    /// an Admin token.
    /// </summary>
    [ApiController]
    [Route("api/v1/item-promotions")]
    public class ItemPromotionController : ControllerBase
    {
        private readonly IPromotionService _promotions;
        private readonly IMediaAssetService _media;

        public ItemPromotionController(IPromotionService promotions, IMediaAssetService media)
        {
            _promotions = promotions;
            _media = media;
        }

        // ===============================
        // CURRENT USER
        // ===============================
        private int CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        private string CurrentUserName => User.Identity?.Name ?? "unknown";

        // ===============================
        // CREATE
        // ===============================
        [HttpPost]
        [Authorize(Roles = Roles.Admin)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create(
            [FromForm] ItemPromotionCreateRequestDto dto, CancellationToken ct)
        {
            var result = await _promotions.CreateAsync(dto, CurrentUserId, CurrentUserName, ct);

            return result.Succeeded
                ? CreatedAtAction(nameof(GetById), new { id = result.Promotion!.Id }, result.Promotion)
                : Resolve(result);
        }

        // ===============================
        // UPDATE
        // ===============================
        [HttpPut("{id:int}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Update(
            int id, [FromBody] ItemPromotionUpdateRequestDto dto, CancellationToken ct)
        {
            var result = await _promotions.UpdateAsync(id, dto, CurrentUserId, ct);

            return result.Succeeded ? Ok(result.Promotion) : Resolve(result);
        }

        // ===============================
        // ARCHIVE
        // ===============================

        /// <summary>
        /// Retires a promotion without destroying it. Replaces the old DELETE, which
        /// removed the row and its image file, making every finished campaign
        /// unrecoverable and unrepeatable.
        /// </summary>
        [HttpPatch("{id:int}/archive")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Archive(int id, CancellationToken ct)
        {
            var result = await _promotions.ArchiveAsync(id, CurrentUserId, ct);

            return result.Succeeded ? Ok(result.Promotion) : Resolve(result);
        }

        // ===============================
        // REACTIVATE
        // ===============================

        /// <summary>
        /// Runs an archived promotion again under a new window, reusing its image
        /// and recording which promotion it came from.
        /// </summary>
        [HttpPost("{id:int}/reactivate")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Reactivate(
            int id, [FromBody] ReactivatePromotionRequestDto dto, CancellationToken ct)
        {
            var result = await _promotions.ReactivateAsync(id, dto, CurrentUserId, CurrentUserName, ct);

            return result.Succeeded
                ? CreatedAtAction(nameof(GetById), new { id = result.Promotion!.Id }, result.Promotion)
                : Resolve(result);
        }

        // ===============================
        // GET BY ID
        // ===============================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var promotion = await _promotions.GetByIdAsync(id, ct);

            return promotion == null ? NotFound() : Ok(promotion);
        }

        // ===============================
        // HISTORY
        // ===============================
        [HttpGet("{id:int}/history")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetHistory(int id, CancellationToken ct)
        {
            var history = await _promotions.GetHistoryAsync(id, ct);

            return history == null ? NotFound() : Ok(history);
        }

        // ===============================
        // LIST BY STATUS
        // ===============================

        /// <summary>
        /// Admin listing. Pass <c>status=Archived</c> for the library of past
        /// promotions available to reactivate.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetByStatus(
            CancellationToken ct,
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12)
        {
            if (status != null && !PromotionStatus.IsValid(status))
                return BadRequest("Status inválido.");

            return Ok(await _promotions.GetByStatusAsync(status, page, pageSize, ct));
        }

        // ===============================
        // ADMIN SUMMARY
        // ===============================
        [HttpGet("missing-images/count")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> CountMissingImages(CancellationToken ct) =>
            Ok(new { count = await _promotions.CountMissingImagesAsync(ct) });

        // ===============================
        // MEDIA LIBRARY
        // ===============================
        [HttpGet("media-assets")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetMediaAssets(
            CancellationToken ct,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 24)
        {
            if (page <= 0) page = 1;
            if (pageSize is <= 0 or > 100) pageSize = 24;

            var assets = await _media.ListAsync(page, pageSize, ct);
            var totalItems = await _media.CountAsync(ct);

            return Ok(new PagedResultDto<MediaAssetDto>
            {
                Items = assets.ConvertAll(a => new MediaAssetDto
                {
                    Id = a.Id,
                    Url = a.FilePath,
                    MimeType = a.MimeType,
                    ByteSize = a.ByteSize,
                    IsMissing = a.IsMissing,
                    CreatedAt = a.CreatedAt,
                }),
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                HasMore = page * pageSize < totalItems,
            });
        }

        // ===============================
        // STOREFRONT READS
        // ===============================
        [HttpGet("all")]
        public async Task<IActionResult> GetAll(CancellationToken ct) =>
            Ok(await _promotions.GetAllAsync(ct));

        [HttpGet("active/all")]
        public async Task<IActionResult> GetActive(CancellationToken ct) =>
            Ok(await _promotions.GetActiveAsync(ct));

        [HttpGet("created-after")]
        public async Task<IActionResult> GetAllCreatedAfter(
            [FromQuery] DateTime? minCreatedAt, CancellationToken ct) =>
            Ok(await _promotions.GetCreatedAfterAsync(minCreatedAt, ct));

        [HttpGet("active")]
        public async Task<IActionResult> GetActivePaged(
            CancellationToken ct,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] string? timeZone = null) =>
            Ok(await _promotions.GetActivePagedAsync(page, pageSize, timeZone, ct));

        [HttpGet("categories/all")]
        public async Task<IActionResult> GetAllCategories(CancellationToken ct) =>
            Ok(await _promotions.GetCategoriesAsync(ct));

        // ===============================
        // RESULT MAPPING
        // ===============================
        private IActionResult Resolve(PromotionResult result) =>
            result.NotFound ? NotFound() : BadRequest(result.Error);
    }
}
