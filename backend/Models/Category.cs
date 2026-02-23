using System.ComponentModel.DataAnnotations.Schema;

namespace PharmacyWorkerAPI.Models
{
    public class Category
    {
        [Column("id")]
        public int Id { get; set; }
        public string Name { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
