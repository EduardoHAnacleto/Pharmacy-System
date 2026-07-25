-- ---------------------------------------------------------------------------
-- Phase 2: adopt EF Core migrations on a database that predates them.
--
-- Run this ONCE, and only on a database that already contains `categories` and
-- `item_promotions` — i.e. one created by the old database/schema.sql. A fresh
-- deployment needs none of this: the migrator service creates everything.
--
--   # 1. auth tables, if 001 was never applied (it is idempotent)
--   docker compose exec -T db mysql -uroot -p"$MYSQL_ROOT_PASSWORD" \
--     "$MYSQL_DATABASE" < database/upgrades/001_add_auth_tables.sql
--
--   # 2. this script: record InitialCreate as already applied
--   docker compose exec -T db mysql -uroot -p"$MYSQL_ROOT_PASSWORD" \
--     "$MYSQL_DATABASE" < database/upgrades/002_baseline_migrations.sql
--
--   # 3. let the migrator apply everything after InitialCreate
--   docker compose run --rm migrator
--
-- Why the baseline is needed: InitialCreate contains CREATE TABLE statements
-- for tables this database already has, so running it would fail. Marking it as
-- applied tells EF the starting point, and only later migrations run.
--
-- This script is idempotent — running it twice changes nothing.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET utf8mb4;

-- Only baseline when the legacy tables are actually present. On an empty
-- database this inserts nothing, so the migrator creates the schema normally
-- instead of skipping InitialCreate and leaving the database empty.
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT '20260725140850_InitialCreate', '9.0.9'
FROM DUAL
WHERE EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = DATABASE() AND table_name = 'item_promotions'
    )
  AND EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = DATABASE() AND table_name = 'users'
    )
  AND NOT EXISTS (
        SELECT 1 FROM `__EFMigrationsHistory`
        WHERE `MigrationId` = '20260725140850_InitialCreate'
    );

-- Note on pre-existing column types: the old schema.sql and InitialCreate agree
-- on every column (varchar lengths, decimal(10,2), char(64), RESTRICT on the
-- category foreign key), so a baselined database matches what migrations would
-- have produced. The one difference is that schema.sql declared
-- `is_active DEFAULT TRUE` and `created_at DEFAULT CURRENT_TIMESTAMP`, which
-- migrations do not: the application always writes both columns explicitly.
-- Leaving the old defaults in place is harmless.
