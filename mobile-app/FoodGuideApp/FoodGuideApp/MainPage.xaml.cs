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
using Mapsui.Nts;
using Microsoft.Maui.Media;
using System.Collections.Generic;
using SensorLocation = Microsoft.Maui.Devices.Sensors.Location;
using NtsPoint = NetTopologySuite.Geometries.Point;
using MapsuiColor = Mapsui.Styles.Color;
using MapsuiBrush = Mapsui.Styles.Brush;
using MapsuiPen = Mapsui.Styles.Pen;
using FoodGuideApp.Models;
using System.Diagnostics;
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
        private Dictionary<int, PoiRuntimeState> poiStates = new();
        private MemoryLayer? poiLayer;
        private DateTime lastPoiCheckTime = DateTime.MinValue;
        private readonly TimeSpan poiDebounce = TimeSpan.FromSeconds(3);
       
        private TimeSpan poiCooldown = TimeSpan.FromSeconds(5);

        private string currentLanguage = "vi";
        private double currentGeofenceRadius = 30.0;
        private HashSet<int> enteredPois = new();
        private bool isSpeaking = false;

        public MainPage()
        {
            InitializeComponent();
            
            mapControl.Map = new Mapsui.Map();
            mapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
           

            _ = LoadPoisAndShowOnMap();
            LoadAppSettings();
            _ = LoadPois();
            _ = InitializeData();

        }
        private async Task InitializeData()
        {
            await LoadPoisFromApi();
            await LoadPois();
            DrawGeofenceCircles();
        }
        private void InitializePoiStates()
        {
            poiStates.Clear();

            foreach (var poi in geoPois)
            {
                if (!poiStates.ContainsKey(poi.Id))
                {
                    poiStates[poi.Id] = new PoiRuntimeState();
                }
            }
        }
        public static class AppConfig
        {
            public static string BaseUrl = "https://192.168.1.5:5000";
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
                var client = new HttpClient();
                string url = $"{AppConfig.BaseUrl}/api/poi";

                var json = await client.GetStringAsync(url);

                Console.WriteLine("JSON API:");
                Console.WriteLine(json);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var data = JsonSerializer.Deserialize<List<Poi>>(json, options);

                if (data == null)
                {
                    Console.WriteLine("Deserialize bị null!");
                    geoPois = new List<Poi>();
                }
                else
                {
                    geoPois = data;

                    // 👉 CHỈ sửa ở đây, không làm mất POI
                    foreach (var poi in geoPois)
                    {
                        poi.RadiusMeters = poi.RadiusMeters == 0 ? 150 : poi.RadiusMeters;
                        poi.NearRadiusMeters = poi.NearRadiusMeters == 0 ? 200 : poi.NearRadiusMeters;
                    }
                }

                InitializePoiStates();

                Console.WriteLine($"Đã tải {geoPois.Count} POI");

                ShowPoisOnMap();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi load POI: " + ex.ToString());
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
        private async Task CheckEnterPoi(SensorLocation location)
        {
            Poi? bestPoi = null;
            double bestDistance = double.MaxValue;

            // Tìm POI tốt nhất đang nằm trong vùng enter
            foreach (var poi in geoPois)
            {
                if (Math.Abs(location.Latitude - poi.Latitude) > 0.01 ||
                    Math.Abs(location.Longitude - poi.Longitude) > 0.01)
                {
                    continue;
                }

                double distanceMeters = SensorLocation.CalculateDistance(
                    location,
                    new SensorLocation(poi.Latitude, poi.Longitude),
                    DistanceUnits.Kilometers) * 1000;

                bool isInside = distanceMeters <= poi.RadiusMeters;

                System.Diagnostics.Debug.WriteLine(
                    $"[ENTER] {poi.Name} | dist={distanceMeters:F1}m | radius={poi.RadiusMeters}m | inside={isInside}");

                if (isInside)
                {
                    if (bestPoi == null ||
                        poi.Priority > bestPoi.Priority ||
                        (poi.Priority == bestPoi.Priority && distanceMeters < bestDistance))
                    {
                        bestPoi = poi;
                        bestDistance = distanceMeters;
                    }
                }
            }

            // Cập nhật geofence label
            if (bestPoi != null)
            {
                geofenceLabel.Text = $"Đã vào vùng geofence: {bestPoi.Name} ({bestDistance:F1}m)";
                geofenceLabel.TextColor = Colors.Green;
            }
            else
            {
                geofenceLabel.Text = "Chưa vào vùng geofence";
                geofenceLabel.TextColor = Colors.Red;
            }

            // Cập nhật trạng thái cho từng POI
            foreach (var poi in geoPois)
            {
                if (!poiStates.ContainsKey(poi.Id))
                    continue;

                var state = poiStates[poi.Id];
                bool isBestPoi = bestPoi != null && poi.Id == bestPoi.Id;

                if (isBestPoi)
                {
                    var now = DateTime.Now;

                    if (!state.WasInside)
                    {
                        if (now - state.LastTriggeredAt >= poiCooldown)
                        {
                            state.WasInside = true;
                            state.LastTriggeredAt = now;

                            string message = GetPoiTextByLanguage(bestPoi, currentLanguage);

                            resultLabel.Text = $"🔊 Đang thuyết minh: {bestPoi.Name} ({bestDistance:F0}m)";

                            await SpeakPoiMessage(message);
                        }
                        else
                        {
                            state.WasInside = true;
                            resultLabel.Text = $"Đã vào vùng geofence: {bestPoi.Name} (đang cooldown)";
                        }
                    }
                    else
                    {
                        resultLabel.Text = $"Đã ở trong vùng: {bestPoi.Name}";
                    }
                }
                else
                {
                    state.WasInside = false;
                }
            }
        }
        private string GetNearbyPoiName(SensorLocation location)
        {
            Poi? nearestPoi = null;
            double nearestDistance = double.MaxValue;

            foreach (var poi in geoPois)
            {
                double distanceMeters = SensorLocation.CalculateDistance(
                    location,
                    new SensorLocation(poi.Latitude, poi.Longitude),
                    DistanceUnits.Kilometers) * 1000;

                if (distanceMeters < nearestDistance)
                {
                    nearestDistance = distanceMeters;
                    nearestPoi = poi;
                }
            }

            return nearestPoi?.Name ?? "";
        }
        private async Task StartTracking()
        {
            while (!trackingCts.Token.IsCancellationRequested)
            {
                try
                {
                    var location = await GetLocation();

                    if (location != null)
                    {
                        // Debug: tìm POI gần nhất để xem log cho dễ test
                        if (geoPois.Count > 0)
                        {
                            var nearestPoi = geoPois
                                .Select(poi => new
                                {
                                    Poi = poi,
                                    Distance = SensorLocation.CalculateDistance(
                                        location,
                                        new SensorLocation(poi.Latitude, poi.Longitude),
                                        DistanceUnits.Kilometers) * 1000
                                })
                                .OrderBy(x => x.Distance)
                                .FirstOrDefault();

                            if (nearestPoi != null)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"[TRACKING] POI gần nhất: {nearestPoi.Poi.Name} - {nearestPoi.Distance:F1}m / {nearestPoi.Poi.RadiusMeters}m");
                            }
                        }

                        // Chỉ check POI theo debounce để đỡ spam
                        var now = DateTime.Now;

                        if (geoPois.Count > 0 && now - lastPoiCheckTime >= poiDebounce)
                        {
                            lastPoiCheckTime = now;

                            CheckNearPoi(location);
                            await CheckEnterPoi(location);
                        }

                        // Hiển thị tên POI gần nhất để debug / xem trạng thái
                        string nearbyName = geoPois.Count > 0 ? GetNearbyPoiName(location) : "";

                        locationLabel.Text =
                            $"Lat: {location.Latitude:F6}\n" +
                            $"Lng: {location.Longitude:F6}" +
                            (string.IsNullOrEmpty(nearbyName) ? "" : $"\nGần quầy: {nearbyName}");

                        // Cập nhật map
                        MoveMapToLocation(location.Latitude, location.Longitude);
                        ShowUserLocation(location.Latitude, location.Longitude);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[TRACKING] Không lấy được vị trí hiện tại");
                    }

                    await Task.Delay(5000, trackingCts.Token);
                }
                catch (TaskCanceledException)
                {
                    // Khi bấm dừng theo dõi
                    System.Diagnostics.Debug.WriteLine("[TRACKING] Đã dừng theo dõi");
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TRACKING ERROR] {ex.Message}");

                    // tránh app bị văng hẳn nếu có lỗi bất ngờ
                    try
                    {
                        await Task.Delay(2000, trackingCts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
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


        private void ShowPoisOnMap()
        {
            if (mapControl.Map == null) return;

            if (poiLayer != null)
            {
                mapControl.Map.Layers.Remove(poiLayer);
            }

            var features = new List<IFeature>();

            foreach (var poi in pois)
            {
                var sphericalMercator = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);

                var feature = new PointFeature(sphericalMercator.x, sphericalMercator.y);

                // thêm tên vào feature
                feature["Label"] = poi.Name;

                // marker (chấm đỏ)
                feature.Styles.Add(new SymbolStyle
                {
                    SymbolType = SymbolType.Ellipse,
                    Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Red),
                    Outline = new Mapsui.Styles.Pen(Mapsui.Styles.Color.White, 2),
                    SymbolScale = 1.0
                });

                // thêm text (tên quầy)
                feature.Styles.Add(new LabelStyle
                {
                    Text = poi.Name,
                    Offset = new Offset(0, 20)
                });
                features.Add(feature);
            }

            poiLayer = new MemoryLayer
            {
                Name = "POIs",
                Features = features
            };

            mapControl.Map.Layers.Add(poiLayer);
            mapControl.Refresh();
        }
        private void CheckNearPoi(SensorLocation location)
        {
            Poi? bestPoi = null;
            double bestDistance = double.MaxValue;

            foreach (var poi in geoPois)
            {
                if (Math.Abs(location.Latitude - poi.Latitude) > 0.01 ||
                    Math.Abs(location.Longitude - poi.Longitude) > 0.01)
                {
                    continue;
                }

                double distanceMeters = SensorLocation.CalculateDistance(
                    location,
                    new SensorLocation(poi.Latitude, poi.Longitude),
                    DistanceUnits.Kilometers) * 1000;

                bool isNear = distanceMeters <= poi.NearRadiusMeters;
                bool isInside = distanceMeters <= poi.RadiusMeters;

                System.Diagnostics.Debug.WriteLine($"[NEAR] {poi.Name} - {distanceMeters:F1}m");

                if (isNear && !isInside)
                {
                    if (bestPoi == null ||
                        poi.Priority > bestPoi.Priority ||
                        (poi.Priority == bestPoi.Priority && distanceMeters < bestDistance))
                    {
                        bestPoi = poi;
                        bestDistance = distanceMeters;
                    }
                }
            }

            if (bestPoi != null)
            {
                System.Diagnostics.Debug.WriteLine($"[BEST NEAR] {bestPoi.Name} - {bestDistance:F1}m");
            }

            foreach (var poi in geoPois)
            {
                if (!poiStates.ContainsKey(poi.Id))
                    continue;

                var state = poiStates[poi.Id];

                if (bestPoi != null && poi.Id == bestPoi.Id)
                {
                    if (!state.WasNear)
                    {
                        state.WasNear = true;
                        resultLabel.Text = $"Bạn đang đến gần: {poi.Name} ({bestDistance:F1}m)";
                    }
                }
                else
                {
                    state.WasNear = false;
                }
            }
        }
        private string GetPoiSpeechContent(Poi poi)
        {
            if (!string.IsNullOrWhiteSpace(poi.Description))
            {
                return poi.Description;
            }

            return $"Bạn đã vào vùng: {poi.Name}";
        }
        //TTS
        private async Task SpeakPoiMessage(string message)
        {
            if (isSpeaking || string.IsNullOrWhiteSpace(message))
                return;

            try
            {
                isSpeaking = true;
                await TextToSpeech.Default.SpeakAsync(message);
            }
            finally
            {
                isSpeaking = false;
            }
        }
        private async Task SpeakText(string text, string languageCode)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (isSpeaking) return;

            try
            {
                isSpeaking = true;

                var locales = await TextToSpeech.GetLocalesAsync();
                var locale = locales.FirstOrDefault(l => l.Language.StartsWith(languageCode));

                var options = new SpeechOptions();

                if (locale != null)
                    options.Locale = locale;

                await TextToSpeech.SpeakAsync(text, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TTS lỗi: {ex.Message}");
            }
            finally
            {
                isSpeaking = false;
            }
        }
        // ham lay noi dung ngon ngu
        private string GetPoiTextByLanguage(Poi poi, string language)
        {
            if (poi?.Translations == null || !poi.Translations.Any())
                return poi?.Description ?? "";

            var translation = poi.Translations
                .FirstOrDefault(t => t.Language.Equals(language, StringComparison.OrdinalIgnoreCase));

            if (translation != null && !string.IsNullOrWhiteSpace(translation.Text))
                return translation.Text;

            var fallback = poi.Translations
                .FirstOrDefault(t => t.Language.Equals("vi", StringComparison.OrdinalIgnoreCase));

            if (fallback != null && !string.IsNullOrWhiteSpace(fallback.Text))
                return fallback.Text;

            return poi.Description ?? "";
        }
        private void LoadAppSettings()
        {
            currentLanguage = Preferences.Get("app_language", "vi");
            currentGeofenceRadius = Preferences.Get("geofence_radius", 30.0);
        }

        private string GetLanguageMessage()
        {
            return currentLanguage switch
            {
                "vi" => "Đã chọn tiếng Việt",
                "en" => "English selected",
                "zh" => "已选择中文",
                "ja" => "日本語を選択しました",
                _ => "Đã chọn tiếng Việt"
            };
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadAppSettings();
            resultLabel.Text = GetLanguageMessage();
        }

    }
}