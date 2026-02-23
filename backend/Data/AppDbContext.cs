using Microsoft.EntityFrameworkCore;
using PharmacyWorkerAPI.Models;
using System.Collections.Generic;

namespace PharmacyWorkerAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<ItemPromotion> ItemPromotions { get; set; }
        public DbSet<Category> Categories { get; set; }

        public ProductType ProductType { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ItemPromotion>(entity =>
            {
                entity.ToTable("item_promotions");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Price).HasColumnName("price");
                entity.Property(e => e.PriceBefore).HasColumnName("price_before");
                entity.Property(e => e.ImagePath).HasColumnName("image_path");
                entity.Property(e => e.DateStart).HasColumnName("date_start");
                entity.Property(e => e.DateEnd).HasColumnName("date_end");
                entity.Property(e => e.IsActive).HasColumnName("is_active");
                entity.Property(e => e.ProductType).HasColumnName("product_type");
                entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
                entity.Property(e => e.CreatedByUserName).HasColumnName("created_by_user_name");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");

                entity.HasOne(e => e.Category)
                      .WithMany()
                      .HasForeignKey(e => e.CategoryId);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("categories");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            });
        }
    }
}
