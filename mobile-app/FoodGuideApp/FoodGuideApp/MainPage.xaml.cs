using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Microsoft.Maui.Devices.Sensors;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Features;

namespace FoodGuideApp
{
    public partial class MainPage : ContentPage
    {
        private bool isTracking = false;
        private CancellationTokenSource trackingCts = new CancellationTokenSource();

        public MainPage()
        {
            InitializeComponent();

            mapControl.Map = new Mapsui.Map();
            mapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
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
    }
}