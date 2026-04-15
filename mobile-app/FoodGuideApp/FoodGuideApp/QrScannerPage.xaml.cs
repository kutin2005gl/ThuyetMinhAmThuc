using FoodGuideApp.Models;
using Microsoft.Maui.Controls;
using System.Text.Json;

namespace FoodGuideApp;

public partial class QrScannerPage : ContentPage
{
    private readonly HttpClient httpClient = new HttpClient();
    private bool isProcessing = false;

    public QrScannerPage()
    {
        InitializeComponent();
    }

    public static class AppConfig
    {
        public static string BaseUrl = "http://10.0.2.2:5000";
    }

    private async void BarcodeReader_BarcodesDetected(object sender, ZXing.Net.Maui.BarcodeDetectionEventArgs e)
    {
        if (isProcessing) return;

        var result = e.Results?.FirstOrDefault();
        if (result == null || string.IsNullOrWhiteSpace(result.Value))
            return;

        isProcessing = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                string rawValue = result.Value.Trim();

                if (!TryParsePoiId(rawValue, out int poiId))
                {
                    await DisplayAlert("Lỗi", $"QR không hợp lệ: {rawValue}", "OK");
                    isProcessing = false;
                    return;
                }

                string url = $"{AppConfig.BaseUrl}/api/poi/{poiId}";
                var json = await httpClient.GetStringAsync(url);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var poi = JsonSerializer.Deserialize<Poi>(json, options);

                if (poi == null)
                {
                    await DisplayAlert("Lỗi", "Không đọc được dữ liệu POI từ API.", "OK");
                    isProcessing = false;
                    return;
                }

                SavePoiInfoToPreferences(poi);

                await DisplayAlert("Thành công", $"Đã mở nội dung: {poi.Name}", "OK");

                await Shell.Current.GoToAsync("//poi");
            }
            catch (HttpRequestException)
            {
                await DisplayAlert("Lỗi", "Không gọi được API POI. Kiểm tra WebAPI có đang chạy không.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", ex.Message, "OK");
            }
            finally
            {
                isProcessing = false;
            }
        });
    }

    private bool TryParsePoiId(string rawValue, out int poiId)
    {
        poiId = 0;

        if (int.TryParse(rawValue, out poiId))
            return true;

        if (rawValue.StartsWith("POI:", StringComparison.OrdinalIgnoreCase))
        {
            string idPart = rawValue.Substring(4).Trim();
            return int.TryParse(idPart, out poiId);
        }

        return false;
    }

    private void SavePoiInfoToPreferences(Poi poi)
    {
        string description = GetPoiTextByLanguage(poi, Preferences.Get("app_language", "vi"));

        Preferences.Set("poi_name", poi.Name ?? "Chưa có POI");
        Preferences.Set("poi_description", string.IsNullOrWhiteSpace(description) ? "Không có mô tả" : description);
        Preferences.Set("poi_image_url", poi.ImageUrl ?? "");
        Preferences.Set("poi_distance", "0.0");
    }

    private string GetPoiTextByLanguage(Poi poi, string currentLanguage)
    {
        if (poi == null)
            return "";

        string lang = (currentLanguage ?? "vi").Trim();

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

            var first = poi.Translations.FirstOrDefault(t =>
                !string.IsNullOrWhiteSpace(t.Text));

            if (first != null)
                return first.Text.Trim();
        }

        if (!string.IsNullOrWhiteSpace(poi.Description))
            return poi.Description.Trim();

        return "";
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}