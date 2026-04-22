namespace WebAPI.Models.Entities;

public class AppUsageEvent
{
    public int Id { get; set; }
    public string GuestSessionId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? EventValue { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
