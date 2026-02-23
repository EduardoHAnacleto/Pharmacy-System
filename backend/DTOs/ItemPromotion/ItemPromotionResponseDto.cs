using Microsoft.AspNetCore.Mvc;
using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.DTOs.ItemPromotion
{
    [Consumes("multipart/form-data")]
    public class ItemPromotionResponseDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public decimal PriceBefore { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        public DateTime DateStart { get; set; }
        public DateTime DateEnd { get; set; }

        public bool IsActive { get; set; }

        public int CategoryId { get; set; }

        public string ProductType { get; set; } = string.Empty;

        public int CreatedByUserId { get; set; }

        public string CreatedByUserName { get; set; } = null!;
    }
}
