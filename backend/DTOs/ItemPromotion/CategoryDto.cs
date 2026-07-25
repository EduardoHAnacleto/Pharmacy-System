using System.ComponentModel.DataAnnotations;

namespace PharmacyWorkerAPI.DTOs.ItemPromotion
{
    /// <summary>Create or rename a category.</summary>
    public class CategoryWriteDto
    {
        // 30 matches the column, so oversized input is a 400 rather than a 500.
        [Required(ErrorMessage = "O nome da categoria é obrigatório.")]
        [MaxLength(30, ErrorMessage = "O nome da categoria deve ter no máximo 30 caracteres.")]
        public string Name { get; set; } = string.Empty;
    }

    public class CategoryDto
    {
        /// <summary>
        /// Without the id a client cannot map a selected category back to the
        /// CategoryId a promotion requires — which is why the admin form used to
        /// hardcode 1.
        /// </summary>
        public int Id { get; set; }

        public string Name { get; set; } = null!;
    }
}
