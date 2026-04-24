using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TtsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TtsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] TtsRequest request)
    {
        if (request.PoiId <= 0)
        {
            return BadRequest(new { message = "PoiId không hợp lệ.", audioUrl = (string?)null });
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { message = "Nội dung thuyết minh không được rỗng.", audioUrl = (string?)null });
        }

        if (string.IsNullOrWhiteSpace(request.Language))
        {
            return BadRequest(new { message = "Language không được rỗng.", audioUrl = (string?)null });
        }

        var poiExists = await _db.Pois.AnyAsync(p => p.Id == request.PoiId);
        if (!poiExists)
        {
            return NotFound(new { message = $"Không tìm thấy POI #{request.PoiId}.", audioUrl = (string?)null });
        }

        var language = request.Language.Trim().ToLowerInvariant();
        var languageConfigured = await _db.SupportedLanguages.AnyAsync(l => l.Code == language);
        if (!languageConfigured)
        {
            return BadRequest(new { message = $"Ngôn ngữ '{request.Language}' chưa được cấu hình.", audioUrl = (string?)null });
        }

        return Ok(new
        {
            message = "Đã lưu/cập nhật nội dung thuyết minh. App mobile sẽ dùng Text-to-Speech trên thiết bị để đọc.",
            audioUrl = (string?)null
        });
    }
}

public record TtsRequest(int PoiId, string Text, string Language);
