CREATE TABLE categories (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(30) NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE item_promotions (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    price DECIMAL(10,2) NOT NULL,
    price_before DECIMAL(10,2) NOT NULL,
    image_path VARCHAR(255) NOT NULL,
    date_start DATETIME NOT NULL,
    date_end DATETIME NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    category_id INT NOT NULL,
    product_type VARCHAR(30) NOT NULL,
    created_by_user_id INT NOT NULL,
    created_by_user_name VARCHAR(50) NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_item_promotions_category
        FOREIGN KEY (category_id) REFERENCES categories(id)
);

-- Admin operators. Staff accounts only: the system stores no customer
-- accounts and no customer personal data.
CREATE TABLE users (
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

-- Only a SHA-256 digest of each refresh token is kept, so a database dump
-- cannot be replayed as a set of live sessions.
CREATE TABLE refresh_tokens (
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
