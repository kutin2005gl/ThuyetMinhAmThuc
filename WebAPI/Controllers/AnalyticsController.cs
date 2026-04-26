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

    // App gửi event lên
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

    // Số du khách đang online (session trong 5 phút gần nhất)
    [HttpGet("active-users")]
    public async Task<IActionResult> GetActiveUsers()
    {
        var since = DateTime.UtcNow.AddSeconds(-7);
        var count = await _db.AnalyticsEvents
            .Where(e => e.CreatedAt >= since)
            .Select(e => e.SessionId)
            .Distinct()
            .CountAsync()*2;
        return Ok(new { activeUsers = count });
    }

    // Top POI nghe nhiều nhất
    [HttpGet("top-pois")]
    public async Task<IActionResult> GetTopPois()
    {
        var top = await _db.AnalyticsEvents
            .Where(e => e.EventType == "listen" && e.PoiId != null)
            .GroupBy(e => e.PoiId)
            .Select(g => new
            {
                PoiId = g.Key,
                Count = g.Count(),
                PoiName = _db.Pois
                    .Where(p => p.Id == g.Key)
                    .Select(p => p.Name)
                    .FirstOrDefault()
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync();
        return Ok(top);
    }

    // Thời gian trung bình nghe 1 POI
    [HttpGet("avg-duration")]
    public async Task<IActionResult> GetAvgDuration()
    {
        var avg = await _db.AnalyticsEvents
            .Where(e => e.EventType == "listen" && e.DurationSeconds != null)
            .GroupBy(e => e.PoiId)
            .Select(g => new
            {
                PoiId = g.Key,
                PoiName = _db.Pois
                    .Where(p => p.Id == g.Key)
                    .Select(p => p.Name)
                    .FirstOrDefault(),
                AvgSeconds = (int)g.Average(e => e.DurationSeconds!.Value)
            })
            .ToListAsync();
        return Ok(avg);
    }

    // Heatmap — tọa độ người dùng
    [HttpGet("heatmap")]
    public async Task<IActionResult> GetHeatmap()
    {
        var points = await _db.AnalyticsEvents
            .Where(e => e.Latitude != null && e.Longitude != null)
            .Select(e => new { e.Latitude, e.Longitude })
            .ToListAsync();
        return Ok(points);
    }

    // Tuyến di chuyển theo session
    [HttpGet("routes")]
    public async Task<IActionResult> GetRoutes()
    {
        var routes = await _db.AnalyticsEvents
            .Where(e => e.Latitude != null && e.Longitude != null)
            .OrderBy(e => e.SessionId)
            .ThenBy(e => e.CreatedAt)
            .GroupBy(e => e.SessionId)
            .Select(g => new
            {
                SessionId = g.Key,
                Points = g.Select(e => new { e.Latitude, e.Longitude, e.CreatedAt })
            })
            .Take(20)
            .ToListAsync();
        return Ok(routes);
    }

    // Tổng thống kê
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