-- ---------------------------------------------------------------------------
-- Phase 1: authentication tables.
--
-- database/schema.sql is only executed by the MySQL entrypoint when the data
-- volume is empty, so an existing deployment never picks it up. Apply this
-- script by hand once, on any database created before Phase 1:
--
--   docker compose exec -T db mysql -uroot -p"$MYSQL_ROOT_PASSWORD" \
--     "$MYSQL_DATABASE" < database/upgrades/001_add_auth_tables.sql
--
-- It is idempotent — running it twice is harmless. Phase 2 replaces this
-- directory with EF Core migrations.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) NOT NULL,
    email VARCHAR(150) NULL,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(20) NOT NULL DEFAULT 'Admin',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login_at DATETIME NULL,
    CONSTRAINT uq_users_username UNIQUE (username)
);

CREATE TABLE IF NOT EXISTS refresh_tokens (
    id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    token_hash CHAR(64) NOT NULL,
    expires_at DATETIME NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    revoked_at DATETIME NULL,
    CONSTRAINT uq_refresh_tokens_hash UNIQUE (token_hash),
    CONSTRAINT fk_refresh_tokens_user
        FOREIGN KEY (user_id) REFERENCES users(id)
        ON DELETE CASCADE
);

-- No user rows are inserted here. The API creates the first admin on startup
-- from ADMIN_SEED_USERNAME / ADMIN_SEED_PASSWORD, so no password hash is ever
-- committed to this repository.
