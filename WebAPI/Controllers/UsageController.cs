using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Models.Entities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsageController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsageController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("events")]
    public async Task<IActionResult> TrackEvent([FromBody] TrackEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            return BadRequest("eventType is required.");
        }

        var sessionId = ResolveGuestSessionId();
        var usageEvent = new AppUsageEvent
        {
            GuestSessionId = sessionId,
            EventType = request.EventType.Trim().ToLowerInvariant(),
            EventValue = request.EventValue?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.AppUsageEvents.Add(usageEvent);
        await _db.SaveChangesAsync();

        return Ok(new { usageEvent.Id, usageEvent.CreatedAtUtc, usageEvent.GuestSessionId });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] int days = 7)
    {
        var safeDays = Math.Clamp(days, 1, 90);
        var utcToday = DateTime.UtcNow.Date;
        var startDate = utcToday.AddDays(-(safeDays - 1));

        var rows = await _db.AppUsageEvents
            .Where(e => e.CreatedAtUtc >= startDate)
            .Select(e => new { Day = e.CreatedAtUtc.Date, e.GuestSessionId, e.EventType })
            .ToListAsync();

        var perDay = rows
            .GroupBy(r => r.Day)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Date = g.Key,
                UniqueUsers = g.Select(x => x.GuestSessionId).Distinct().Count(),
                QrScans = g.Count(x => x.EventType == "qr_scan"),
                AppOpens = g.Count(x => x.EventType == "app_open"),
                TotalEvents = g.Count()
            })
            .ToList();

        var totalUniqueUsers = rows.Select(r => r.GuestSessionId).Distinct().Count();
        var totalQrScans = rows.Count(r => r.EventType == "qr_scan");
        var totalAppOpens = rows.Count(r => r.EventType == "app_open");

        return Ok(new
        {
            Range = new { StartDate = startDate, EndDate = utcToday, Days = safeDays },
            Totals = new
            {
                UniqueUsers = totalUniqueUsers,
                QrScans = totalQrScans,
                AppOpens = totalAppOpens,
                TotalEvents = rows.Count
            },
            PerDay = perDay
        });
    }

    private string ResolveGuestSessionId()
    {
        const string headerName = "X-Guest-Session-Id";

        if (Request.Headers.TryGetValue(headerName, out var headerValues))
        {
            var headerSessionId = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerSessionId))
            {
                return headerSessionId.Trim();
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    public record TrackEventRequest(string EventType, string? EventValue);
}
