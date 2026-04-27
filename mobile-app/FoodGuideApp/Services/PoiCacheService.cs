using System.Diagnostics;
using System.Text.Json;
using FoodGuideApp.Models;
using Microsoft.Maui.Storage;

namespace FoodGuideApp.Services;

public static class PoiCacheService
{
    private const string CacheFileName = "poi-cache.json";
    private const string CacheUpdatedAtKey = "poi_cache_updated_at";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string CacheFilePath => Path.Combine(FileSystem.AppDataDirectory, CacheFileName);

    // Công dụng: lưu danh sách POI mới nhất vào JSON local để app dùng lại khi mất mạng.
    public static async Task SaveAsync(IReadOnlyCollection<Poi> pois)
    {
        try
        {
            Directory.CreateDirectory(FileSystem.AppDataDirectory);

            var json = JsonSerializer.Serialize(pois ?? Array.Empty<Poi>(), JsonOptions);
            await File.WriteAllTextAsync(CacheFilePath, json);

            Preferences.Set(CacheUpdatedAtKey, DateTimeOffset.UtcNow.ToString("O"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[POI CACHE SAVE ERROR] {ex.Message}");
        }
    }

    // Công dụng: đọc POI đã cache; nếu file lỗi hoặc chưa có thì trả về danh sách rỗng.
    public static async Task<List<Poi>> LoadAsync()
    {
        try
        {
            if (!File.Exists(CacheFilePath))
            {
                return new List<Poi>();
            }

            var json = await File.ReadAllTextAsync(CacheFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<Poi>();
            }

            return JsonSerializer.Deserialize<List<Poi>>(json, JsonOptions) ?? new List<Poi>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[POI CACHE LOAD ERROR] {ex.Message}");
            return new List<Poi>();
        }
    }

    // Công dụng: kiểm tra cache POI đang tồn tại để fallback offline nhẹ.
    public static bool HasCache()
        => File.Exists(CacheFilePath);
}
