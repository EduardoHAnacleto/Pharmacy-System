using System.ComponentModel.DataAnnotations.Schema;

namespace Storefront.Api.Models
{
    /// <summary>
    /// A record of an administrative action.
    /// </summary>
    /// <remarks>
    /// Only trustworthy because the actor comes from the authenticated token. When
    /// promotions accepted CreatedByUserId from the request body, any caller could
    /// write any name into the trail.
    /// </remarks>
    public class AuditLogEntry
    {
        [Column("id")]
        public long Id { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("user_name")]
        public string? UserName { get; set; }

        /// <summary>create, update, archive, reactivate, delete, login.</summary>
        [Column("action")]
        public string Action { get; set; } = null!;

        [Column("entity_type")]
        public string EntityType { get; set; } = null!;

        [Column("entity_id")]
        public int? EntityId { get; set; }

        /// <summary>Short JSON summary of what changed. Never holds credentials.</summary>
        [Column("changes")]
        public string? Changes { get; set; }

        [Column("occurred_at")]
        public DateTime OccurredAt { get; set; }
    }
}
