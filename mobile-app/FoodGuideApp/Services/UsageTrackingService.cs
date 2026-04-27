using System.Net.Http.Json;

namespace FoodGuideApp.Services;

public class UsageTrackingService
{
    private readonly HttpClient _httpClient = new();

    public UsageTrackingService()
    {
        GuestSessionService.AttachTo(_httpClient);
    }

    public Task TrackAppOpenAsync() => TrackEventAsync("app_open", "mobile");

    public Task TrackQrScanAsync(int poiId) => TrackEventAsync("qr_scan", poiId.ToString());

    private async Task TrackEventAsync(string eventType, string? eventValue)
    {
        try
        {
            var request = new { eventType, eventValue };
            await _httpClient.PostAsJsonAsync($"{AppConfig.BaseUrl}/api/usage/events", request);
        }
        catch
        {
            // Không làm gián đoạn luồng người dùng nếu tracking lỗi.
        }
    }
}
