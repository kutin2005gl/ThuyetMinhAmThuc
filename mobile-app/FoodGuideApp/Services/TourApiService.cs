using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FoodGuideApp.Models;

namespace FoodGuideApp.Services;

public class TourApiService
{
    private readonly HttpClient _httpClient;

    public TourApiService()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri($"{AppConfig.BaseUrl}/");
        _httpClient.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
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

            System.Diagnostics.Debug.WriteLine($"[TOUR API SUCCESS] {tours?.Count ?? 0} tours");

            return tours ?? new List<Tour>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TOUR API ERROR] {ex}");
            return new List<Tour>();
        }
    }
}