using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Models.Entities;
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranslationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TranslateService _translator;

    public TranslationController(AppDbContext db, TranslateService translator)
    {
        _db = db;
        _translator = translator;
    }

    [HttpGet("{poiId}")]
    public async Task<IActionResult> GetByPoi(int poiId)
    {
        var translations = await _db.Translations
            .Where(t => t.PoiId == poiId)
            .ToListAsync();

        // Kiểm tra file thực tế trên server để trả về trạng thái HasAudio chính xác
        var result = translations.Select(t => new {
            t.Id,
            t.PoiId,
            t.Language,
            t.Text,
            HasAudio = System.IO.File.Exists(Path.Combine("wwwroot/audio", $"tts_{poiId}_{t.Language}.mp3"))
        });

        return Ok(result);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var byLanguage = await _db.Translations
            .GroupBy(t => t.Language)
            .Select(g => new { Language = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(new
        {
            Total = byLanguage.Sum(x => x.Count),
            ByLanguage = byLanguage
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] TranslationDto dto)
    {
        // 1. Lưu bản dịch gốc (ngôn ngữ mà người dùng vừa nhập)
        await SaveSingle(dto.PoiId, dto.Language, dto.Text);
        await _db.SaveChangesAsync();

        // 2. Lấy danh sách ngôn ngữ ĐANG HỖ TRỢ từ Database
        // Loại trừ ngôn ngữ gốc để không dịch đè lên chính nó
        var targetLanguages = await _db.SupportedLanguages
            .Where(l => l.Code != dto.Language)
            .ToListAsync();

        // 3. Vòng lặp dịch tự động sang tất cả các ngôn ngữ trong DB
        foreach (var lang in targetLanguages)
        {
            try
            {
                var translatedText = await _translator.TranslateAsync(dto.Text, dto.Language, lang.Code);
                await SaveSingle(dto.PoiId, lang.Code, translatedText);
            }
            catch (Exception ex)
            {
                // Nếu lỗi 1 ngôn ngữ thì bỏ qua để dịch tiếp ngôn ngữ khác
                Console.WriteLine($"Lỗi dịch sang {lang.Code}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Đã dịch thành công sang các ngôn ngữ hệ thống hỗ trợ!" });
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

    private async Task SaveSingle(int poiId, string language, string text)
    {
        var existing = await _db.Translations
            .FirstOrDefaultAsync(t => t.PoiId == poiId && t.Language == language);

        if (existing != null)
            existing.Text = text;
        else
            _db.Translations.Add(new Translation
            {
                PoiId = poiId,
                Language = language,
                Text = text
            });
    }
}

public record TranslationDto(int PoiId, string Language, string Text);