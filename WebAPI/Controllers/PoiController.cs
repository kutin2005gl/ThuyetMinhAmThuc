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

    public PoiController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var pois = await _db.Pois
            .Include(p => p.Translations)
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Latitude,
                p.Longitude,
                p.RadiusMeters,
                Translations = p.Translations.Select(t => new
                {
                    t.Language,
                    t.Text
                })
            })
            .ToListAsync();

        return Ok(pois);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var poi = await _db.Pois
            .Include(p => p.Translations)
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Latitude,
                p.Longitude,
                p.RadiusMeters,
                Translations = p.Translations.Select(t => new
                {
                    t.Language,
                    t.Text
                })
            })
            .FirstOrDefaultAsync();

        if (poi == null)
            return NotFound();

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
            CreatedAt = new DateTime(2024, 1, 1)
        };
        _db.Pois.Add(poi);
        await _db.SaveChangesAsync();
        return Ok(poi);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] PoiCreateDto dto)
    {
        var poi = await _db.Pois.FindAsync(id);
        if (poi == null) return NotFound();

        poi.Name = dto.Name;
        poi.Description = dto.Description;
        poi.Latitude = dto.Latitude;
        poi.Longitude = dto.Longitude;
        poi.RadiusMeters = dto.RadiusMeters;

        await _db.SaveChangesAsync();
        return Ok(poi);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var poi = await _db.Pois.FindAsync(id);
        if (poi == null) return NotFound();

        _db.Pois.Remove(poi);
        await _db.SaveChangesAsync();
        return Ok();
    }

    public record PoiCreateDto(
        string Name,
        string Description,
        double Latitude,
        double Longitude,
        double RadiusMeters
    );
}