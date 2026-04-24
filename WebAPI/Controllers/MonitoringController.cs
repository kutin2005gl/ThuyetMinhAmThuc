using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitoringController : ControllerBase
{
    private static readonly string[] DayLabels =
    [
        "Thứ 2",
        "Thứ 3",
        "Thứ 4",
        "Thứ 5",
        "Thứ 6",
        "Thứ 7",
        "Chủ nhật"
    ];

    private readonly AppDbContext _db;

    public MonitoringController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<MonitoringSummaryDto>> GetSummary()
    {
        var nowUtc = DateTime.UtcNow;
        var todayUtc = nowUtc.Date;
        var activeSinceUtc = nowUtc.AddSeconds(-10);
        var events = await LoadEventsAsync(null, nowUtc);

        return Ok(new MonitoringSummaryDto
        {
            TotalDevices = CountDevices(events),
            ActiveDevicesNow = CountDevices(events.Where(e => e.EventTimeUtc >= activeSinceUtc)),
            ActiveDevicesToday = CountDevices(events.Where(e => e.EventTimeUtc >= todayUtc)),
            TotalAppOpens = events.Count(IsAppOpen),
            TotalQrScans = events.Count(IsQrScan),
            TotalPoiListens = events.Count(IsPoiListen),
            TotalEvents = events.Count,
            GeneratedAtUtc = nowUtc
        });
    }

    [HttpGet("weekly")]
    public async Task<ActionResult<MonitoringWeeklyDto>> GetWeekly()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var weekStartUtc = GetMonday(todayUtc);
        var weekEndExclusiveUtc = weekStartUtc.AddDays(7);

        var events = await LoadEventsAsync(weekStartUtc, weekEndExclusiveUtc);
        var days = Enumerable.Range(0, 7)
            .Select(index =>
            {
                var day = weekStartUtc.AddDays(index);
                var dayEvents = events
                    .Where(e => e.EventTimeUtc >= day && e.EventTimeUtc < day.AddDays(1))
                    .ToList();

                return new MonitoringWeeklyDayDto
                {
                    Date = day,
                    DayOfWeek = index + 1,
                    Label = DayLabels[index],
                    UniqueDevices = CountDevices(dayEvents),
                    AppOpens = dayEvents.Count(IsAppOpen),
                    QrScans = dayEvents.Count(IsQrScan),
                    PoiListens = dayEvents.Count(IsPoiListen),
                    TotalEvents = dayEvents.Count
                };
            })
            .ToList();

        return Ok(new MonitoringWeeklyDto
        {
            WeekStart = weekStartUtc,
            WeekEnd = weekEndExclusiveUtc.AddDays(-1),
            Days = days
        });
    }

    private async Task<List<MonitoringEventRow>> LoadEventsAsync(DateTime? startUtc, DateTime endExclusiveUtc)
    {
        var usageQuery = _db.AppUsageEvents.AsNoTracking().AsQueryable();
        if (startUtc.HasValue)
        {
            usageQuery = usageQuery.Where(e => e.CreatedAtUtc >= startUtc.Value);
        }

        var usageEvents = await usageQuery
            .Where(e => e.CreatedAtUtc < endExclusiveUtc)
            .Select(e => new MonitoringEventRow(
                e.GuestSessionId,
                e.EventType,
                e.CreatedAtUtc))
            .ToListAsync();

        var analyticsQuery = _db.AnalyticsEvents.AsNoTracking().AsQueryable();
        if (startUtc.HasValue)
        {
            analyticsQuery = analyticsQuery.Where(e => e.CreatedAt >= startUtc.Value);
        }

        var analyticsEvents = await analyticsQuery
            .Where(e => e.CreatedAt < endExclusiveUtc)
            .Select(e => new MonitoringEventRow(
                e.SessionId,
                e.EventType,
                e.CreatedAt))
            .ToListAsync();

        usageEvents.AddRange(analyticsEvents);
        return usageEvents;
    }

    private static DateTime GetMonday(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    private static int CountDevices(IEnumerable<MonitoringEventRow> events)
        => events
            .Select(e => e.SessionId)
            .Where(sessionId => !string.IsNullOrWhiteSpace(sessionId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

    private static bool IsAppOpen(MonitoringEventRow row)
        => IsEvent(row, "app_open", "open_app");

    private static bool IsQrScan(MonitoringEventRow row)
        => IsEvent(row, "qr_scan", "scan_qr", "qr");

    private static bool IsPoiListen(MonitoringEventRow row)
        => IsEvent(row, "listen", "poi_listen", "audio_play", "tts_play", "play_audio");

    private static bool IsEvent(MonitoringEventRow row, params string[] eventTypes)
        => eventTypes.Contains(row.EventType.Trim(), StringComparer.OrdinalIgnoreCase);

    private sealed record MonitoringEventRow(string SessionId, string EventType, DateTime EventTimeUtc);
}

public class MonitoringSummaryDto
{
    public int TotalDevices { get; set; }
    public int ActiveDevicesNow { get; set; }
    public int ActiveDevicesToday { get; set; }
    public int TotalAppOpens { get; set; }
    public int TotalQrScans { get; set; }
    public int TotalPoiListens { get; set; }
    public int TotalEvents { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
}

public class MonitoringWeeklyDto
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public List<MonitoringWeeklyDayDto> Days { get; set; } = [];
}

public class MonitoringWeeklyDayDto
{
    public DateTime Date { get; set; }
    public int DayOfWeek { get; set; }
    public string Label { get; set; } = string.Empty;
    public int UniqueDevices { get; set; }
    public int AppOpens { get; set; }
    public int QrScans { get; set; }
    public int PoiListens { get; set; }
    public int TotalEvents { get; set; }
}
