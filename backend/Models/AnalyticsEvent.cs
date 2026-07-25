using System.ComponentModel.DataAnnotations.Schema;

namespace PharmacyWorkerAPI.Models
{
    /// <summary>Storefront event types. Anything not listed is rejected at ingestion.</summary>
    public static class AnalyticsEventType
    {
        public const string PromotionView = "promotion_view";
        public const string AddToCart = "add_to_cart";
        public const string CartView = "cart_view";
        public const string CheckoutStarted = "checkout_started";
        public const string WhatsAppClick = "whatsapp_click";

        public static readonly string[] All =
            [PromotionView, AddToCart, CartView, CheckoutStarted, WhatsAppClick];

        public static bool IsValid(string type) => All.Contains(type);
    }

    /// <summary>
    /// One raw storefront interaction.
    /// </summary>
    /// <remarks>
    /// Deliberately carries no personal data: no IP address, no user agent, no
    /// account, and nothing that survives the visit. What is recorded is what
    /// happened to a promotion, not who it happened to.
    /// <para>
    /// <see cref="SessionKey"/> is a random value the browser keeps in
    /// sessionStorage — it dies with the tab, is never written to a cookie, and is
    /// discarded when the daily rollup runs. It exists only so that "how many of
    /// the visits that saw this promotion added it to a cart" is answerable at all;
    /// without some per-visit correlation there is no funnel, only counters.
    /// </para>
    /// </remarks>
    public class AnalyticsEvent
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("event_type")]
        public string EventType { get; set; } = null!;

        [Column("promotion_id")]
        public int? PromotionId { get; set; }

        /// <summary>Ephemeral, per-visit, non-identifying. Null is acceptable.</summary>
        [Column("session_key")]
        public string? SessionKey { get; set; }

        [Column("occurred_at")]
        public DateTime OccurredAt { get; set; }
    }

    /// <summary>
    /// Aggregated daily counts, kept indefinitely.
    /// </summary>
    /// <remarks>
    /// The rollup is what makes long-term reporting affordable and privacy-safe:
    /// raw rows are purged after a short retention window, while these counts
    /// survive — including for promotions that have since been archived, which is
    /// what makes a reactivation decision an informed one.
    /// </remarks>
    public class AnalyticsDaily
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("stat_date")]
        public DateOnly StatDate { get; set; }

        [Column("event_type")]
        public string EventType { get; set; } = null!;

        [Column("promotion_id")]
        public int? PromotionId { get; set; }

        [Column("event_count")]
        public int EventCount { get; set; }

        /// <summary>Distinct visits, computed before the session keys are discarded.</summary>
        [Column("unique_sessions")]
        public int UniqueSessions { get; set; }
    }
}
