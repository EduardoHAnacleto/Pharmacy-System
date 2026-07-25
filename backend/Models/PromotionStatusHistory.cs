using System.ComponentModel.DataAnnotations.Schema;

namespace PharmacyWorkerAPI.Models
{
    /// <summary>
    /// One recorded status transition of a promotion.
    /// </summary>
    /// <remarks>
    /// Answers questions the old model could not: when a promotion actually went
    /// live, how long it ran, who retired it, and whether it expired on its own or
    /// was pulled early. Expiry used to be only a query filter, so the fact of it
    /// was never written down anywhere.
    /// </remarks>
    public class PromotionStatusHistory
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("promotion_id")]
        public int PromotionId { get; set; }

        public ItemPromotion? Promotion { get; set; }

        [Column("from_status")]
        public string? FromStatus { get; set; }

        [Column("to_status")]
        public string ToStatus { get; set; } = null!;

        /// <summary>Null when the maintenance job made the transition.</summary>
        [Column("changed_by_user_id")]
        public int? ChangedByUserId { get; set; }

        [Column("reason")]
        public string? Reason { get; set; }

        [Column("changed_at")]
        public DateTime ChangedAt { get; set; }
    }
}
