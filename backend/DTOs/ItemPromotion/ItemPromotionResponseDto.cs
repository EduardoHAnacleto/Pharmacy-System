namespace Storefront.Api.DTOs.ItemPromotion
{
    public class ItemPromotionResponseDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public decimal PriceBefore { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        public DateTime DateStart { get; set; }
        public DateTime DateEnd { get; set; }

        /// <summary>
        /// Draft, Scheduled, Active, Expired or Archived. Replaces the isActive
        /// boolean, which collapsed four distinct states into false.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        public DateTime? ArchivedAt { get; set; }

        /// <summary>True when the image file behind this promotion is known to be gone.</summary>
        public bool ImageMissing { get; set; }

        /// <summary>Set when this promotion was reactivated from an archived one.</summary>
        public int? SourcePromotionId { get; set; }

        public int CategoryId { get; set; }

        public string ProductType { get; set; } = string.Empty;

        public int CreatedByUserId { get; set; }

        public string CreatedByUserName { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
