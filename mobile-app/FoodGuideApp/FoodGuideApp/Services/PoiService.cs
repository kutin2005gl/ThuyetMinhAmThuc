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
    private readonly HttpClient _httpClient = new();

    public async Task<List<Poi>> GetPoisAsync()
    {
        try
        {
            string url = "http://10.0.2.2:5000/api/poi";
            var json = await _httpClient.GetStringAsync(url);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var data = JsonSerializer.Deserialize<List<Poi>>(json, options);
            return data ?? new List<Poi>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[POI API ERROR] {ex.Message}");
            return new List<Poi>();
        }
    }
}