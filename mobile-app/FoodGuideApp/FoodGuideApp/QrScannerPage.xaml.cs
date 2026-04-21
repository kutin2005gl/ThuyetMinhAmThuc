using FoodGuideApp.Services;
using Microsoft.Maui.Controls;

namespace FoodGuideApp;

public partial class QrScannerPage : ContentPage
{
    private bool isProcessing = false;

    public QrScannerPage()
    {
        InitializeComponent();
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

                var loaded = await PoiNavigationService.LoadPoiToPreferencesAsync(poiId);
                if (!loaded)
                {
                    await DisplayAlert("Lỗi", "Không đọc được dữ liệu POI từ API.", "OK");
                    isProcessing = false;
                    return;
                }
                await Shell.Current.GoToAsync("//pois");
                await Shell.Current.Navigation.PushAsync(new PoiInfoPage());
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

        if (Uri.TryCreate(rawValue, UriKind.Absolute, out var absoluteUri))
        {
            return TryParsePoiIdFromPath(absoluteUri.AbsolutePath, out poiId);
        }

        return TryParsePoiIdFromPath(rawValue, out poiId);
    }

    private static bool TryParsePoiIdFromPath(string path, out int poiId)
    {
        poiId = 0;
        var cleanedPath = path.Trim();
        var segments = cleanedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 2 && segments[0].Equals("poi", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(segments[1], out poiId);
        }

        return false;
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
