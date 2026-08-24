using System.ComponentModel.DataAnnotations.Schema;

namespace Storefront.Api.Models
{
    public class ItemPromotion
    {
        [Column("id")]
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        /// <summary>
        /// The original price, when there is one. Null means the item is simply
        /// sold at <see cref="Price"/>, which the storefront renders without a
        /// strikethrough.
        /// </summary>
        public decimal? PriceBefore { get; set; }

        /// <summary>
        /// Relative URL actually served for this promotion. Equal to the linked
        /// asset's path once <see cref="MediaAssetId"/> is set; kept on the row so
        /// promotions whose file was lost before uploads had a volume still render
        /// a URL rather than breaking the projection.
        /// </summary>
        public string ImagePath { get; set; } = null!;

        public DateTime DateStart { get; set; }
        public DateTime DateEnd { get; set; }

        /// <summary>
        /// Whether this item may only be dispensed against a prescription.
        /// </summary>
        /// <remarks>
        /// The catalogue had no way to say it, so a shelf of antibiotics carried the
        /// same "Adicionar" button as a bar of soap. This does not attempt to enforce
        /// anything — the order still completes over WhatsApp, where a pharmacist
        /// handles it — but it lets the storefront set the right expectation before a
        /// customer builds a basket they cannot collect without a doctor.
        /// <para>
        /// Defaults to false so every existing row keeps its current meaning: the
        /// migration must not silently mark a shampoo as prescription-only.
        /// </para>
        /// </remarks>
        [Column("requires_prescription")]
        public bool RequiresPrescription { get; set; }

        // ===== LIFECYCLE =====

        /// <summary>One of the constants on <see cref="PromotionStatus"/>.</summary>
        [Column("status")]
        public string Status { get; set; } = PromotionStatus.Draft;

        [Column("archived_at")]
        public DateTime? ArchivedAt { get; set; }

        [Column("archived_by_user_id")]
        public int? ArchivedByUserId { get; set; }

        // ===== MEDIA =====

        /// <summary>
        /// Nullable on purpose: a promotion migrated from before this table existed
        /// may point at a file that is no longer on disk.
        /// </summary>
        [Column("media_asset_id")]
        public int? MediaAssetId { get; set; }

        public MediaAsset? MediaAsset { get; set; }

        // ===== LINEAGE =====

        /// <summary>
        /// The archived promotion this one was reactivated from. Turns a repeated
        /// campaign into a traceable chain, so earlier runs can be compared.
        /// </summary>
        [Column("source_promotion_id")]
        public int? SourcePromotionId { get; set; }

        public ItemPromotion? SourcePromotion { get; set; }

        // ===== RELATIONSHIPS =====
        [Column("category_id")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        public string ProductType { get; set; } = null!;

        // ===== COMMON =====
        public int CreatedByUserId { get; set; }
        public string CreatedByUserName { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Whether the storefront may show this promotion at <paramref name="now"/>.
        /// </summary>
        public bool IsVisibleAt(DateTime now) =>
            PromotionStatus.Publishable.Contains(Status)
            && DateStart <= now
            && DateEnd >= now;
    }
}
