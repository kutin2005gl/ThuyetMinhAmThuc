using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Models.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoiController : ControllerBase
{
    private const double DefaultRadiusMeters = 30;
    private const double NearRadiusPaddingMeters = 50;
    private const int DefaultPoiPriority = 1;
    private const long MaxPoiImageBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> AllowedPoiImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
    };

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public PoiController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var poiEntities = await _db.Pois
            .AsNoTracking()
            .Include(p => p.Translations)
            .Where(p => p.IsActive)
            .ToListAsync();
        var pois = poiEntities.Select(p => ToPoiDto(p, baseUrl)).ToList();

        return Ok(pois);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var poi = await _db.Pois
            .AsNoTracking()
            .Include(p => p.Translations)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (poi == null) return NotFound();
        return Ok(ToPoiDto(poi, baseUrl));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PoiCreateDto dto)
    {
        var poi = new Poi
        {
            Name = dto.Name,
            Description = dto.Description,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            RadiusMeters = dto.RadiusMeters,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Pois.Add(poi);
        await _db.SaveChangesAsync();
        return Ok(poi);
    }

    // Công dụng: upload ảnh POI có giới hạn dung lượng để mobile không tải ảnh quá lớn.
    [HttpPost("{id}/upload-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage([FromRoute] int id, IFormFile file)
    {
        var poi = await _db.Pois.FindAsync(id);
        if (poi == null) return NotFound();

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "File ảnh không hợp lệ." });
        }

        if (file.Length > MaxPoiImageBytes)
        {
            return BadRequest(new { message = "Ảnh POI tối đa 2MB để tránh app mobile tải ảnh quá lớn." });
        }

        if (!AllowedPoiImageTypes.Contains(file.ContentType))
        {
            return BadRequest(new { message = "Chỉ hỗ trợ ảnh JPEG, PNG hoặc WebP." });
        }

        var folder = Path.Combine(_env.WebRootPath, "images", "pois");
        Directory.CreateDirectory(folder);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"poi_{id}{extension}";
        var filePath = Path.Combine(folder, fileName);
        var oldImagePath = poi.ImagePath;

        using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream);

        poi.ImagePath = $"/images/pois/{fileName}";
        await _db.SaveChangesAsync();

        DeleteOldPoiImageIfReplaced(oldImagePath, poi.ImagePath);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(new { imageUrl = $"{baseUrl}{poi.ImagePath}" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] PoiUpdateDto dto)
    {
        // 1. Tìm POI trong Database
        var poi = await _db.Pois.FindAsync(id);
        if (poi == null) return NotFound("Không tìm thấy địa điểm");

        // 2. Cập nhật các thông tin
        poi.Name = dto.Name;
        poi.Description = dto.Description;
        poi.Latitude = dto.Latitude;
        poi.Longitude = dto.Longitude;
        poi.RadiusMeters = dto.RadiusMeters;

        // 3. Lưu thay đổi
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!PoiExists(id)) return NotFound();
            else throw;
        }

        return Ok(poi);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        // 1. Tìm POI kèm theo các bản dịch của nó
        var poi = await _db.Pois
            .Include(p => p.Translations) // Quan trọng: Load luôn các bản dịch để xóa cùng lúc
            .FirstOrDefaultAsync(p => p.Id == id);

        if (poi == null) return NotFound("Không tìm thấy địa điểm để xóa");

        // 2. Xóa các bản dịch liên quan trước (nếu có)
        if (poi.Translations != null && poi.Translations.Any())
        {
            _db.Translations.RemoveRange(poi.Translations);
        }

        // 3. Xóa file ảnh trên ổ cứng (để tránh rác server)
        if (!string.IsNullOrEmpty(poi.ImagePath))
        {
            var filePath = Path.Combine(_env.WebRootPath, poi.ImagePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        // 4. Xóa POI
        _db.Pois.Remove(poi);

        await _db.SaveChangesAsync();
        return Ok(new { message = "Đã xóa địa điểm và dữ liệu liên quan thành công" });
    }

    // Hàm hỗ trợ kiểm tra tồn tại
    private bool PoiExists(int id) => _db.Pois.Any(e => e.Id == id);

    // Công dụng: xóa ảnh cũ khi admin thay ảnh POI bằng file khác, tránh giữ tài nguyên thừa trên server.
    private void DeleteOldPoiImageIfReplaced(string? oldImagePath, string? newImagePath)
    {
        if (string.IsNullOrWhiteSpace(oldImagePath) ||
            string.Equals(oldImagePath, newImagePath, StringComparison.OrdinalIgnoreCase) ||
            oldImagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            oldImagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var oldFilePath = Path.Combine(_env.WebRootPath, oldImagePath.TrimStart('/'));
        if (System.IO.File.Exists(oldFilePath))
        {
            System.IO.File.Delete(oldFilePath);
        }
    }

    // Công dụng: gom mapping POI API để giữ contract cũ và bổ sung field cho GPS/geofence.
    private static PoiDto ToPoiDto(Poi poi, string baseUrl)
        => new()
        {
            Id = poi.Id,
            Name = poi.Name,
            Description = poi.Description,
            Latitude = poi.Latitude,
            Longitude = poi.Longitude,
            RadiusMeters = poi.RadiusMeters,
            NearRadiusMeters = CalculateNearRadiusMeters(poi.RadiusMeters),
            Priority = DefaultPoiPriority,
            ImagePath = poi.ImagePath,
            ImageUrl = BuildImageUrl(poi.ImagePath, baseUrl),
            Translations = poi.Translations.Select(t => new PoiTranslationDto
            {
                Language = t.Language,
                Text = t.Text
            }).ToList()
        };

    // Công dụng: tạo URL ảnh đầy đủ nhưng vẫn giữ nguyên URL ngoài nếu admin đã lưu sẵn.
    private static string? BuildImageUrl(string? imagePath, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return null;
        }

        var trimmedPath = imagePath.Trim();
        if (trimmedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmedPath;
        }

        return $"{baseUrl.TrimEnd('/')}/{trimmedPath.TrimStart('/')}";
    }

    // Công dụng: chuẩn hóa vùng cảnh báo gần để client mới nhận sẵn nearRadiusMeters.
    private static double CalculateNearRadiusMeters(double radiusMeters)
    {
        var effectiveRadius = radiusMeters > 0 ? radiusMeters : DefaultRadiusMeters;
        return effectiveRadius + NearRadiusPaddingMeters;
    }

    // DTO classes
    public record PoiCreateDto(
        string Name,
        string Description,
        double Latitude,
        double Longitude,
        double RadiusMeters
    );

    public class PoiDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double RadiusMeters { get; set; } = 30;
        public double NearRadiusMeters { get; set; } = DefaultRadiusMeters + NearRadiusPaddingMeters;
        public int Priority { get; set; } = DefaultPoiPriority;

        public string? ImagePath { get; set; }
        public string? ImageUrl { get; set; }
        public List<PoiTranslationDto>? Translations { get; set; }
    }

    public class PoiTranslationDto
    {
        public string Language { get; set; } = "";
        public string Text { get; set; } = "";
    }

    public record PoiUpdateDto(
        int Id,
        string Name,
        string Description,
        double Latitude,
        double Longitude,
        double RadiusMeters
    );
}
