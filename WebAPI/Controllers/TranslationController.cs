using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Models.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranslationController : ControllerBase
{
    private readonly AppDbContext _db;

    public TranslationController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{poiId}")]
    public async Task<IActionResult> GetByPoi(int poiId)
    {
        var translations = await _db.Translations
            .Where(t => t.PoiId == poiId)
            .ToListAsync();
        return Ok(translations);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] TranslationDto dto)
    {
        var existing = await _db.Translations
            .FirstOrDefaultAsync(t => t.PoiId == dto.PoiId && t.Language == dto.Language);

        if (existing != null)
        {
            existing.Text = dto.Text;
        }
        else
        {
            _db.Translations.Add(new Translation
            {
                PoiId = dto.PoiId,
                Language = dto.Language,
                Text = dto.Text
            });
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await _db.Translations.FindAsync(id);
        if (t == null) return NotFound();
        _db.Translations.Remove(t);
        await _db.SaveChangesAsync();
        return Ok();
    }
}

public record TranslationDto(int PoiId, string Language, string Text);