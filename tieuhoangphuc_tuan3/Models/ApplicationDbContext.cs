using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Linq; // Thêm để dùng LINQ cho vòng lặp decimal
using WebBanDienThoai.Models;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    internal object MomoInfoModel;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderDetail> OrderDetails { get; set; }
    public DbSet<Wishlist> Wishlists { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }
    public DbSet<MomoInfoModel> MomoInfos { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ProductRating> ProductRatings { get; set; }
    public DbSet<ProductRatingReply> ProductRatingReplies { get; set; }
    public DbSet<SubCategory> SubCategories { get; set; }
    public DbSet<News> News { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<Coupon> Coupons { get; set; }
    public DbSet<WarrantyOption> WarrantyOptions { get; set; }
    public DbSet<OrderDetailWarranty> OrderDetailWarranties { get; set; }
    public DbSet<ProductSpecs> ProductSpecs { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<Store> Stores { get; set; }
    public DbSet<CouponUsage> CouponUsages { get; set; }
    public DbSet<ShippingRate> ShippingRates { get; set; }
    public DbSet<Province> Provinces { get; set; }
    public DbSet<District> Districts { get; set; }
    public DbSet<ProductRatingImage> ProductRatingImages { get; set; }
    public DbSet<ProductRatingVote> ProductRatingVotes { get; set; }
    public DbSet<ProductRatingReport> ProductRatingReports { get; set; }
    public DbSet<ProductQuestion> ProductQuestions { get; set; }
    public DbSet<ProductQuestionReply> ProductQuestionReplies { get; set; }
    public DbSet<ProductFaq> ProductFaqs { get; set; }
    public DbSet<Shipper> Shippers { get; set; }
    public DbSet<OrderLog> OrderLogs { get; set; }
    public DbSet<ApplicationUser> ApplicationUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ==========================================
        // 1. TỰ ĐỘNG FIX CẢNH BÁO DECIMAL WARNINGS
        // ==========================================
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }

        // ==========================================
        // 2. FIX LỖI MULTIPLE CASCADE PATHS (BẢNG STORES)
        // ==========================================
        modelBuilder.Entity<Store>()
            .HasOne(s => s.Province)
            .WithMany()
            .HasForeignKey(s => s.ProvinceId)
            .OnDelete(DeleteBehavior.Restrict); // Tắt cascade khi xóa Province

        modelBuilder.Entity<Store>()
            .HasOne(s => s.District)
            .WithMany()
            .HasForeignKey(s => s.DistrictId)
            .OnDelete(DeleteBehavior.Restrict); // Tắt cascade khi xóa District

        // ==========================================
        // CÁC CẤU HÌNH HIỆN TẠI CỦA BẠN (GIỮ NGUYÊN)
        // ==========================================
        modelBuilder.Entity<Product>()
            .HasMany(p => p.Images)
            .WithOne(pi => pi.Product)
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductRatingReply>()
            .HasOne(r => r.ProductRating)
            .WithMany(rating => rating.Replies)
            .HasForeignKey(r => r.ProductRatingId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductRatingReply>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductRating>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductRatingVote>()
            .HasIndex(v => new { v.ProductRatingId, v.UserId })
            .IsUnique();

        modelBuilder.Entity<ProductRatingReport>()
            .HasIndex(r => new { r.ProductRatingId, r.UserId })
            .IsUnique();

        modelBuilder.Entity<ProductRating>()
            .HasIndex(r => new { r.ProductId, r.UserId })
            .IsUnique();

        modelBuilder.Entity<ProductRatingImage>()
            .HasOne(x => x.ProductRating)
            .WithMany(r => r.Images)
            .HasForeignKey(x => x.ProductRatingId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductRatingVote>()
            .HasOne(x => x.ProductRating)
            .WithMany(r => r.Votes)
            .HasForeignKey(x => x.ProductRatingId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductRatingReport>()
            .HasOne(x => x.ProductRating)
            .WithMany(r => r.Reports)
            .HasForeignKey(x => x.ProductRatingId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductQuestion>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductQuestionReply>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductQuestionReply>()
            .HasOne(x => x.ProductQuestion)
            .WithMany(q => q.Replies)
            .HasForeignKey(x => x.ProductQuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductFaq>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductQuestion>()
            .HasIndex(x => new { x.ProductId, x.CreatedAt });

        modelBuilder.Entity<ProductFaq>()
            .HasIndex(x => new { x.ProductId, x.IsActive, x.SortOrder });
    }
}