using System.Text.Json;
using FoodGuideApp.Models;

namespace FoodGuideApp.Services;

public class TourApiService
{
    private readonly HttpClient _httpClient;

    public TourApiService()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri($"{AppConfig.BaseUrl}/");
    }

    public async Task<List<Tour>> GetToursAsync()
    {
        try
        {
            GuestSessionService.AttachTo(_httpClient);
            var response = await _httpClient.GetAsync("api/tour");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var tours = JsonSerializer.Deserialize<List<Tour>>(json, options);
            return tours ?? new List<Tour>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TOUR API ERROR] {ex.Message}");
            return new List<Tour>();
        }
    }
}
