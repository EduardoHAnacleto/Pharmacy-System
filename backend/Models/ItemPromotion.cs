using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmacyWorkerAPI.Models
{
    public class ItemPromotion
    {
        [Column("id")]
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        public decimal PriceBefore { get; set; }
        public string ImagePath { get; set; } = null!; 
        public DateTime DateStart { get; set; }
        public DateTime DateEnd { get; set; }
        public bool IsActive { get; set; }

        // ===== RELATIONSHIPS =====
        [Column("category_id")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; } = null!;
        public string ProductType { get; set; } = null!;

        // ===== COMMON =====
        public int CreatedByUserId { get; set; }
        public string CreatedByUserName { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

    }
}
