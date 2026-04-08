using Microsoft.AspNetCore.Mvc;
using WebAPI.Services.Interfaces;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TtsController : ControllerBase
{
    private readonly ITtsGeneratorService _tts;

    public TtsController(ITtsGeneratorService tts)
    {
        _tts = tts;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] TtsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Text không được để trống.");

        var audioUrl = await _tts.GenerateAsync(
            request.PoiId,
            request.Text,
            request.Language ?? "vi");

        return Ok(new { audioUrl, poiId = request.PoiId });
    }

    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok(new
        {
            message = "TTS API hoạt động bình thường",
            voices = new[]
            {
                new { language = "vi", voice = "vi-VN-HoaiMyNeural" },
                new { language = "en", voice = "en-US-JennyNeural" },
                new { language = "zh", voice = "zh-CN-XiaoxiaoNeural" }
            },
            note = "Cần Azure Key để generate audio thật"
        });
    }
}

public record TtsRequest(string PoiId, string Text, string? Language);