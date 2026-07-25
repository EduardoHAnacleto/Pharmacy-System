using System.ComponentModel.DataAnnotations.Schema;

namespace PharmacyWorkerAPI.Models
{
    /// <summary>
    /// A long-lived credential exchanged for short-lived access tokens.
    /// </summary>
    /// <remarks>
    /// Only a SHA-256 digest of the token is stored. A database dump therefore
    /// cannot be replayed as a set of valid sessions, which is the same reason
    /// passwords are not stored in a recoverable form.
    /// </remarks>
    public class RefreshToken
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        public User? User { get; set; }

        [Column("token_hash")]
        public string TokenHash { get; set; } = null!;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        /// <summary>Set when the token is rotated or explicitly logged out.</summary>
        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }

        public bool IsActive(DateTime utcNow) => RevokedAt == null && ExpiresAt > utcNow;
    }
}
