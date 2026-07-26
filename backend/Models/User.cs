using System.ComponentModel.DataAnnotations.Schema;

namespace Storefront.Api.Models
{
    /// <summary>
    /// An operator of the store's admin area. This is staff, not a customer:
    /// the system deliberately stores no customer accounts or personal data.
    /// </summary>
    public class User
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("username")]
        public string Username { get; set; } = null!;

        [Column("email")]
        public string? Email { get; set; }

        /// <summary>
        /// Encoded PBKDF2 digest, never a reversible or plaintext value.
        /// See <see cref="Services.Pbkdf2PasswordHasher"/> for the format.
        /// </summary>
        [Column("password_hash")]
        public string PasswordHash { get; set; } = null!;

        [Column("role")]
        public string Role { get; set; } = Roles.Admin;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("last_login_at")]
        public DateTime? LastLoginAt { get; set; }
    }

    /// <summary>Role names, matched against the JWT role claim.</summary>
    public static class Roles
    {
        public const string Admin = "Admin";
    }
}
