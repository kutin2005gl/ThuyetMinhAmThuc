using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using WebAPI.Data;
using WebAPI.Models.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;
    private static readonly string[] ListenEventTypes = ["poi_listen", "listen", "audio_play", "tts_play", "play_audio"];
    private static readonly string[] LocationEventTypes = ["location_update", "location"];
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(5);
    private const int LocationThrottleSeconds = 30;

    public AnalyticsController(AppDbContext db)
    {
        _db = db;
    }

    // Công dụng: nhận event ẩn danh từ mobile app, chuẩn hóa tên event mới/cũ và lưu vào lịch sử analytics.
    [HttpPost("event")]
    public async Task<IActionResult> TrackEvent([FromBody] AnalyticsEventDto? dto)
    {
        if (dto == null)
        {
            return BadRequest("event payload is required.");
        }

        var eventType = NormalizeEventType(dto.EventType);
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return BadRequest("eventType is not supported.");
        }

        var sessionId = ResolveGuestSessionId(dto);
        var now = DateTime.UtcNow;
        var latitude = dto.Latitude;
        var longitude = dto.Longitude;

        if (eventType == "location_update")
        {
            if (!IsValidLocation(latitude, longitude))
            {
                return BadRequest("valid latitude and longitude are required for location_update.");
            }

            var latestLocationAt = await _db.AnalyticsEvents
                .AsNoTracking()
                .Where(e => e.SessionId == sessionId && LocationEventTypes.Contains(e.EventType.ToLower()))
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => e.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestLocationAt != default && now - latestLocationAt < TimeSpan.FromSeconds(LocationThrottleSeconds))
            {
                return Ok(new { skipped = true, reason = "location_throttled", eventType });
            }
        }
        else if (!IsValidLocation(latitude, longitude))
        {
            latitude = null;
            longitude = null;
        }

        _db.AnalyticsEvents.Add(new AnalyticsEvent
        {
            SessionId = sessionId,
            EventType = eventType,
            PoiId = dto.PoiId,
            Latitude = latitude,
            Longitude = longitude,
            DurationSeconds = NormalizeDuration(dto.DurationSeconds),
            CreatedAt = now
        });
        await _db.SaveChangesAsync();
        return Ok(new { eventType, createdAt = now });
    }

    // Công dụng: đếm số thiết bị/session ẩn danh có event trong cửa sổ hoạt động gần đây.
    [HttpGet("active-users")]
    public async Task<IActionResult> GetActiveUsers()
    {
        var since = DateTime.UtcNow.Subtract(ActiveWindow);
        var count = await _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.CreatedAt >= since)
            .Select(e => e.SessionId)
            .Where(sessionId => sessionId != "")
            .Distinct()
            .CountAsync();

        return Ok(new { activeUsers = count, activeWindowMinutes = (int)ActiveWindow.TotalMinutes });
    }

    // Công dụng: thống kê các POI có lượt nghe nhiều nhất, gồm cả event poi_listen mới và listen cũ.
    [HttpGet("top-pois")]
    public async Task<IActionResult> GetTopPois()
    {
        var topRows = await _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => ListenEventTypes.Contains(e.EventType.ToLower()) && e.PoiId != null)
            .GroupBy(e => e.PoiId)
            .Select(g => new
            {
                PoiId = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync();

        var poiIds = topRows.Select(x => x.PoiId!.Value).ToList();
        var poiNames = await _db.Pois
            .AsNoTracking()
            .Where(p => poiIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        var top = topRows.Select(x => new
        {
            x.PoiId,
            PoiName = poiNames.GetValueOrDefault(x.PoiId!.Value) ?? $"POI #{x.PoiId}",
            x.Count
        });

        return Ok(top);
    }

    // Công dụng: tính thời gian nghe trung bình theo từng POI từ các event nghe hợp lệ.
    [HttpGet("avg-duration")]
    public async Task<IActionResult> GetAvgDuration()
    {
        var avgRows = await _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => ListenEventTypes.Contains(e.EventType.ToLower()) && e.PoiId != null && e.DurationSeconds > 0)
            .GroupBy(e => e.PoiId)
            .Select(g => new
            {
                PoiId = g.Key,
                AvgSeconds = g.Average(e => (double)e.DurationSeconds!.Value)
            })
            .ToListAsync();

        var poiIds = avgRows.Select(x => x.PoiId!.Value).ToList();
        var poiNames = await _db.Pois
            .AsNoTracking()
            .Where(p => poiIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        var avg = avgRows
            .OrderByDescending(x => x.AvgSeconds)
            .Select(x => new
            {
                x.PoiId,
                PoiName = poiNames.GetValueOrDefault(x.PoiId!.Value) ?? $"POI #{x.PoiId}",
                AvgSeconds = (int)Math.Round(x.AvgSeconds)
            });

        return Ok(avg);
    }

    // Công dụng: trả về các điểm GPS đã làm tròn và gộp số lần xuất hiện để vẽ heatmap không lộ tuyến chi tiết.
    [HttpGet("heatmap")]
    public async Task<IActionResult> GetHeatmap([FromQuery] int days = 7, [FromQuery] int take = 500)
    {
        var safeDays = Math.Clamp(days, 1, 90);
        var safeTake = Math.Clamp(take, 50, 1000);
        var since = DateTime.UtcNow.Date.AddDays(-(safeDays - 1));

        var rows = await _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e =>
                e.CreatedAt >= since &&
                LocationEventTypes.Contains(e.EventType.ToLower()) &&
                e.Latitude >= -90 &&
                e.Latitude <= 90 &&
                e.Longitude >= -180 &&
                e.Longitude <= 180)
            .OrderByDescending(e => e.CreatedAt)
            .Take(safeTake * 5)
            .Select(e => new { Latitude = e.Latitude!.Value, Longitude = e.Longitude!.Value })
            .ToListAsync();

        var points = rows
            .GroupBy(p => new
            {
                Latitude = Math.Round(p.Latitude, 4),
                Longitude = Math.Round(p.Longitude, 4)
            })
            .Select(g => new
            {
                g.Key.Latitude,
                g.Key.Longitude,
                Count = g.Count()
            })
            .OrderByDescending(p => p.Count)
            .Take(safeTake)
            .ToList();

        return Ok(points);
    }

    // Công dụng: nhóm các điểm GPS theo session để xem tuyến di chuyển.
    [HttpGet("routes")]
    public async Task<IActionResult> GetRoutes()
    {
        var routes = await _db.AnalyticsEvents
            .Where(e => LocationEventTypes.Contains(e.EventType.ToLower()) && e.Latitude != null && e.Longitude != null)
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

    // Công dụng: tổng hợp các chỉ số chính cho dashboard analytics theo session ẩn danh.
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var today = DateTime.UtcNow.Date;
        var total = await _db.AnalyticsEvents
            .AsNoTracking()
            .Select(e => e.SessionId)
            .Distinct()
            .CountAsync();
        var todayCount = await _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.CreatedAt >= today)
            .Select(e => e.SessionId)
            .Distinct()
            .CountAsync();
        var totalListens = await _db.AnalyticsEvents
            .AsNoTracking()
            .CountAsync(e => ListenEventTypes.Contains(e.EventType.ToLower()));
        var qrScans = await _db.AnalyticsEvents
            .AsNoTracking()
            .CountAsync(e => e.EventType.ToLower() == "qr_scan");
        var appOpens = await _db.AnalyticsEvents
            .AsNoTracking()
            .CountAsync(e => e.EventType.ToLower() == "app_open");

        return Ok(new { total, todayCount, totalListens, qrScans, appOpens });
    }

    // Công dụng: thống kê event theo từng ngày trong tuần để WebAdmin vẽ biểu đồ.
    [HttpGet("trend")]
    public async Task<IActionResult> GetTrend([FromQuery] int days = 7)
    {
        var safeDays = Math.Clamp(days, 1, 31);
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-(safeDays - 1));

        var rows = await _db.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.CreatedAt >= startDate)
            .Select(e => new
            {
                Date = e.CreatedAt.Date,
                e.SessionId,
                EventType = e.EventType.ToLower()
            })
            .ToListAsync();

        var trend = Enumerable.Range(0, safeDays)
            .Select(offset => startDate.AddDays(offset))
            .Select(day =>
            {
                var dayRows = rows.Where(r => r.Date == day).ToList();
                return new
                {
                    Date = day,
                    Label = GetWeekdayLabel(day.DayOfWeek),
                    UniqueDevices = dayRows.Select(r => r.SessionId).Distinct().Count(),
                    TotalEvents = dayRows.Count,
                    AppOpens = dayRows.Count(r => r.EventType == "app_open"),
                    QrScans = dayRows.Count(r => r.EventType == "qr_scan"),
                    PoiListens = dayRows.Count(r => ListenEventTypes.Contains(r.EventType)),
                    LocationUpdates = dayRows.Count(r => LocationEventTypes.Contains(r.EventType))
                };
            })
            .ToList();

        return Ok(trend);
    }

    // Công dụng: trả về lịch sử event gần đây để WebAdmin theo dõi hoạt động app.
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents([FromQuery] int take = 30)
    {
        var safeTake = Math.Clamp(take, 10, 100);
        var events = await _db.AnalyticsEvents
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(safeTake)
            .Select(e => new
            {
                e.Id,
                e.SessionId,
                e.EventType,
                e.PoiId,
                PoiName = _db.Pois
                    .Where(p => p.Id == e.PoiId)
                    .Select(p => p.Name)
                    .FirstOrDefault(),
                e.Latitude,
                e.Longitude,
                e.DurationSeconds,
                e.CreatedAt
            })
            .ToListAsync();

        return Ok(events);
    }

    // Công dụng: chuyển tên event cũ và mới về một chuẩn lưu trữ thống nhất.
    private static string NormalizeEventType(string? eventType)
    {
        var normalized = eventType?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "app_open" or "open_app" => "app_open",
            "qr_scan" or "scan_qr" or "qr" => "qr_scan",
            "poi_listen" or "listen" or "audio_play" or "tts_play" or "play_audio" => "poi_listen",
            "location_update" or "location" => "location_update",
            "enter_poi" or "exit_poi" => normalized,
            _ => string.Empty
        };
    }

    // Công dụng: lấy guest_session_id ẩn danh từ body/header, nếu thiếu thì tạo session tạm không chứa PII.
    private string ResolveGuestSessionId(AnalyticsEventDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.GuestSessionId))
        {
            return dto.GuestSessionId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(dto.GuestSessionIdSnake))
        {
            return dto.GuestSessionIdSnake.Trim();
        }

        if (!string.IsNullOrWhiteSpace(dto.SessionId))
        {
            return dto.SessionId.Trim();
        }

        if (Request.Headers.TryGetValue("X-Guest-Session-Id", out var headerValues))
        {
            var headerSessionId = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerSessionId))
            {
                return headerSessionId.Trim();
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    // Công dụng: kiểm tra tọa độ GPS nằm trong biên hợp lệ trước khi lưu hoặc hiển thị heatmap.
    private static bool IsValidLocation(double? latitude, double? longitude)
        => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    // Công dụng: giới hạn duration nghe để tránh số liệu âm hoặc bất thường làm lệch trung bình.
    private static int? NormalizeDuration(int? durationSeconds)
    {
        if (durationSeconds is not > 0)
        {
            return null;
        }

        return Math.Min(durationSeconds.Value, 24 * 60 * 60);
    }

    // Công dụng: tạo nhãn thứ trong tuần cho biểu đồ analytics của Admin.
    private static string GetWeekdayLabel(DayOfWeek dayOfWeek)
        => dayOfWeek switch
        {
            DayOfWeek.Monday => "T2",
            DayOfWeek.Tuesday => "T3",
            DayOfWeek.Wednesday => "T4",
            DayOfWeek.Thursday => "T5",
            DayOfWeek.Friday => "T6",
            DayOfWeek.Saturday => "T7",
            _ => "CN"
        };
}

public class AnalyticsEventDto
{
    public string? SessionId { get; set; }

    public string? GuestSessionId { get; set; }

    [JsonPropertyName("guest_session_id")]
    public string? GuestSessionIdSnake { get; set; }

    public string? EventType { get; set; }
    public int? PoiId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? DurationSeconds { get; set; }
}
