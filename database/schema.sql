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
