using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Models.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AnalyticsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("event")]
    public async Task<IActionResult> TrackEvent([FromBody] AnalyticsEventDto dto)
    {
        _db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            SessionId = dto.SessionId,
            EventType = dto.EventType,
            PoiId = dto.PoiId,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            DurationSeconds = dto.DurationSeconds,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

    // Giữ nguyên hoặc điều chỉnh thời gian active tùy nhu cầu
    [HttpGet("active-users")]
    public async Task<IActionResult> GetActiveUsers()
    {
<<<<<<< Updated upstream
        var since = DateTime.UtcNow.AddMinutes(-5);
=======
        var since = DateTime.UtcNow.AddMinutes(-5); // Thường tính 5 phút gần nhất
>>>>>>> Stashed changes
        var count = await _db.AnalyticsEvents
            .Where(e => e.CreatedAt >= since)
            .Select(e => e.SessionId)
            .Distinct()
            .CountAsync();
        return Ok(new { activeUsers = count });
    }

    // Sửa lại để lấy tên POI chính xác cho trang Analytics
    [HttpGet("top-pois")]
    public async Task<IActionResult> GetTopPois()
    {
        var top = await _db.AnalyticsEvents
            .Where(e => e.EventType == "listen" && e.PoiId != null)
            .GroupBy(e => e.PoiId)
            .Select(g => new
            {
                PoiId = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync();

        // Lấy danh sách ID để map tên một lần duy nhất (tối ưu performance)
        var poiIds = top.Select(x => x.PoiId).ToList();
        var poiNames = await _db.Pois
            .Where(p => poiIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        var result = top.Select(x => new
        {
            PoiName = poiNames.ContainsKey(x.PoiId!.Value) ? poiNames[x.PoiId.Value] : "Nơi chốn bí ẩn",
            Count = x.Count
        });

        return Ok(result);
    }

    // Thời gian trung bình nghe (Chỉ tính những lượt nghe thực tế > 0 giây)
    [HttpGet("avg-duration")]
    public async Task<IActionResult> GetAvgDuration()
    {
        var avgData = await _db.AnalyticsEvents
            .Where(e => e.EventType == "listen" && e.PoiId != null && e.DurationSeconds > 0)
            .GroupBy(e => e.PoiId)
            .Select(g => new
            {
                PoiId = g.Key,
                AvgSeconds = (int)g.Average(e => e.DurationSeconds ?? 0)
            })
            .ToListAsync();

        var poiIds = avgData.Select(x => x.PoiId).ToList();
        var poiNames = await _db.Pois
            .Where(p => poiIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        var result = avgData.Select(x => new
        {
            PoiName = poiNames.ContainsKey(x.PoiId!.Value) ? poiNames[x.PoiId.Value] : "Không xác định",
            AvgSeconds = x.AvgSeconds
        });

        return Ok(result);
    }

    [HttpGet("heatmap")]
    public async Task<IActionResult> GetHeatmap()
    {
        // Chỉ lấy những điểm có tọa độ hợp lệ
        var points = await _db.AnalyticsEvents
            .Where(e => e.Latitude != null && e.Longitude != null && e.Latitude != 0)
            .Select(e => new { e.Latitude, e.Longitude })
            .ToListAsync();
        return Ok(points);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var today = DateTime.UtcNow.Date;
        var total = await _db.AnalyticsEvents
            .Select(e => e.SessionId).Distinct().CountAsync();
        var todayCount = await _db.AnalyticsEvents
            .Where(e => e.CreatedAt >= today)
            .Select(e => e.SessionId).Distinct().CountAsync();
        var totalListens = await _db.AnalyticsEvents
            .CountAsync(e => e.EventType == "listen");

        return Ok(new { total, todayCount, totalListens });
    }
}

public record AnalyticsEventDto(
    string SessionId,
    string EventType,
    int? PoiId,
    double? Latitude,
    double? Longitude,
    int? DurationSeconds
);