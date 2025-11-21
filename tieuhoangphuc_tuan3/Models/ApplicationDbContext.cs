using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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
    // Các DbSet khác nếu cần
    public DbSet<MomoInfoModel> MomoInfos { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ProductRating> ProductRatings { get; set; }
    public DbSet<ProductRatingReply> ProductRatingReplies { get; set; }
    public DbSet<SubCategory> SubCategories { get; set; }
    public DbSet<News> News { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cấu hình DeleteBehavior.Restrict để không tự động xóa ảnh khi xóa sản phẩm
        modelBuilder.Entity<Product>()
            .HasMany(p => p.Images)
            .WithOne(pi => pi.Product)
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Restrict); // Không tự động xóa ảnh khi xóa sản phẩm

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
    }



}
