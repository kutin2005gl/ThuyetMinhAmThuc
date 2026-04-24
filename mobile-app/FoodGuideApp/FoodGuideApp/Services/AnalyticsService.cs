using System.Text;
using System.Text.Json;

namespace FoodGuideApp.Services;

public class AnalyticsService
{
    private readonly HttpClient _httpClient;

    public AnalyticsService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(AppConfig.BaseUrl)
        };

        _httpClient.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");

        GuestSessionService.AttachTo(_httpClient);
    }

    private async Task SendEvent(
        string eventType,
        int? poiId = null,
        double? lat = null,
        double? lng = null,
        int? duration = null)
    {
        try
        {
            var payload = new
            {
                SessionId = GuestSessionService.GetOrCreateSessionId(),
                EventType = eventType,
                PoiId = poiId,
                Latitude = lat,
                Longitude = lng,
                DurationSeconds = duration
            };

            var json = JsonSerializer.Serialize(payload);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await _httpClient.PostAsync("/api/analytics/event", content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ANALYTICS ERROR] {ex.Message}");
        }
    }

    // ===== PUBLIC METHODS =====

    public Task TrackAppOpen()
        => SendEvent("app_open");

    public Task TrackLocation(double lat, double lng)
        => SendEvent("location", null, lat, lng);

    public Task TrackEnterPoi(int poiId)
        => SendEvent("enter_poi", poiId);

    public Task TrackListen(int poiId, int durationSeconds)
        => SendEvent("listen", poiId, duration: durationSeconds);

    public Task TrackQrScan(int poiId)
        => SendEvent("qr_scan", poiId);
}