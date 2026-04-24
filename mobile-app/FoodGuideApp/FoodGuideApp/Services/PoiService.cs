using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FoodGuideApp.Models;

namespace FoodGuideApp.Services;

public class PoiService
{
    private readonly HttpClient _httpClient;

    public PoiService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
    }

    public async Task<List<Poi>> GetPoisAsync()
    {
        try
        {
            GuestSessionService.AttachTo(_httpClient);

            string url = $"{AppConfig.BaseUrl}/api/poi";

            Debug.WriteLine("========== POI DEBUG START ==========");
            Debug.WriteLine($"[POI API CALL] {url}");

            var response = await _httpClient.GetAsync(url);

            Debug.WriteLine($"[POI API STATUS] {(int)response.StatusCode} - {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"[POI API JSON] {json}");

            response.EnsureSuccessStatusCode();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var data = JsonSerializer.Deserialize<List<Poi>>(json, options);

            Debug.WriteLine($"[POI API SUCCESS] Loaded {data?.Count ?? 0} POIs");
            Debug.WriteLine("========== POI DEBUG END ==========");

            return data ?? new List<Poi>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("========== POI DEBUG ERROR ==========");
            Debug.WriteLine($"[POI API ERROR] {ex}");
            Debug.WriteLine("========== POI DEBUG ERROR END ==========");
            return new List<Poi>();
        }
    }
}