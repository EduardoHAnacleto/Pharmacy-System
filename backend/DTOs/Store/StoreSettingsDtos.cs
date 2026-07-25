using System.ComponentModel.DataAnnotations;

namespace PharmacyWorkerAPI.DTOs.Store
{
    /// <summary>
    /// The shop's configuration as the storefront consumes it.
    /// </summary>
    /// <remarks>
    /// Every field here is already visible to any visitor — it is the name, address
    /// and phone number printed on the page — so this response is public. Nothing
    /// operational (credentials, connection strings, seed configuration) belongs in
    /// this entity for exactly that reason.
    /// </remarks>
    public class StoreSettingsDto
    {
        public string StoreName { get; set; } = string.Empty;
        public string? Tagline { get; set; }
        public string? LogoUrl { get; set; }
        public string PrimaryColor { get; set; } = string.Empty;
        public string SecondaryColor { get; set; } = string.Empty;

        public string? Address { get; set; }
        public string? City { get; set; }
        public string CountryCode { get; set; } = string.Empty;
        public string TimeZone { get; set; } = string.Empty;
        public string? MapQuery { get; set; }

        public string? Phone { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? Email { get; set; }
        public string? InstagramUrl { get; set; }
        public string? FacebookUrl { get; set; }

        public string Currency { get; set; } = string.Empty;
        public string Locale { get; set; } = string.Empty;
        public bool DeliveryEnabled { get; set; }
        public bool PickupEnabled { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal MinDeliveryTotal { get; set; }

        /// <summary>Split out of the stored comma-separated list.</summary>
        public IReadOnlyList<string> DeliveryCities { get; set; } = [];

        public bool CollectTaxId { get; set; }
        public bool CollectPostalCode { get; set; }

        /// <summary>Weekday number (1 = Monday) to its open/close ranges.</summary>
        public IReadOnlyDictionary<string, List<OpeningRangeDto>> OpeningHours { get; set; } =
            new Dictionary<string, List<OpeningRangeDto>>();

        public string? FooterText { get; set; }
    }

    /// <summary>One continuous span the shop is open, as <c>HH:mm</c> strings.</summary>
    /// <remarks>
    /// A list per day rather than a single range because a shop that closes for
    /// lunch is common, and modelling it as two ranges keeps "open now" a simple
    /// containment test instead of a special case.
    /// </remarks>
    public class OpeningRangeDto
    {
        [Required]
        [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Horário deve estar no formato HH:mm.")]
        public string Open { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Horário deve estar no formato HH:mm.")]
        public string Close { get; set; } = string.Empty;
    }

    /// <summary>
    /// A full replacement of the shop's configuration.
    /// </summary>
    /// <remarks>
    /// Deliberately a PUT of the whole object rather than a PATCH: the admin screen
    /// edits every field on one form, and a partial update would need every property
    /// to be nullable just to distinguish "unchanged" from "cleared" — which for
    /// bools and non-nullable strings makes the contract worse than the problem.
    /// <para>
    /// The lengths mirror the column widths, so oversized input is a 400 from
    /// validation instead of a 500 from the database.
    /// </para>
    /// </remarks>
    public class StoreSettingsUpdateDto
    {
        [Required(ErrorMessage = "O nome da loja é obrigatório.")]
        [MaxLength(100)]
        public string StoreName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Tagline { get; set; }

        [MaxLength(255)]
        public string? LogoUrl { get; set; }

        [Required]
        [MaxLength(20)]
        [RegularExpression(
            @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
            ErrorMessage = "A cor deve ser um hexadecimal como #0d6efd.")]
        public string PrimaryColor { get; set; } = "#0d6efd";

        [Required]
        [MaxLength(20)]
        [RegularExpression(
            @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
            ErrorMessage = "A cor deve ser um hexadecimal como #198754.")]
        public string SecondaryColor { get; set; } = "#198754";

        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [Required(ErrorMessage = "O país é obrigatório.")]
        [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Use o código de duas letras do país, como BR ou NZ.")]
        public string CountryCode { get; set; } = "BR";

        [Required(ErrorMessage = "O fuso horário é obrigatório.")]
        [MaxLength(64)]
        public string TimeZone { get; set; } = "America/Sao_Paulo";

        [MaxLength(200)]
        public string? MapQuery { get; set; }

        [MaxLength(30)]
        public string? Phone { get; set; }

        /// <summary>
        /// Digits only, in international format. Validated because a malformed number
        /// silently breaks the one action the whole storefront exists to produce.
        /// </summary>
        [MaxLength(20)]
        [RegularExpression(@"^\d{8,20}$", ErrorMessage = "O WhatsApp deve conter apenas dígitos, com DDI e DDD.")]
        public string? WhatsAppNumber { get; set; }

        [MaxLength(150)]
        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        public string? Email { get; set; }

        [MaxLength(255)]
        [Url(ErrorMessage = "Informe uma URL completa.")]
        public string? InstagramUrl { get; set; }

        [MaxLength(255)]
        [Url(ErrorMessage = "Informe uma URL completa.")]
        public string? FacebookUrl { get; set; }

        [Required(ErrorMessage = "A moeda é obrigatória.")]
        [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Use o código de três letras da moeda, como BRL ou NZD.")]
        public string Currency { get; set; } = "BRL";

        [Required(ErrorMessage = "O idioma é obrigatório.")]
        [MaxLength(10)]
        public string Locale { get; set; } = "pt-BR";

        public bool DeliveryEnabled { get; set; }
        public bool PickupEnabled { get; set; }

        [Range(0, 100000, ErrorMessage = "A taxa de entrega não pode ser negativa.")]
        public decimal DeliveryFee { get; set; }

        [Range(0, 1000000, ErrorMessage = "O mínimo para entrega não pode ser negativo.")]
        public decimal MinDeliveryTotal { get; set; }

        public List<string> DeliveryCities { get; set; } = [];

        public bool CollectTaxId { get; set; }
        public bool CollectPostalCode { get; set; }

        public Dictionary<string, List<OpeningRangeDto>> OpeningHours { get; set; } = [];

        [MaxLength(300)]
        public string? FooterText { get; set; }
    }
}
