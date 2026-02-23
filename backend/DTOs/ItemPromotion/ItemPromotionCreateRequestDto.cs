using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.DTOs.ItemPromotion
{
    public class ItemPromotionCreateRequestDto
    {
        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public decimal PriceBefore { get; set; }

        public IFormFile Image { get; set; } = null!;

        public DateTime DateStart { get; set; }
        public DateTime DateEnd { get; set; }

        public bool IsActive { get; set; }

        public int CategoryId { get; set; }

        public string ProductType { get; set; } = string.Empty;

        public int CreatedByUserId { get; set; }

        public string CreatedByUserName { get; set; } = null!;
    }
}
