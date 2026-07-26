using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionWindowIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_item_promotions_window",
                table: "item_promotions",
                columns: new[] { "is_active", "date_start", "date_end" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_item_promotions_window",
                table: "item_promotions");
        }
    }
}
