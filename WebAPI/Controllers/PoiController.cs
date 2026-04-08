using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Models.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoiController : ControllerBase
{
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
        var pois = await _db.Pois
            .Include(p => p.Translations)
            .Where(p => p.IsActive)
            .Select(p => new PoiDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                RadiusMeters = p.RadiusMeters,
                ImagePath = p.ImagePath,
                ImageUrl = string.IsNullOrEmpty(p.ImagePath) ? null : $"{baseUrl}{p.ImagePath}",
                Translations = p.Translations.Select(t => new PoiTranslationDto
                {
                    Language = t.Language,
                    Text = t.Text
                }).ToList()
            })
            .ToListAsync();

        return Ok(pois);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var poi = await _db.Pois
            .Include(p => p.Translations)
            .Where(p => p.Id == id)
            .Select(p => new PoiDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                RadiusMeters = p.RadiusMeters,
                ImagePath = p.ImagePath,
                ImageUrl = string.IsNullOrEmpty(p.ImagePath) ? null : $"{baseUrl}{p.ImagePath}",
                Translations = p.Translations.Select(t => new PoiTranslationDto
                {
                    Language = t.Language,
                    Text = t.Text
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (poi == null) return NotFound();
        return Ok(poi);
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

    [HttpPost("{id}/upload-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(int id, [FromForm] IFormFile file)
    {
        var poi = await _db.Pois.FindAsync(id);
        if (poi == null) return NotFound();

        var folder = Path.Combine(_env.WebRootPath, "images", "pois");
        Directory.CreateDirectory(folder);

        var fileName = $"poi_{id}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(folder, fileName);

        using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream);

        poi.ImagePath = $"/images/pois/{fileName}";
        await _db.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return Ok(new { imageUrl = $"{baseUrl}{poi.ImagePath}" });
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

        public string? ImagePath { get; set; }
        public string? ImageUrl { get; set; }
        public List<PoiTranslationDto>? Translations { get; set; }
    }

    public class PoiTranslationDto
    {
        public string Language { get; set; } = "";
        public string Text { get; set; } = "";
    }
}