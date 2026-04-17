using FoodGuideApp.Models;
using FoodGuideApp.Services;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices.Sensors;
using System.Text.Json;

namespace FoodGuideApp;

public partial class TourDetailPage : ContentPage
{
    private readonly Tour currentTour;
    private List<TourPoiDisplayItem> poiDisplayItems = new();

    public TourDetailPage(Tour tour)
    {
        InitializeComponent();
        currentTour = tour;
        ApplyLocalization();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalization();
        await LoadPoiDisplayItemsAsync();
    }

    private void ApplyLocalization()
    {
        Title = currentTour.DisplayName;
        tourNameLabel.Text = currentTour.DisplayName;
        tourDescriptionLabel.Text = currentTour.DisplayDescription;

        poiSectionTitleLabel.Text = LanguageManager.Get(
            "Danh sách điểm tham quan / ẩm thực",
            "List of sightseeing / food places",
            "景点 / 美食地点列表",
            "관광지 / 음식 장소 목록",
            "観光地 / グルメスポット一覧",
            "Liste des lieux touristiques / gastronomiques"
        );
    }

    private async Task LoadPoiDisplayItemsAsync()
    {
        try
        {
            poiDisplayItems = currentTour.Pois
                .OrderBy(p => p.Order)
                .Select(tp => new TourPoiDisplayItem
                {
                    Order = tp.Order,
                    PoiId = tp.PoiId,
                    DisplayName = tp.Name
                })
                .ToList();

            poiCollectionView.ItemsSource = null;
            poiCollectionView.ItemsSource = poiDisplayItems;
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                LanguageManager.Get("Lỗi", "Error", "错误", "오류", "エラー", "Erreur"),
                ex.Message,
                "OK");
        }
    }

    private async void OnPoiSelected(object sender, SelectionChangedEventArgs e)
    {
        var selectedPoi = e.CurrentSelection.FirstOrDefault() as TourPoiDisplayItem;
        if (selectedPoi == null)
            return;

        ((CollectionView)sender).SelectedItem = null;

        try
        {
            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("http://10.0.2.2:5000/");

            var res = await httpClient.GetAsync("api/poi");
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var pois = JsonSerializer.Deserialize<List<Poi>>(json, options) ?? new List<Poi>();
            var fullPoi = pois.FirstOrDefault(p => p.Id == selectedPoi.PoiId);

            if (fullPoi == null)
            {
                await DisplayAlert(
                    LanguageManager.Get("Lỗi", "Error", "错误", "오류", "エラー", "Erreur"),
                    LanguageManager.Get(
                        "Không tìm thấy thông tin POI.",
                        "POI information not found.",
                        "未找到 POI 信息。",
                        "POI 정보를 찾을 수 없습니다.",
                        "POI 情報が見つかりません。",
                        "Informations du POI introuvables."
                    ),
                    "OK");
                return;
            }

            if (!IsValidCoordinate(fullPoi.Latitude, fullPoi.Longitude))
            {
                await DisplayAlert(
                    LanguageManager.Get("Lỗi", "Error", "错误", "오류", "エラー", "Erreur"),
                    $"{LanguageManager.Get("Tọa độ POI không hợp lệ", "Invalid POI coordinates", "POI 坐标无效", "잘못된 POI 좌표", "無効な POI 座標", "Coordonnées POI invalides")}\n{fullPoi.Name}\nLat: {fullPoi.Latitude}\nLng: {fullPoi.Longitude}",
                    "OK");
                return;
            }

            double distanceMeters = 0;

            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5)));

            if (location != null)
            {
                distanceMeters = Location.CalculateDistance(
                    location,
                    new Location(fullPoi.Latitude, fullPoi.Longitude),
                    DistanceUnits.Kilometers) * 1000;
            }

            Preferences.Set("poi_name", fullPoi.Name);
            Preferences.Set("poi_description", GetPoiDescriptionByLanguage(fullPoi));
            Preferences.Set("poi_image_url", fullPoi.ImageUrl ?? "");
            Preferences.Set("poi_distance", distanceMeters.ToString("F1"));
            Preferences.Set("highlight_poi_id", fullPoi.Id);

            await Navigation.PushAsync(new PoiInfoPage());
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                LanguageManager.Get("Lỗi", "Error", "错误", "오류", "エラー", "Erreur"),
                ex.Message,
                "OK");
        }
    }

    private string GetPoiDescriptionByLanguage(Poi poi)
    {
        string lang = LanguageManager.CurrentLanguage.ToLower();

        var translation = poi.Translations?
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Language) &&
                                 t.Language.Trim().ToLower() == lang);

        return translation?.Text ?? poi.Description;
    }

    private bool IsValidCoordinate(double lat, double lng)
    {
        return lat >= -90 && lat <= 90 &&
               lng >= -180 && lng <= 180;
    }
}