using Microsoft.EntityFrameworkCore;
using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<ItemPromotion> ItemPromotions { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

        // Column facets below are not decoration: this model is now the source of
        // truth for the schema. Left unconfigured, EF picks its own defaults —
        // decimal(65,30) for money and longtext for every string — which would
        // make a database created from migrations differ from the hand-written
        // schema this replaces.
        //
        // Store-level defaults (is_active DEFAULT TRUE, created_at DEFAULT
        // CURRENT_TIMESTAMP) are deliberately not declared. EF always writes both
        // columns, and HasDefaultValue(true) on a bool would make saving an
        // explicitly inactive row insert TRUE instead, because false is also the
        // CLR default.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ItemPromotion>(entity =>
            {
                entity.ToTable("item_promotions");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100);
                entity.Property(e => e.Price).HasColumnName("price").HasPrecision(10, 2);
                entity.Property(e => e.PriceBefore).HasColumnName("price_before").HasPrecision(10, 2);
                entity.Property(e => e.ImagePath).HasColumnName("image_path").HasMaxLength(255);
                entity.Property(e => e.DateStart).HasColumnName("date_start");
                entity.Property(e => e.DateEnd).HasColumnName("date_end");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
                entity.Property(e => e.ProductType).HasColumnName("product_type").HasMaxLength(30);
                entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
                entity.Property(e => e.CreatedByUserName)
                      .HasColumnName("created_by_user_name")
                      .HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");

                // Covers the storefront's hot query: active promotions whose date
                // window contains "now", which every page of the home grid runs.
                entity.HasIndex(e => new { e.IsActive, e.DateStart, e.DateEnd })
                      .HasDatabaseName("ix_item_promotions_window");

                // Restrict, not Cascade: removing a category must not silently
                // delete its promotion history.
                entity.HasOne(e => e.Category)
                      .WithMany()
                      .HasForeignKey(e => e.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("categories");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(30);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Username).HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(150);
                entity.Property(e => e.PasswordHash).HasMaxLength(255);
                entity.Property(e => e.Role).HasMaxLength(20);

                entity.HasIndex(e => e.Username).IsUnique();
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("refresh_tokens");
                entity.HasKey(e => e.Id);

                // Always a 64-character hex SHA-256 digest.
                entity.Property(e => e.TokenHash).HasMaxLength(64).IsFixedLength();

                entity.HasIndex(e => e.TokenHash).IsUnique();

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
