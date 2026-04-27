using Microsoft.Maui.Storage;

namespace FoodGuideApp.Services;

public static class LanguageManager
{
    public static string CurrentLanguage =>
        Preferences.Get("app_language", "vi");

    public static string Get(
        string vi,
        string en,
        string zh,
        string ko,
        string ja,
        string fr)
    {
        return CurrentLanguage switch
        {
            "en" => en,
            "zh" => zh,
            "ko" => ko,
            "ja" => ja,
            "fr" => fr,
            _ => vi
        };
    }
}