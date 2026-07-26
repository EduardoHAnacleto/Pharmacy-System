using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storefront.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaAssetsAndPromotionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_item_promotions_window",
                table: "item_promotions");

            // NOTE: hand-edited. EF scaffolded `DropColumn is_active` here, before
            // the status column it is replaced by even exists — which would discard
            // the only information available to derive status from. The drop is
            // moved below the backfill.

            migrationBuilder.AddColumn<DateTime>(
                name: "archived_at",
                table: "item_promotions",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "archived_by_user_id",
                table: "item_promotions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "media_asset_id",
                table: "item_promotions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_promotion_id",
                table: "item_promotions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "item_promotions",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "item_promotions",
                type: "datetime(6)",
                nullable: true);

            // ---------------------------------------------------------------
            // Hand-written backfill: derive status from the boolean it replaces.
            //
            // is_active could not distinguish a draft from an expired, manually
            // switched-off, or archived promotion — all four were false. What can
            // be recovered is: inactive rows become drafts, and active rows are
            // placed relative to their own window. Nothing existing is archived,
            // because archiving did not exist before this migration.
            // ---------------------------------------------------------------
            migrationBuilder.Sql(
                """
                UPDATE item_promotions
                SET status = CASE
                    WHEN is_active = FALSE THEN 'Draft'
                    WHEN UTC_TIMESTAMP() < date_start THEN 'Scheduled'
                    WHEN UTC_TIMESTAMP() > date_end THEN 'Expired'
                    ELSE 'Active'
                END;
                """);

            // Safe now that the value has been carried across.
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "item_promotions");

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    user_name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    action = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    entity_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    entity_id = table.Column<int>(type: "int", nullable: true),
                    changes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    file_path = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content_hash = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    mime_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    byte_size = table.Column<int>(type: "int", nullable: false),
                    original_filename = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_user_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    is_missing = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_assets", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "promotion_status_history",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    promotion_id = table.Column<int>(type: "int", nullable: false),
                    from_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    to_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    changed_by_user_id = table.Column<int>(type: "int", nullable: true),
                    reason = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    changed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotion_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_promotion_status_history_item_promotions_promotion_id",
                        column: x => x.promotion_id,
                        principalTable: "item_promotions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_item_promotions_media_asset_id",
                table: "item_promotions",
                column: "media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_promotions_source_promotion_id",
                table: "item_promotions",
                column: "source_promotion_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_promotions_window",
                table: "item_promotions",
                columns: new[] { "status", "date_start", "date_end" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entity",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_occurred",
                table: "audit_log",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_content_hash",
                table: "media_assets",
                column: "content_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_promotion_status_history_promotion",
                table: "promotion_status_history",
                columns: new[] { "promotion_id", "changed_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_item_promotions_item_promotions_source_promotion_id",
                table: "item_promotions",
                column: "source_promotion_id",
                principalTable: "item_promotions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_item_promotions_media_assets_media_asset_id",
                table: "item_promotions",
                column: "media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_item_promotions_item_promotions_source_promotion_id",
                table: "item_promotions");

            migrationBuilder.DropForeignKey(
                name: "FK_item_promotions_media_assets_media_asset_id",
                table: "item_promotions");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "promotion_status_history");

            migrationBuilder.DropIndex(
                name: "IX_item_promotions_media_asset_id",
                table: "item_promotions");

            migrationBuilder.DropIndex(
                name: "IX_item_promotions_source_promotion_id",
                table: "item_promotions");

            migrationBuilder.DropIndex(
                name: "ix_item_promotions_window",
                table: "item_promotions");

            migrationBuilder.DropColumn(
                name: "archived_at",
                table: "item_promotions");

            migrationBuilder.DropColumn(
                name: "archived_by_user_id",
                table: "item_promotions");

            migrationBuilder.DropColumn(
                name: "media_asset_id",
                table: "item_promotions");

            migrationBuilder.DropColumn(
                name: "source_promotion_id",
                table: "item_promotions");

            // Hand-written: re-add is_active and derive it from status before
            // status is dropped, so a rollback does not silently reactivate every
            // draft and archived promotion on the storefront.
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "item_promotions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE item_promotions
                SET is_active = CASE
                    WHEN status IN ('Draft', 'Archived') THEN FALSE
                    ELSE TRUE
                END;
                """);

            migrationBuilder.DropColumn(
                name: "status",
                table: "item_promotions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "item_promotions");

            // is_active is re-added above, before status is dropped, so its value
            // can be derived. EF scaffolded a second AddColumn here, which has been
            // removed.

            migrationBuilder.CreateIndex(
                name: "ix_item_promotions_window",
                table: "item_promotions",
                columns: new[] { "is_active", "date_start", "date_end" });
        }
    }
}
