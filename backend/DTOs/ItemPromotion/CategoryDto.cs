namespace PharmacyWorkerAPI.DTOs.ItemPromotion
{
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
