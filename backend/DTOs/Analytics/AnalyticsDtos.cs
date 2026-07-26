using System.ComponentModel.DataAnnotations;

namespace Storefront.Api.DTOs.Analytics
{
    // ===============================
    // INGESTION
    // ===============================

    public class TrackEventDto
    {
        [Required]
        [MaxLength(30)]
        public string EventType { get; set; } = string.Empty;

        public int? PromotionId { get; set; }

        /// <summary>
        /// Ephemeral per-visit key from sessionStorage. Optional — events without
        /// one still count, they just cannot contribute to funnel rates.
        /// </summary>
        [MaxLength(32)]
        public string? SessionKey { get; set; }
    }

    public class TrackEventBatchDto
    {
        /// <summary>
        /// Batched so a scrolling storefront does not issue one request per card.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public List<TrackEventDto> Events { get; set; } = [];
    }

    // ===============================
    // ORDERS
    // ===============================

    public class CreateOrderItemDto
    {
        public int? PromotionId { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Range(0.01, 99_999_999.99)]
        public decimal UnitPrice { get; set; }

        [Range(1, 999)]
        public int Quantity { get; set; }
    }

    /// <summary>
    /// What the storefront reports about an order.
    /// </summary>
    /// <remarks>
    /// There is deliberately no field for a name, phone, national ID, postcode or
    /// street. Those are collected by the checkout form, kept in the visitor's
    /// browser and sent in the WhatsApp message — they are not accepted here, so
    /// they cannot reach the database even by mistake.
    /// </remarks>
    public class CreateOrderDto
    {
        [Required]
        [MaxLength(20)]
        public string FulfillmentType { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PaymentMethod { get; set; }

        /// <summary>City only, for coverage analysis.</summary>
        [MaxLength(100)]
        public string? DeliveryCity { get; set; }

        [Range(0, 99_999_999.99)]
        public decimal DeliveryFee { get; set; }

        [Required]
        [MinLength(1)]
        [MaxLength(100)]
        public List<CreateOrderItemDto> Items { get; set; } = [];
    }

    public class OrderCreatedDto
    {
        public string OrderNumber { get; set; } = string.Empty;

        public decimal Total { get; set; }
    }

    // ===============================
    // REPORTING
    // ===============================

    public class FunnelDto
    {
        public int PromotionViews { get; set; }
        public int AddToCart { get; set; }
        public int CheckoutStarted { get; set; }
        public int WhatsAppClicks { get; set; }
        public int Orders { get; set; }

        /// <summary>Share of visits that saw something and put it in a cart.</summary>
        public double ViewToCartRate { get; set; }

        public double CartToCheckoutRate { get; set; }

        public double CheckoutToHandoffRate { get; set; }
    }

    public class PromotionPerformanceDto
    {
        public int PromotionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? SourcePromotionId { get; set; }

        public int Views { get; set; }
        public int AddToCart { get; set; }
        public int OrderedQuantity { get; set; }
        public decimal Revenue { get; set; }

        /// <summary>
        /// Views converted to cart additions. A high view count with a low rate is
        /// the signal worth acting on: the item is wanted but something — price or
        /// photo — is stopping people.
        /// </summary>
        public double ConversionRate { get; set; }
    }

    public class DailyPointDto
    {
        public DateOnly Date { get; set; }
        public int Count { get; set; }
    }

    public class OperationalAlertDto
    {
        public string Kind { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? PromotionId { get; set; }
    }

    public class SalesSummaryDto
    {
        public int Orders { get; set; }
        public decimal Revenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int PickupOrders { get; set; }
        public int DeliveryOrders { get; set; }
    }
}
