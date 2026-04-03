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
using System.Net.Http.Json;
using NetTopologySuite.Geometries;
using Mapsui.Nts;
using SensorLocation = Microsoft.Maui.Devices.Sensors.Location;
using NtsPoint = NetTopologySuite.Geometries.Point;
using MapsuiColor = Mapsui.Styles.Color;
using MapsuiBrush = Mapsui.Styles.Brush;
using MapsuiPen = Mapsui.Styles.Pen;
namespace FoodGuideApp
{
    public partial class MainPage : ContentPage
    {
        private bool isTracking = false;
        private CancellationTokenSource trackingCts = new CancellationTokenSource();
        private List<PoiItem> pois = new();
        private List<Poi> geoPois = new(); // dùng cho geofence
        private HashSet<int> triggeredPois = new();
        private readonly HttpClient httpClient = new HttpClient();
       
        private MemoryLayer? poiLayer;
        public MainPage()
        {
            InitializeComponent();

            mapControl.Map = new Mapsui.Map();
            mapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
            mapControl.Map = new Mapsui.Map();
            mapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());

            _ = LoadPoisAndShowOnMap();


            _ = InitializeData();   

        }
        private async Task InitializeData()
        {
            await LoadPoisFromApi();
            await LoadPois();
            DrawGeofenceCircles();
        }
        private async Task LoadPoisFromApi()
        {
            try
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback =
                    (message, cert, chain, errors) => true;

                using var client = new HttpClient(handler);

                string apiUrl = "http://10.0.2.2:5000/api/poi";

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
        private async Task LoadPois()
        {
            try
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback =
                    (message, cert, chain, errors) => true;

                using var client = new HttpClient(handler);

                string apiUrl = "http://10.0.2.2:5000/api/poi";

                var json = await client.GetStringAsync(apiUrl);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                geoPois = JsonSerializer.Deserialize<List<Poi>>(json, options) ?? new List<Poi>();

                Console.WriteLine($"Đã tải geofence: {geoPois.Count}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi geofence", ex.Message, "OK");
            }
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

            _ = StartTracking();
        }

        private void OnStopTrackingClicked(object sender, EventArgs e)
        {
            if (!isTracking) return;

            isTracking = false;

            trackingCts.Cancel();
            trackingCts = new CancellationTokenSource(); // 👈 reset

            triggeredPois.Clear(); // 👈 reset geofence

            MainThread.BeginInvokeOnMainThread(() =>
            {
                geofenceLabel.Text = "Đã dừng theo dõi";
            });
        }

        private async Task StartTracking()
        {
            while (!trackingCts.Token.IsCancellationRequested)
            {
                var location = await GetLocation();

                if (location != null)
                {
                    string nearbyName = "";

                    foreach (var poi in geoPois)
                    {
                        var distanceKm = SensorLocation.CalculateDistance(
                            location,
                            new SensorLocation(poi.Latitude, poi.Longitude),
                            DistanceUnits.Kilometers);

                        var distanceMeters = distanceKm * 1000;

                        Console.WriteLine($"Quầy: {poi.Name}");
                        Console.WriteLine($"Khoảng cách: {distanceMeters} m - Radius: {poi.RadiusMeters} m");

                        if (distanceMeters <= poi.RadiusMeters)
                        {
                            nearbyName = poi.Name;

                            if (!triggeredPois.Contains(poi.Id))
                            {
                                triggeredPois.Add(poi.Id);
                                OnEnterPoi(poi);
                            }

                            break;
                        }
                    }

                    locationLabel.Text =
                        $"Lat: {location.Latitude:F6}\nLng: {location.Longitude:F6}" +
                        (string.IsNullOrEmpty(nearbyName) ? "" : $"\nGần quầy: {nearbyName}");

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

        private async Task<SensorLocation?> GetLocation()
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
            mapControl.Map.Navigator.CenterOnAndZoomTo(point.ToMPoint(), 5);
        }

        private void ShowUserLocation(double latitude, double longitude)
        {
            if (mapControl.Map == null) return;

            var point = SphericalMercator.FromLonLat(longitude, latitude);

            var feature = new PointFeature(point.ToMPoint());

            feature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 0.25
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
        private void OnEnterPoi(Poi poi)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                geofenceLabel.Text = $"Đã vào vùng: {poi.Name}";
                await DisplayAlert("Geofence", $"Đã vào vùng của {poi.Name}", "OK");
            });
        }
        private void DrawGeofenceCircles()
        {
            if (mapControl.Map == null || geoPois == null || geoPois.Count == 0)
                return;

            var features = new List<IFeature>();

            foreach (var poi in geoPois)
            {
                var center = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);

                // Tạo hình tròn theo bán kính mét
                var displayRadius = Math.Max(poi.RadiusMeters, 80);
                var circleGeometry = new NtsPoint(center.x, center.y).Buffer(displayRadius);

                var feature = new GeometryFeature
                {
                    Geometry = circleGeometry
                };

               feature.Styles.Add(new VectorStyle
{
                Fill = new MapsuiBrush(new MapsuiColor(255, 0, 0, 80)),
                Outline = new MapsuiPen(new MapsuiColor(255, 0, 0, 225), 3)
            });

                features.Add(feature);
            }

            var geofenceLayer = new MemoryLayer
            {
                Name = "geofences",
                Features = features
            };

            var oldLayer = mapControl.Map.Layers.FirstOrDefault(l => l.Name == "geofences");
            if (oldLayer != null)
                mapControl.Map.Layers.Remove(oldLayer);

            mapControl.Map.Layers.Add(geofenceLayer);
            mapControl.Refresh();
        }
        private async Task LoadPoisAndShowOnMap()
        {
            try
            {
                // Android Emulator dùng 10.0.2.2 để trỏ về máy thật
                string apiUrl = "http://10.0.2.2:5000/api/poi";

                var json = await httpClient.GetStringAsync(apiUrl);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var data = JsonSerializer.Deserialize<List<Poi>>(json, options);

                if (data != null)
                {
                    geoPois = data;
                    ShowPoiMarkers();

                    resultLabel.Text = $"Đã tải {pois.Count} POI";
                }
                else
                {
                    resultLabel.Text = "Không đọc được dữ liệu POI";
                }
            }
            catch (Exception ex)
            {
                resultLabel.Text = $"Lỗi tải POI: {ex.Message}";
            }
            if (pois.Count > 0)
            {
                var firstPoi = pois[0];
                var center = SphericalMercator.FromLonLat(firstPoi.Longitude, firstPoi.Latitude);

                mapControl.Map.Navigator.CenterOn(new MPoint(center.x, center.y));
                mapControl.Map.Navigator.ZoomTo(5000);
            }
        }
        private void ShowPoiMarkers()
        {
            if (mapControl.Map == null || pois.Count == 0)
                return;

            if (poiLayer != null)
            {
                mapControl.Map.Layers.Remove(poiLayer);
            }

            var features = new List<IFeature>();

            foreach (var poi in pois)
            {
                var spherical = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);

                var feature = new PointFeature(new MPoint(spherical.x, spherical.y));

                feature["Name"] = poi.Name;

                feature.Styles.Add(new SymbolStyle
                {
                    SymbolType = SymbolType.Ellipse,
                    Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Red),
                    Outline = new Pen(Mapsui.Styles.Color.White, 2),
                    SymbolScale = 0.8
                });

                features.Add(feature);
            }

            poiLayer = new MemoryLayer
            {
                Name = "POI Markers",
                Features = features
            };

            mapControl.Map.Layers.Add(poiLayer);
            mapControl.Refresh();
        }
    }
}