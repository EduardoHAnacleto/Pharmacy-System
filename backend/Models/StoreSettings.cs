using System.ComponentModel.DataAnnotations.Schema;

namespace Storefront.Api.Models
{
    /// <summary>
    /// Everything about a shop that used to be hardcoded.
    /// </summary>
    /// <remarks>
    /// This is what turns the project from one pharmacy's site into a storefront any
    /// shop can run: branding, contact details, currency, locale, delivery rules,
    /// opening hours and which checkout fields to collect all become data.
    /// <para>
    /// Before this existed the code carried a Google Map pointing at the Tower of
    /// Pisa, <c>EMAIL@MAIL.com</c>, two different hardcoded WhatsApp numbers, an R$8
    /// delivery fee, a single service city, and Brazilian public holidays — none of
    /// which a second customer could change without a code change.
    /// </para>
    /// <para>
    /// A single row. Multi-tenancy is a later step: one deployment per shop, fully
    /// configurable, delivers the commercial value without rewriting the data model
    /// for tenants that do not exist yet.
    /// </para>
    /// </remarks>
    public class StoreSettings
    {
        /// <summary>Fixed at 1: there is exactly one row.</summary>
        [Column("id")]
        public int Id { get; set; } = 1;

        // ===== IDENTITY =====

        [Column("store_name")]
        public string StoreName { get; set; } = "Minha Loja";

        [Column("tagline")]
        public string? Tagline { get; set; }

        [Column("logo_url")]
        public string? LogoUrl { get; set; }

        /// <summary>CSS colour, applied as a custom property so the whole UI follows.</summary>
        [Column("primary_color")]
        public string PrimaryColor { get; set; } = "#0d6efd";

        [Column("secondary_color")]
        public string SecondaryColor { get; set; } = "#198754";

        // ===== LOCATION =====

        [Column("address")]
        public string? Address { get; set; }

        [Column("city")]
        public string? City { get; set; }

        /// <summary>ISO 3166-1 alpha-2. Also selects the public-holiday calendar.</summary>
        [Column("country_code")]
        public string CountryCode { get; set; } = "BR";

        /// <summary>IANA id, used for the promotion window and opening hours.</summary>
        [Column("time_zone")]
        public string TimeZone { get; set; } = "America/Sao_Paulo";

        /// <summary>Free-form query for the embedded map, so no coordinates are needed.</summary>
        [Column("map_query")]
        public string? MapQuery { get; set; }

        // ===== CONTACT =====

        [Column("phone")]
        public string? Phone { get; set; }

        /// <summary>International format, digits only. Used for the order handoff.</summary>
        [Column("whatsapp_number")]
        public string? WhatsAppNumber { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("instagram_url")]
        public string? InstagramUrl { get; set; }

        [Column("facebook_url")]
        public string? FacebookUrl { get; set; }

        // ===== COMMERCE =====

        /// <summary>ISO 4217, e.g. BRL or NZD.</summary>
        [Column("currency")]
        public string Currency { get; set; } = "BRL";

        /// <summary>BCP 47 tag driving number and date formatting.</summary>
        [Column("locale")]
        public string Locale { get; set; } = "pt-BR";

        [Column("delivery_enabled")]
        public bool DeliveryEnabled { get; set; } = true;

        [Column("pickup_enabled")]
        public bool PickupEnabled { get; set; } = true;

        [Column("delivery_fee")]
        public decimal DeliveryFee { get; set; } = 8m;

        [Column("min_delivery_total")]
        public decimal MinDeliveryTotal { get; set; } = 30m;

        /// <summary>Comma-separated list of cities delivered to.</summary>
        [Column("delivery_cities")]
        public string? DeliveryCities { get; set; }

        // ===== CHECKOUT =====

        /// <summary>
        /// Whether to ask for a national tax id. Off for shops outside Brazil, where
        /// CPF is meaningless — and it is never sent to the API regardless.
        /// </summary>
        [Column("collect_tax_id")]
        public bool CollectTaxId { get; set; } = true;

        [Column("collect_postal_code")]
        public bool CollectPostalCode { get; set; } = true;

        // ===== COMPLIANCE =====

        /// <remarks>
        /// These three are what a customer looks for before sending money to a shop
        /// they have not used: who is legally behind it, and who is answerable for
        /// what it dispenses. A Brazilian pharmacy must publish them; the footer had
        /// none of the three.
        /// <para>
        /// Named for the concept rather than the country, like <see cref="CountryCode"/>
        /// and <see cref="Currency"/>. The obligation is not Brazilian — only the
        /// acronyms are — and the labels that say CNPJ and CRF live in the
        /// translations, where they can differ per market.
        /// </para>
        /// </remarks>

        /// <summary>Company registration: CNPJ in Brazil, NZBN in New Zealand.</summary>
        [Column("business_number")]
        public string? BusinessNumber { get; set; }

        /// <summary>
        /// The professional legally answerable for what the shop dispenses — the
        /// <i>farmacêutico responsável</i> of a Brazilian pharmacy. The field does not
        /// name the profession: other regulated trades carry the same duty.
        /// </summary>
        [Column("technical_manager_name")]
        public string? TechnicalManagerName { get; set; }

        /// <summary>Their professional registration, e.g. <c>CRF-SP 12345</c>.</summary>
        [Column("technical_manager_license")]
        public string? TechnicalManagerLicense { get; set; }

        // ===== OPERATION =====

        /// <summary>
        /// JSON, keyed by ISO weekday: <c>{"1":[["08:00","18:00"]],"7":[]}</c>.
        /// Stored as text because the shape is presentational, not queried.
        /// </summary>
        [Column("opening_hours")]
        public string? OpeningHours { get; set; }

        [Column("footer_text")]
        public string? FooterText { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
