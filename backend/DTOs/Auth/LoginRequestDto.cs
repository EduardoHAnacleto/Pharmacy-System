using System.ComponentModel.DataAnnotations;

namespace Storefront.Api.DTOs.Auth
{
    public class LoginRequestDto
    {
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Password { get; set; } = string.Empty;
    }
}
