using Microsoft.EntityFrameworkCore;
using WebAPI.Models.Entities;

namespace WebAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Poi> Pois => Set<Poi>();
    public DbSet<Translation> Translations => Set<Translation>();
    public DbSet<AudioFile> AudioFiles => Set<AudioFile>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<TourPoi> TourPois => Set<TourPoi>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Poi>().HasData(
            new Poi { Id = 1, Name = "Quán Phở Bà Dậu", Description = "Phở bò truyền thống từ 1970", Latitude = 10.7769, Longitude = 106.7009, RadiusMeters = 30, IsActive = true, CreatedAt = new DateTime(2024, 1, 1) },
            new Poi { Id = 2, Name = "Bánh Mì Hùng", Description = "Bánh mì đặc sản nổi tiếng nhất phố", Latitude = 10.7775, Longitude = 106.7015, RadiusMeters = 30, IsActive = true, CreatedAt = new DateTime(2024, 1, 1) },
            new Poi { Id = 3, Name = "Chè Bà Ba", Description = "Chè truyền thống Nam Bộ", Latitude = 10.7780, Longitude = 106.7020, RadiusMeters = 30, IsActive = true, CreatedAt = new DateTime(2024, 1, 1) }
        );

        modelBuilder.Entity<Translation>().HasData(
            new Translation { Id = 1, PoiId = 1, Language = "vi", Text = "Chào mừng đến với Quán Phở Bà Dậu. Quán được thành lập từ năm 1970 với công thức phở bò truyền thống." },
            new Translation { Id = 2, PoiId = 1, Language = "en", Text = "Welcome to Pho Ba Dau restaurant, established in 1970 with traditional beef pho recipe." },
            new Translation { Id = 3, PoiId = 2, Language = "vi", Text = "Bánh Mì Hùng là địa điểm nổi tiếng nhất phố với hơn 30 năm kinh nghiệm làm bánh mì đặc sản." },
            new Translation { Id = 4, PoiId = 2, Language = "en", Text = "Banh Mi Hung is the most famous spot on the street with over 30 years of experience." },
            new Translation { Id = 5, PoiId = 3, Language = "vi", Text = "Chè Bà Ba phục vụ các loại chè truyền thống Nam Bộ được nấu theo công thức gia truyền." },
            new Translation { Id = 6, PoiId = 3, Language = "en", Text = "Che Ba Ba serves traditional Southern Vietnamese sweet desserts cooked with family recipes." }
        );

        modelBuilder.Entity<AdminUser>().HasData(
            new AdminUser
            {
                Id = 1,
                Username = "admin",
                // password: admin123 (đã hash bằng BCrypt)
                PasswordHash = "$2a$11$rBnqmPnFjQFnXcJZCJRfUOZjHvBNOGKcMmJJKNJaGpGcBFQ9ABCDE",
                FullName = "Quản trị viên",
                Role = "Admin",
                CreatedAt = new DateTime(2024, 1, 1)
            }
        );
    }
}