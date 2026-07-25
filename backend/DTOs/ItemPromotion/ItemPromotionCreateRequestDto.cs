using System.ComponentModel.DataAnnotations;

namespace PharmacyWorkerAPI.DTOs.ItemPromotion
{
    public class ItemPromotionCreateRequestDto
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
        public IFormFile Image { get; set; } = null!;

        [Required]
        public DateTime DateStart { get; set; }

        [Required]
        public DateTime DateEnd { get; set; }

        public bool IsActive { get; set; }

        [Range(1, int.MaxValue)]
        public int CategoryId { get; set; }

        [MaxLength(30)]
        public string ProductType { get; set; } = string.Empty;

        // CreatedByUserId and CreatedByUserName are deliberately absent: they are
        // read from the authenticated caller's token. Accepting them from the
        // request body made the audit trail whatever the client chose to send.
    }
}
