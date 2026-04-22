namespace WebAPI.Models.Entities;

public class AnalyticsEvent
{
    public int Id { get; set; }
    public string SessionId { get; set; } = ""; // ẩn danh
    public string EventType { get; set; } = ""; // "listen", "enter_poi", "exit_poi"
    public int? PoiId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? DurationSeconds { get; set; } // thời gian nghe
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}