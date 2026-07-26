using System.ComponentModel.DataAnnotations.Schema;

namespace Storefront.Api.Models
{
    /// <summary>
    /// A recorded order, without the customer.
    /// </summary>
    /// <remarks>
    /// Everything needed to report on sales — what, how many, for how much, when,
    /// pickup or delivery — and nothing that identifies a person. Name, phone,
    /// national ID, postcode and street address stay where they already were:
    /// in the visitor's own browser and in the WhatsApp conversation. The endpoint
    /// that creates this row does not accept those fields at all.
    /// <para>
    /// There is no status. The system cannot know whether an order was fulfilled,
    /// because that happens in a chat it has no access to; recording "order
    /// started" is honest, while inventing a lifecycle nobody updates would
    /// produce confident, wrong reports.
    /// </para>
    /// </remarks>
    public class Order
    {
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// Short human reference, also sent in the WhatsApp message so the operator
        /// can match a conversation to this row without the system knowing who it is.
        /// </summary>
        [Column("order_number")]
        public string OrderNumber { get; set; } = null!;

        /// <summary>pickup or delivery.</summary>
        [Column("fulfillment_type")]
        public string FulfillmentType { get; set; } = null!;

        /// <summary>Declared by the customer, never processed. No payment data is stored.</summary>
        [Column("payment_method")]
        public string? PaymentMethod { get; set; }

        /// <summary>City only — useful for coverage analysis, identifies nobody.</summary>
        [Column("delivery_city")]
        public string? DeliveryCity { get; set; }

        [Column("currency")]
        public string Currency { get; set; } = "BRL";

        [Column("items_subtotal")]
        public decimal ItemsSubtotal { get; set; }

        [Column("delivery_fee")]
        public decimal DeliveryFee { get; set; }

        [Column("total")]
        public decimal Total { get; set; }

        [Column("item_count")]
        public int ItemCount { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        public List<OrderItem> Items { get; set; } = [];
    }

    /// <summary>
    /// A line of an order, with the name and price frozen as they were.
    /// </summary>
    /// <remarks>
    /// The snapshots are not redundancy. Without them, renaming a product or
    /// repricing it would silently rewrite history, and last month's revenue
    /// report would change.
    /// </remarks>
    public class OrderItem
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("order_id")]
        public int OrderId { get; set; }

        public Order? Order { get; set; }

        /// <summary>Nullable: the promotion may later be purged, the line stays.</summary>
        [Column("promotion_id")]
        public int? PromotionId { get; set; }

        [Column("name_snapshot")]
        public string NameSnapshot { get; set; } = null!;

        [Column("unit_price_snapshot")]
        public decimal UnitPriceSnapshot { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("line_total")]
        public decimal LineTotal { get; set; }
    }
}
