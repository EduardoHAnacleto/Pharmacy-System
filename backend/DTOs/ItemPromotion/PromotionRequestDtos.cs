using System.ComponentModel.DataAnnotations;

namespace Storefront.Api.DTOs.ItemPromotion
{
    /// <summary>Fields shared by create and update.</summary>
    public abstract class PromotionWriteRequestDto
    {
        // Lengths mirror the column definitions. Without them oversized input
        // reaches the database and comes back as a 500 instead of a 400.
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // decimal(10,2) allows up to 99,999,999.99. Negative prices were accepted
        // before: only Price < PriceBefore was ever checked.
        [Range(0.01, 99_999_999.99)]
        public decimal Price { get; set; }

        [Range(0.01, 99_999_999.99)]
        public decimal PriceBefore { get; set; }

        [Required]
        public DateTime DateStart { get; set; }

        [Required]
        public DateTime DateEnd { get; set; }

        /// <summary>
        /// Whether to publish. The concrete status — Scheduled, Active or Expired —
        /// is derived from the window, so a caller cannot set one that contradicts
        /// the dates.
        /// </summary>
        public bool Publish { get; set; } = true;

        [Range(1, int.MaxValue)]
        public int CategoryId { get; set; }

        [MaxLength(30)]
        public string ProductType { get; set; } = string.Empty;

        // CreatedByUserId and CreatedByUserName are deliberately absent: they are
        // read from the authenticated caller's token. Accepting them from the
        // request body made the audit trail whatever the client chose to send.
    }

    public class ItemPromotionCreateRequestDto : PromotionWriteRequestDto
    {
        [Required]
        public IFormFile Image { get; set; } = null!;
    }

    /// <summary>
    /// Update carries no image: replacing artwork means uploading a new promotion
    /// or reactivating from the media library, which keeps assets reusable.
    /// </summary>
    public class ItemPromotionUpdateRequestDto : PromotionWriteRequestDto
    {
    }

    /// <summary>
    /// Reactivation needs only a new window; everything else defaults to the
    /// archived promotion's values, including its image.
    /// </summary>
    public class ReactivatePromotionRequestDto
    {
        [Required]
        public DateTime DateStart { get; set; }

        [Required]
        public DateTime DateEnd { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        [Range(0.01, 99_999_999.99)]
        public decimal? Price { get; set; }

        [Range(0.01, 99_999_999.99)]
        public decimal? PriceBefore { get; set; }

        public bool Publish { get; set; } = true;
    }

    public class PromotionStatusHistoryDto
    {
        public string? FromStatus { get; set; }

        public string ToStatus { get; set; } = string.Empty;

        public int? ChangedByUserId { get; set; }

        public string? Reason { get; set; }

        public DateTime ChangedAt { get; set; }
    }

    public class MediaAssetDto
    {
        public int Id { get; set; }

        public string Url { get; set; } = string.Empty;

        public string MimeType { get; set; } = string.Empty;

        public int ByteSize { get; set; }

        public bool IsMissing { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
