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
        public DbSet<MediaAsset> MediaAssets { get; set; } = null!;
        public DbSet<PromotionStatusHistory> PromotionStatusHistory { get; set; } = null!;
        public DbSet<AuditLogEntry> AuditLog { get; set; } = null!;
        public DbSet<AnalyticsEvent> AnalyticsEvents { get; set; } = null!;
        public DbSet<AnalyticsDaily> AnalyticsDaily { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;

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
                entity.Property(e => e.Status).HasMaxLength(20);
                entity.Property(e => e.ProductType).HasColumnName("product_type").HasMaxLength(30);
                entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
                entity.Property(e => e.CreatedByUserName)
                      .HasColumnName("created_by_user_name")
                      .HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");

                // Covers the storefront's hot query: publishable promotions whose
                // date window contains "now", which every page of the grid runs.
                entity.HasIndex(e => new { e.Status, e.DateStart, e.DateEnd })
                      .HasDatabaseName("ix_item_promotions_window");

                // Restrict, not Cascade: removing a category must not silently
                // delete its promotion history.
                entity.HasOne(e => e.Category)
                      .WithMany()
                      .HasForeignKey(e => e.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Restrict as well: an asset referenced by any promotion, archived
                // or live, must not be deletable out from under it.
                entity.HasOne(e => e.MediaAsset)
                      .WithMany()
                      .HasForeignKey(e => e.MediaAssetId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Self-reference for the reactivation chain. Deleting the original
                // must not cascade through every campaign that repeated it.
                entity.HasOne(e => e.SourcePromotion)
                      .WithMany()
                      .HasForeignKey(e => e.SourcePromotionId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<MediaAsset>(entity =>
            {
                entity.ToTable("media_assets");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.FilePath).HasMaxLength(255);
                entity.Property(e => e.ContentHash).HasMaxLength(64).IsFixedLength();
                entity.Property(e => e.MimeType).HasMaxLength(50);
                entity.Property(e => e.OriginalFileName).HasMaxLength(255);

                // Uploading the same file twice reuses this row instead of writing
                // a second copy to disk.
                entity.HasIndex(e => e.ContentHash).IsUnique();
            });

            modelBuilder.Entity<PromotionStatusHistory>(entity =>
            {
                entity.ToTable("promotion_status_history");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.FromStatus).HasMaxLength(20);
                entity.Property(e => e.ToStatus).HasMaxLength(20);
                entity.Property(e => e.Reason).HasMaxLength(200);

                entity.HasIndex(e => new { e.PromotionId, e.ChangedAt })
                      .HasDatabaseName("ix_promotion_status_history_promotion");

                entity.HasOne(e => e.Promotion)
                      .WithMany()
                      .HasForeignKey(e => e.PromotionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // No PII by construction: these entities have no column for a name,
            // phone, national ID, address, IP or user agent, so none can be stored
            // by accident.
            modelBuilder.Entity<AnalyticsEvent>(entity =>
            {
                entity.ToTable("analytics_events");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.EventType).HasMaxLength(30);
                entity.Property(e => e.SessionKey).HasMaxLength(32).IsFixedLength();

                entity.HasIndex(e => new { e.OccurredAt, e.EventType })
                      .HasDatabaseName("ix_analytics_events_occurred");
                entity.HasIndex(e => new { e.PromotionId, e.OccurredAt })
                      .HasDatabaseName("ix_analytics_events_promotion");
            });

            modelBuilder.Entity<AnalyticsDaily>(entity =>
            {
                entity.ToTable("analytics_daily");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.EventType).HasMaxLength(30);

                // One row per day, event type and promotion, so the rollup can
                // upsert idempotently and be re-run safely.
                entity.HasIndex(e => new { e.StatDate, e.EventType, e.PromotionId })
                      .IsUnique()
                      .HasDatabaseName("uq_analytics_daily");
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("orders");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.OrderNumber).HasMaxLength(20);
                entity.Property(e => e.FulfillmentType).HasMaxLength(20);
                entity.Property(e => e.PaymentMethod).HasMaxLength(20);
                entity.Property(e => e.DeliveryCity).HasMaxLength(100);
                entity.Property(e => e.Currency).HasMaxLength(3).IsFixedLength();
                entity.Property(e => e.ItemsSubtotal).HasPrecision(10, 2);
                entity.Property(e => e.DeliveryFee).HasPrecision(10, 2);
                entity.Property(e => e.Total).HasPrecision(10, 2);

                entity.HasIndex(e => e.OrderNumber).IsUnique();
                entity.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_orders_created");
            });

            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.ToTable("order_items");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.NameSnapshot).HasMaxLength(150);
                entity.Property(e => e.UnitPriceSnapshot).HasPrecision(10, 2);
                entity.Property(e => e.LineTotal).HasPrecision(10, 2);

                entity.HasOne(e => e.Order)
                      .WithMany(o => o.Items)
                      .HasForeignKey(e => e.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);

                // SetNull, not Cascade: purging a promotion must not delete the
                // sales history that references it. The snapshot keeps the line
                // readable on its own.
                entity.HasOne<ItemPromotion>()
                      .WithMany()
                      .HasForeignKey(e => e.PromotionId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<AuditLogEntry>(entity =>
            {
                entity.ToTable("audit_log");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Action).HasMaxLength(50);
                entity.Property(e => e.EntityType).HasMaxLength(50);
                entity.Property(e => e.UserName).HasMaxLength(50);

                entity.HasIndex(e => e.OccurredAt).HasDatabaseName("ix_audit_log_occurred");
                entity.HasIndex(e => new { e.EntityType, e.EntityId })
                      .HasDatabaseName("ix_audit_log_entity");
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
