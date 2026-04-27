using System.Text.Json;
using FoodGuideApp.Models;
using Microsoft.Maui.Storage;

namespace FoodGuideApp.Services;

public static class PoiNavigationService
{
    public static async Task<bool> LoadPoiToPreferencesAsync(int poiId)
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{AppConfig.BaseUrl}/")
        };

        GuestSessionService.AttachTo(httpClient);

        var response = await httpClient.GetAsync($"api/poi/{poiId}");
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var json = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var poi = JsonSerializer.Deserialize<Poi>(json, options);
        if (poi == null)
        {
            return false;
        }

        var description = GetPoiTextByLanguage(poi, Preferences.Get("app_language", "vi"));

        Preferences.Set("poi_name", poi.Name ?? "Chưa có POI");
        Preferences.Set("poi_description", string.IsNullOrWhiteSpace(description) ? "Không có mô tả" : description);
        Preferences.Set("poi_image_url", poi.ImageUrl ?? "");
        Preferences.Set("poi_distance", "0.0");
        Preferences.Set("highlight_poi_id", poi.Id);

        return true;
    }

    private static string GetPoiTextByLanguage(Poi poi, string currentLanguage)
    {
        var lang = (currentLanguage ?? "vi").Trim();

        if (poi.Translations != null && poi.Translations.Count > 0)
        {
            var exact = poi.Translations.FirstOrDefault(t =>
                !string.IsNullOrWhiteSpace(t.Language) &&
                string.Equals(t.Language.Trim(), lang, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(t.Text));

            if (exact != null)
                return exact.Text.Trim();

            var vi = poi.Translations.FirstOrDefault(t =>
                !string.IsNullOrWhiteSpace(t.Language) &&
                string.Equals(t.Language.Trim(), "vi", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(t.Text));

            if (vi != null)
                return vi.Text.Trim();

            var first = poi.Translations.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Text));
            if (first != null)
                return first.Text.Trim();
        }

        return string.IsNullOrWhiteSpace(poi.Description) ? "" : poi.Description.Trim();
    }
}
