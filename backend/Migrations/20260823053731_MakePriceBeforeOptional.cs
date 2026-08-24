using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakePriceBeforeOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "price_before",
                table: "item_promotions",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Backfill before narrowing. `defaultValue` below sets the column's
            // DEFAULT for future inserts; it does not touch rows that are already
            // NULL, and under MySQL's default sql_mode the MODIFY then fails with
            // "Invalid use of NULL value" the moment anyone has used the feature
            // this migration exists to add.
            //
            // That failure is not clean: MySQL commits DDL implicitly, so the
            // statements EF has already run in this rollback stay applied while
            // __EFMigrationsHistory no longer matches the schema.
            migrationBuilder.Sql(
                "UPDATE `item_promotions` SET `price_before` = 0 WHERE `price_before` IS NULL;");

            migrationBuilder.AlterColumn<decimal>(
                name: "price_before",
                table: "item_promotions",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
