using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Thêm dòng này để dùng FirstOrDefaultAsync
using WebAPI.Data;                 // Thêm namespace chứa AppDbContext (thường là WebAPI.Data)
using WebAPI.Services;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TtsController : ControllerBase
{
    private readonly AudioService _audio;
    private readonly AppDbContext _db;

    public TtsController(AudioService audio, AppDbContext db)
    {
        _audio = audio;
        _db = db;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] TtsRequest request)
    {
        // 1. Tìm cấu hình ngôn ngữ trong Database
        var langConfig = await _db.SupportedLanguages
            .FirstOrDefaultAsync(l => l.Code == request.Language);

        if (langConfig == null)
        {
            return BadRequest(new { message = $"Ngôn ngữ '{request.Language}' chưa được cấu hình." });
        }

        string fileName = $"tts_{request.PoiId}_{request.Language}.mp3";

        // 2. Truyền VoiceName từ DB vào Service
        var audioUrl = await _audio.GenerateSpeech(
            request.PoiId,
            request.Text,
            request.Language,
            fileName
        // Nếu AudioService của bạn chưa nhận tham số thứ 5 (voiceName), 
        // hãy tạm thời xóa dòng langConfig.VoiceName ở đây để build thành công.
        );

        if (!string.IsNullOrEmpty(audioUrl))
        {
            return Ok(new { audioUrl = audioUrl });
        }

        return BadRequest(new { message = "Lỗi từ dịch vụ Google TTS." });
    }
}

// QUAN TRỌNG: Định nghĩa TtsRequest ở đây nếu bạn không để nó ở file riêng
public record TtsRequest(int PoiId, string Text, string Language);