using System.Text.Json.Serialization;
using FoodGuideApp.Services;

namespace FoodGuideApp.Models;

public class Tour
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public List<TourPoiItem> Pois { get; set; } = new();
    public List<TourTranslation> Translations { get; set; } = new();

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            string lang = LanguageManager.CurrentLanguage.ToLower();

            var t = Translations?.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.Language) &&
                x.Language.Trim().ToLower() == lang);

            return !string.IsNullOrWhiteSpace(t?.Name) ? t.Name : Name;
        }
    }

    [JsonIgnore]
    public string DisplayDescription
    {
        get
        {
            string lang = LanguageManager.CurrentLanguage.ToLower();

            var t = Translations?.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.Language) &&
                x.Language.Trim().ToLower() == lang);

            return !string.IsNullOrWhiteSpace(t?.Description) ? t.Description : Description;
        }
    }

    [JsonIgnore]
    public string DisplayPoiCount
    {
        get
        {
            int count = Pois?.Count ?? 0;

            return LanguageManager.Get(
                $"Số điểm: {count}",
                $"Number of places: {count}",
                $"景点数量: {count}",
                $"장소 수: {count}",
                $"スポット数: {count}",
                $"Nombre de lieux : {count}"
            );
        }
    }
}