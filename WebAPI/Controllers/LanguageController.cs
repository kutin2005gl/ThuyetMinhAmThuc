using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LanguageController : ControllerBase
{
    private readonly AppDbContext _db;
    public LanguageController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.SupportedLanguages.ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] SupportedLanguage lang)
    {
        _db.SupportedLanguages.Add(lang);
        await _db.SaveChangesAsync();
        return Ok(lang);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var lang = await _db.SupportedLanguages.FindAsync(id);
        if (lang == null) return NotFound();
        _db.SupportedLanguages.Remove(lang);
        await _db.SaveChangesAsync();
        return Ok();
    }
}