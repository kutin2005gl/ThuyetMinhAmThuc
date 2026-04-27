using Microsoft.Maui.ApplicationModel;
using FoodGuideApp.Models;
using FoodGuideApp.Services;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;

namespace FoodGuideApp;

public partial class PoiListPage : ContentPage
{
    private List<Poi> pois = new();
    private readonly PoiService poiService = new();

    // Công dụng: khởi tạo giao diện danh sách POI.
    public PoiListPage()
    {
        InitializeComponent();
    }

    // Công dụng: tải lại POI, ngôn ngữ và khoảng cách khi màn danh sách xuất hiện.
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        ApplyLanguageToUI();

        pois = await LoadPois();

        string currentLanguage = Preferences.Get("app_language", "vi");
        var currentLocation = await TryGetLocationForDistanceAsync();

        poiCollectionView.ItemsSource = pois.Select(p =>
        {
            var imageSource = CreateCachedImageSource(p.ImageUrl);

            return new PoiListItemViewModel
            {
                Poi = p,
                DisplayName = p.Name ?? LanguageManager.Get(
                    "Chưa có POI",
                    "No POI",
                    "暂无 POI",
                    "POI 없음",
                    "POIなし",
                    "Aucun POI"),
                DisplayDescription = GetPoiTextByLanguage(p, currentLanguage),
                ImageUrl = p.ImageUrl ?? "",
                ImageSource = imageSource,
                HasImage = imageSource != null,
                HasNoImage = imageSource == null,
                DisplayDistance = GetDisplayDistance(p, currentLocation),
                DisplayLanguage = GetLanguageBadge(currentLanguage)
            };
        }).ToList();
    }

    // Công dụng: áp dụng nội dung tiêu đề theo ngôn ngữ đang chọn.
    private void ApplyLanguageToUI()
    {
        Title = LanguageManager.Get("POI", "POIs", "兴趣点", "POI", "POI", "POI");

        pageTitleLabel.Text = LanguageManager.Get(
            "Danh sách địa điểm",
            "List of locations",
            "地点列表",
            "장소 목록",
            "スポット一覧",
            "Liste des lieux");
    }

    // Công dụng: tải danh sách POI từ API mobile đang dùng.
    private async Task<List<Poi>> LoadPois()
        => await poiService.GetPoisAsync();

    // Công dụng: tạo nguồn ảnh remote có cache ngắn hạn, tránh tải lại ảnh POI khi mở danh sách nhiều lần.
    private static ImageSource? CreateCachedImageSource(string? imageUrl)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return new UriImageSource
        {
            Uri = uri,
            CachingEnabled = true,
            CacheValidity = TimeSpan.FromDays(3)
        };
    }

    // Công dụng: lưu POI được chọn vào Preferences rồi mở trang chi tiết hiện có.
    private async void OnPoiSelected(object sender, SelectionChangedEventArgs e)
    {
        var selectedItem = e.CurrentSelection.FirstOrDefault() as PoiListItemViewModel;
        var selected = selectedItem?.Poi;
        if (selected == null)
            return;

        ((CollectionView)sender).SelectedItem = null;

        string currentLanguage = Preferences.Get("app_language", "vi");
        string description = GetPoiTextByLanguage(selected, currentLanguage);

        Preferences.Set(
            "poi_name",
            selected.Name ?? LanguageManager.Get(
                "Chưa có POI",
                "No POI",
                "暂无 POI",
                "POI 없음",
                "POIなし",
                "Aucun POI"));

        Preferences.Set(
            "poi_description",
            string.IsNullOrWhiteSpace(description)
                ? LanguageManager.Get(
                    "Không có mô tả",
                    "No description",
                    "没有描述",
                    "설명이 없습니다",
                    "説明がありません",
                    "Aucune description")
                : description);

        Preferences.Set("poi_image_url", selected.ImageUrl ?? "");
        Preferences.Set("highlight_poi_id", selected.Id);

        string distanceText = "--";

        try
        {
            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5)));

            if (location != null && IsValidCoordinate(selected.Latitude, selected.Longitude))
            {
                double distanceMeters = Location.CalculateDistance(
                    location,
                    new Location(selected.Latitude, selected.Longitude),
                    DistanceUnits.Kilometers) * 1000;

                distanceText = distanceMeters.ToString("F1");
            }
        }
        catch
        {
            distanceText = "--";
        }

        Preferences.Set("poi_distance", distanceText);

        await Navigation.PushAsync(new PoiInfoPage());
    }

    // Công dụng: lấy nội dung mô tả POI theo ngôn ngữ đang chọn, có fallback về tiếng Việt.
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

    // Công dụng: kiểm tra tọa độ POI có nằm trong giới hạn hợp lệ hay không.
    private bool IsValidCoordinate(double latitude, double longitude)
    {
        return latitude >= -90 && latitude <= 90 &&
               longitude >= -180 && longitude <= 180;
    }

    // Công dụng: lấy vị trí gần nhất của thiết bị để tính khoảng cách hiển thị trong danh sách.
    private async Task<Location?> TryGetLocationForDistanceAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                return null;
            }

            return await Geolocation.Default.GetLastKnownLocationAsync()
                   ?? await Geolocation.Default.GetLocationAsync(
                       new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(3)));
        }
        catch
        {
            return null;
        }
    }

    // Công dụng: định dạng khoảng cách từ vị trí hiện tại đến POI cho card danh sách.
    private string GetDisplayDistance(Poi poi, Location? currentLocation)
    {
        if (currentLocation == null || !IsValidCoordinate(poi.Latitude, poi.Longitude))
        {
            return LanguageManager.Get(
                "Chưa rõ",
                "Unknown",
                "未知",
                "알 수 없음",
                "不明",
                "Inconnue");
        }

        double distanceMeters = Location.CalculateDistance(
            currentLocation,
            new Location(poi.Latitude, poi.Longitude),
            DistanceUnits.Kilometers) * 1000;

        return distanceMeters >= 1000
            ? $"{distanceMeters / 1000:0.0} km"
            : $"{distanceMeters:0} m";
    }

    // Công dụng: rút gọn ngôn ngữ đang chọn thành badge nhỏ cho từng card POI.
    private string GetLanguageBadge(string language)
    {
        return (language ?? "vi").Trim().ToLowerInvariant() switch
        {
            "en" => "EN",
            "zh" => "中文",
            "ko" => "한국어",
            "ja" => "日本語",
            "fr" => "FR",
            _ => "VI"
        };
    }

    public class PoiListItemViewModel
    {
        public Poi Poi { get; set; } = new();
        public string DisplayName { get; set; } = "";
        public string DisplayDescription { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public ImageSource? ImageSource { get; set; }
        public bool HasImage { get; set; }
        public bool HasNoImage { get; set; } = true;
        public string DisplayDistance { get; set; } = "";
        public string DisplayLanguage { get; set; } = "";
    }
}
