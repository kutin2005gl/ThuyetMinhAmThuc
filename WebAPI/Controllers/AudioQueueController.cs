using Microsoft.AspNetCore.Mvc;
using WebAPI.Services;

namespace WebAPI.Controllers;

// Controller MỚI — KHÔNG sửa TtsController cũ.
// Tiếp nhận audio request từ nhiều user đồng thời,
// đẩy vào MultiUserAudioQueueService để xử lý tuần tự, tránh phát trùng.
[ApiController]
[Route("api/[controller]")]
public class AudioQueueController : ControllerBase
{
    private readonly IMultiUserAudioQueueService _audioQueue;

    public AudioQueueController(IMultiUserAudioQueueService audioQueue)
    {
        _audioQueue = audioQueue;
    }

    // POST api/audioqueue/enqueue
    // Nhận yêu cầu audio từ user, thêm vào hàng đợi và chờ kết quả.
    [HttpPost("enqueue")]
    public async Task<IActionResult> Enqueue(
        [FromBody] AudioQueueRequest body,
        CancellationToken cancellationToken)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.Text))
            return BadRequest(new { message = "Text không được rỗng." });

        if (body.PoiId <= 0)
            return BadRequest(new { message = "PoiId không hợp lệ." });

        var request = new MultiUserAudioRequest
        {
            UserId = body.UserId ?? "anonymous",
            PoiId = body.PoiId,
            Language = body.Language ?? "vi",
            Text = body.Text,
            Priority = body.Priority,
        };

        string audioKey = await _audioQueue.EnqueueAsync(request, cancellationToken);

        if (string.IsNullOrEmpty(audioKey))
        {
            return Ok(new
            {
                message = "Request bị bỏ qua (trùng hoặc cooldown).",
                audioKey = (string?)null,
                pending = _audioQueue.PendingCount,
            });
        }

        return Ok(new
        {
            message = "Xử lý audio thành công.",
            audioKey,
            pending = _audioQueue.PendingCount,
        });
    }

    // GET api/audioqueue/status
    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new { pending = _audioQueue.PendingCount });
    }
}

public record AudioQueueRequest(
    string? UserId,
    int PoiId,
    string Text,
    string? Language,
    int Priority = 1
);