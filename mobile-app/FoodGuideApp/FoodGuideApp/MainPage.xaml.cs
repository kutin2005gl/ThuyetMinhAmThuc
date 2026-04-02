using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Microsoft.Maui.Devices.Sensors;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Features;
using System.Text.Json;
using System.Net.Http;
using System.Linq;
namespace FoodGuideApp
{
    public partial class MainPage : ContentPage
    {
        private bool isTracking = false;
        private CancellationTokenSource trackingCts = new CancellationTokenSource();
        private List<PoiItem> pois = new();

        public MainPage()
        {
            InitializeComponent();

            mapControl.Map = new Mapsui.Map();
            mapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
            mapControl.Map = new Mapsui.Map();
            mapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());

            _ = LoadPoisFromApi();
        }

        private async void OnStartTrackingClicked(object sender, EventArgs e)
        {
            if (isTracking) return;

            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Lỗi", "Bạn chưa cấp quyền vị trí", "OK");
                return;
            }

            isTracking = true;
            trackingCts = new CancellationTokenSource();

            await StartTracking();
        }

        private void OnStopTrackingClicked(object sender, EventArgs e)
        {
            if (!isTracking) return;

            isTracking = false;
            trackingCts.Cancel();
        }

        private async Task StartTracking()
        {
            while (!trackingCts.Token.IsCancellationRequested)
            {
                var location = await GetLocation();

                if (location != null)
                {
                    locationLabel.Text =
                        $"Lat: {location.Latitude:F6}\nLng: {location.Longitude:F6}";

                    MoveMapToLocation(location.Latitude, location.Longitude);
                    ShowUserLocation(location.Latitude, location.Longitude);
                }

                try
                {
                    await Task.Delay(5000, trackingCts.Token);
                }
                catch
                {
                    break;
                }
            }
        }

        private async Task<Location?> GetLocation()
        {
            try
            {
                var request = new GeolocationRequest(
                    GeolocationAccuracy.High,
                    TimeSpan.FromSeconds(10));

                return await Geolocation.GetLocationAsync(request);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", ex.Message, "OK");
                return null;
            }
        }

        private void MoveMapToLocation(double latitude, double longitude)
        {
            if (mapControl.Map == null) return;

            var point = SphericalMercator.FromLonLat(longitude, latitude);
            mapControl.Map.Navigator.CenterOnAndZoomTo(point.ToMPoint(), 10);
        }

        private void ShowUserLocation(double latitude, double longitude)
        {
            if (mapControl.Map == null) return;

            var point = SphericalMercator.FromLonLat(longitude, latitude);

            var feature = new PointFeature(point.ToMPoint());

            feature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 1
            });

            var layer = new MemoryLayer
            {
                Name = "user",
                Features = new[] { feature }
            };

            var oldLayer = mapControl.Map.Layers.FirstOrDefault(l => l.Name == "user");
            if (oldLayer != null)
                mapControl.Map.Layers.Remove(oldLayer);

            mapControl.Map.Layers.Add(layer);

        }
        private async Task LoadPoisFromApi()
        {
            try
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback =
                    (message, cert, chain, errors) => true;

                using var client = new HttpClient(handler);

                string apiUrl = "https://10.0.2.2:5001/api/poi";

                var json = await client.GetStringAsync(apiUrl);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var data = JsonSerializer.Deserialize<List<PoiItem>>(json, options);

                if (data != null)
                    pois = data;

                await DisplayAlert("API", $"Đã tải {pois.Count} quầy", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi API", ex.Message, "OK");
            }
        }
    }
}