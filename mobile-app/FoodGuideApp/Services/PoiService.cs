using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FoodGuideApp.Models;
using Microsoft.Maui.Networking;

namespace FoodGuideApp.Services;

public class PoiService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool LastResultFromCache { get; private set; }

    public PoiService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("ngrok-skip-browser-warning", "true");
    }

    public async Task<List<Poi>> GetPoisAsync()
    {
        LastResultFromCache = false;

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return await LoadCachedPoisAsync("offline");
        }

        try
        {
            GuestSessionService.AttachTo(_httpClient);

            string url = $"{AppConfig.BaseUrl.TrimEnd('/')}/api/poi";

            Debug.WriteLine("========== POI DEBUG START ==========");
            Debug.WriteLine($"[POI API CALL] {url}");

            var response = await _httpClient.GetAsync(url);

            Debug.WriteLine($"[POI API STATUS] {(int)response.StatusCode} - {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"[POI API JSON LENGTH] {json.Length}");

            response.EnsureSuccessStatusCode();

            var data = JsonSerializer.Deserialize<List<Poi>>(json, JsonOptions) ?? new List<Poi>();
            await PoiCacheService.SaveAsync(data);

            Debug.WriteLine($"[POI API SUCCESS] Loaded {data.Count} POIs");
            Debug.WriteLine("========== POI DEBUG END ==========");

            return data;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("========== POI DEBUG ERROR ==========");
            Debug.WriteLine($"[POI API ERROR] {ex}");
            Debug.WriteLine("========== POI DEBUG ERROR END ==========");
            return await LoadCachedPoisAsync("api-error");
        }
    }

    // Công dụng: dùng cache POI local khi API lỗi hoặc thiết bị không có mạng.
    private async Task<List<Poi>> LoadCachedPoisAsync(string reason)
    {
        var cachedPois = await PoiCacheService.LoadAsync();
        LastResultFromCache = cachedPois.Count > 0;

        Debug.WriteLine($"[POI CACHE] reason={reason} | count={cachedPois.Count}");
        return cachedPois;
    }
}
