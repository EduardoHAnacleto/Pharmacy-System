using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceAndPrescriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "business_number",
                table: "store_settings",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "technical_manager_license",
                table: "store_settings",
                type: "varchar(60)",
                maxLength: 60,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "technical_manager_name",
                table: "store_settings",
                type: "varchar(120)",
                maxLength: 120,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "requires_prescription",
                table: "item_promotions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "business_number",
                table: "store_settings");

            migrationBuilder.DropColumn(
                name: "technical_manager_license",
                table: "store_settings");

            migrationBuilder.DropColumn(
                name: "technical_manager_name",
                table: "store_settings");

            migrationBuilder.DropColumn(
                name: "requires_prescription",
                table: "item_promotions");
        }
    }
}
