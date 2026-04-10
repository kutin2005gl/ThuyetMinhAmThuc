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
using Mapsui.Nts;
using Microsoft.Maui.Media;
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

        // Danh sách POI chính dùng cho marker + geofence + TTS
        private List<Poi> geoPois = new();

        // Lưu trạng thái runtime của từng POI (đã vào, đã gần, cooldown...)
        private Dictionary<int, PoiRuntimeState> poiStates = new();

        // HttpClient dùng để gọi API
        private readonly HttpClient httpClient = new HttpClient();

        // Layer marker POI trên bản đồ
        private MemoryLayer? poiLayer;

        // Thời điểm check POI gần nhất để tránh spam
        private DateTime lastPoiCheckTime = DateTime.MinValue;

        // Debounce kiểm tra POI
        private readonly TimeSpan poiDebounce = TimeSpan.FromSeconds(1);

        // Cooldown tránh TTS đọc lặp liên tục
        private readonly TimeSpan poiCooldown = TimeSpan.FromSeconds(5);

        // Ngôn ngữ hiện tại đang chọn
        private string currentLanguage = "vi";

        // Bán kính geofence mặc định nếu POI từ API không có radius
        private double currentGeofenceRadius = 30.0;

        // Tránh chồng nhiều lệnh TTS cùng lúc
        private bool isSpeaking = false;
        private HashSet<int> spokenPois = new();
        private DateTime lastMapUpdateTime = DateTime.MinValue;

        public MainPage()
        {
            InitializeComponent();

            mapControl.Map = new Mapsui.Map();
            mapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer());

            LoadAppSettings();
            _ = InitializeData();
        }

        // Công dụng: chứa base URL API để app gọi dữ liệu POI
        public static class AppConfig
        {
            public static string BaseUrl = "http://10.0.2.2:5000";
        }

        // Công dụng: khởi tạo dữ liệu ban đầu của trang
        private async Task InitializeData()
        {
            await LoadPois();
            InitializePoiStates();
            ShowPoisOnMap();
            DrawGeofenceCircles();
        }

        // Công dụng: tạo state runtime cho từng POI để quản lý near/inside/cooldown
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

        // Công dụng: tải danh sách POI từ API
        // Công dụng: tải danh sách POI từ API và loại bỏ POI có tọa độ sai
        private async Task LoadPois()
        {
            try
            {
                string url = $"{AppConfig.BaseUrl}/api/poi";
                var json = await httpClient.GetStringAsync(url);

                Debug.WriteLine("JSON API:");
                Debug.WriteLine(json);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var data = JsonSerializer.Deserialize<List<Poi>>(json, options);

                if (data == null)
                {
                    Debug.WriteLine("Deserialize bị null!");
                    geoPois = new List<Poi>();
                }
                else
                {
                    geoPois = data
                        .Where(p =>
                        {
                            bool valid = IsValidCoordinate(p.Latitude, p.Longitude);

                            if (!valid)
                            {
                                Debug.WriteLine($"[POI INVALID] {p.Name} | Lat={p.Latitude} | Lng={p.Longitude}");
                            }

                            return valid;
                        })
                        .ToList();

                    foreach (var poi in geoPois)
                    {
                        if (poi.RadiusMeters == 0)
                            poi.RadiusMeters = currentGeofenceRadius;

                        if (poi.NearRadiusMeters == 0)
                            poi.NearRadiusMeters = poi.RadiusMeters + 50;
                    }
                }

                Debug.WriteLine($"Đã tải {geoPois.Count} POI hợp lệ");
                resultLabel.Text = $"Đã tải {geoPois.Count} POI";
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Lỗi load POI: " + ex);
                resultLabel.Text = $"Lỗi load POI: {ex.Message}";
            }
        }

        // Công dụng: xử lý khi bấm nút bắt đầu theo dõi vị trí
        private async void OnStartTrackingClicked(object sender, EventArgs e)
        {
            if (isTracking) return;

            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Lỗi", "Bạn chưa cấp quyền vị trí", "OK");
                return;
            }

            resultLabel.Text = "Đang bắt đầu theo dõi...";
            locationLabel.Text = "Đang lấy vị trí...";
            geofenceLabel.Text = "Đang kiểm tra geofence...";
            geofenceLabel.TextColor = Colors.Orange;

            // Reset trạng thái tracking
            isTracking = true;
            isSpeaking = false;
            lastMapUpdateTime = DateTime.MinValue;
            lastPoiCheckTime = DateTime.MinValue;

            trackingCts?.Cancel();
            trackingCts = new CancellationTokenSource();

            _ = Task.Run(StartTracking);
        }
        // Công dụng: xử lý khi bấm nút dừng theo dõi vị trí
        private void OnStopTrackingClicked(object sender, EventArgs e)
        {
            if (!isTracking) return;

            isTracking = false;
            trackingCts.Cancel();
            trackingCts = new CancellationTokenSource();

            foreach (var state in poiStates.Values)
            {
                state.WasInside = false;
                state.WasNear = false;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                geofenceLabel.Text = "Đã dừng theo dõi";
                geofenceLabel.TextColor = Colors.Red;
                resultLabel.Text = "Đã dừng theo dõi";
            });
        }

        // Công dụng: vòng lặp theo dõi vị trí liên tục, cập nhật map, near POI và enter geofence
        private async Task StartTracking()
        {
            int nullLocationCount = 0;

            while (!trackingCts.Token.IsCancellationRequested)
            {
                try
                {
                    var location = await GetLocation();

                    if (location != null)
                    {
                        nullLocationCount = 0;

                        Debug.WriteLine($"[TRACKING] Current: {location.Latitude}, {location.Longitude}");

                        string nearbyName = geoPois.Count > 0 ? GetNearbyPoiName(location) : "";

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            locationLabel.Text =
                                $"Lat: {location.Latitude:F6}\n" +
                                $"Lng: {location.Longitude:F6}" +
                                (string.IsNullOrEmpty(nearbyName) ? "" : $"\nGần quầy: {nearbyName}");

                            resultLabel.Text = "Đang theo dõi vị trí...";
                        });

                        if (geoPois.Count > 0)
                        {
                            var nearestPoi = geoPois
                                .Where(poi => IsValidCoordinate(poi.Latitude, poi.Longitude))
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
                                Debug.WriteLine($"[TRACKING] POI gần nhất: {nearestPoi.Poi.Name} - {nearestPoi.Distance:F1}m / {nearestPoi.Poi.RadiusMeters}m");
                            }
                        }

                        var now = DateTime.Now;
                        if (geoPois.Count > 0 && now - lastPoiCheckTime >= poiDebounce)
                        {
                            lastPoiCheckTime = now;

                            CheckNearPoi(location);
                            await CheckEnterPoi(location);
                        }

                        if ((DateTime.Now - lastMapUpdateTime).TotalSeconds >= 2)
                        {
                            lastMapUpdateTime = DateTime.Now;

                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                MoveMapToLocation(location.Latitude, location.Longitude);
                                ShowUserLocation(location.Latitude, location.Longitude);
                            });
                        }
                    }
                    else
                    {
                        nullLocationCount++;
                        Debug.WriteLine($"[TRACKING] location null lần {nullLocationCount}");

                        if (nullLocationCount >= 3)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                resultLabel.Text = "Tín hiệu GPS chưa ổn định...";
                            });
                        }
                    }

                    await Task.Delay(1000, trackingCts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TRACKING ERROR] {ex}");

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        resultLabel.Text = $"Lỗi tracking: {ex.Message}";
                    });

                    break;
                }
            }
        }
        // Công dụng: lấy vị trí hiện tại của thiết bị
        private async Task<SensorLocation?> GetLocation()
        {
            try
            {
                var request = new GeolocationRequest(
                    GeolocationAccuracy.Best,
                    TimeSpan.FromSeconds(2));

                var location = await Geolocation.Default.GetLocationAsync(request);

                // 🔥 fallback nếu lần đầu null (emulator hay bị)
                if (location == null)
                {
                    Debug.WriteLine("[LOCATION] null lần 1, thử lại...");

                    await Task.Delay(300);

                    location = await Geolocation.Default.GetLocationAsync(request);
                }

                if (location == null)
                {
                    Debug.WriteLine("[LOCATION] null lần 2");
                    return null;
                }

                Debug.WriteLine($"[LOCATION] Current: {location.Latitude}, {location.Longitude}");

                return new SensorLocation(location.Latitude, location.Longitude);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOCATION ERROR] {ex.Message}");
                return null;
            }
        }

        // Công dụng: kiểm tra khi người dùng đi vào geofence của POI nào
        private async Task CheckEnterPoi(SensorLocation location)
        {
            Poi? bestPoi = null;
            double bestDistance = double.MaxValue;

            foreach (var poi in geoPois)
            {
                if (!IsValidCoordinate(poi.Latitude, poi.Longitude))
                {
                    Debug.WriteLine($"[ENTER SKIP INVALID] {poi.Name} | Lat={poi.Latitude} | Lng={poi.Longitude}");
                    continue;
                }

                // Lọc nhanh các POI quá xa để đỡ tốn tính toán
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

                Debug.WriteLine($"[ENTER] {poi.Name} | dist={distanceMeters:F1}m | radius={poi.RadiusMeters}m | inside={isInside}");

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
                        state.WasInside = true;
                        state.LastTriggeredAt = now;

                        if (!spokenPois.Contains(poi.Id))
                        {
                            string message = GetPoiTextByLanguage(poi, currentLanguage);

                            if (string.IsNullOrWhiteSpace(message))
                            {
                                resultLabel.Text = $"⚠ Không có nội dung thuyết minh cho: {poi.Name}";
                                Debug.WriteLine($"[TTS SKIP] {poi.Name} không có text cho ngôn ngữ {currentLanguage}");
                                continue;
                            }

                            resultLabel.Text = $"🔊 Đang thuyết minh: {poi.Name} ({bestDistance:F0}m)";

                            try
                            {
                                await SpeakText(message, currentLanguage);
                                spokenPois.Add(poi.Id);
                                Debug.WriteLine($"[TTS OK] {poi.Name} | lang={currentLanguage}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[TTS ERROR] {poi.Name}: {ex.Message}");
                                resultLabel.Text = $"❌ Lỗi thuyết minh: {poi.Name}";
                            }
                        }
                    }
                }
                else
                {
                    state.WasInside = false;
                }
            }
        }
        // Công dụng: kiểm tra khi người dùng đang đến gần POI nhưng chưa vào hẳn geofence
        private void CheckNearPoi(SensorLocation location)
        {
            Poi? bestPoi = null;
            double bestDistance = double.MaxValue;

            foreach (var poi in geoPois)
            {
                if (!IsValidCoordinate(poi.Latitude, poi.Longitude))
                {
                    Debug.WriteLine($"[NEAR SKIP INVALID] {poi.Name} | Lat={poi.Latitude} | Lng={poi.Longitude}");
                    continue;
                }

                // Bỏ qua POI quá xa để đỡ tốn tính toán
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

                Debug.WriteLine($"[NEAR] {poi.Name} - {distanceMeters:F1}m | near={isNear} | inside={isInside}");

                // Chỉ tính là "đến gần" khi ở gần nhưng chưa vào hẳn geofence
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
                Debug.WriteLine($"[BEST NEAR] {bestPoi.Name} - {bestDistance:F1}m");
            }

            bool hasBestNearPoi = false;

            foreach (var poi in geoPois)
            {
                if (!poiStates.ContainsKey(poi.Id))
                    continue;

                var state = poiStates[poi.Id];
                bool isBestPoi = bestPoi != null && poi.Id == bestPoi.Id;

                if (isBestPoi)
                {
                    hasBestNearPoi = true;

                    if (!state.WasNear)
                    {
                        state.WasNear = true;

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            resultLabel.Text = $"Bạn đang đến gần: {poi.Name} ({bestDistance:F1}m)";
                        });
                    }
                }
                else
                {
                    state.WasNear = false;
                }
            }

            // Nếu không gần POI nào thì không giữ text cũ mãi
            if (!hasBestNearPoi)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!isSpeaking)
                    {
                        resultLabel.Text = "Đang theo dõi vị trí...";
                    }
                });
            }
        }

        // Công dụng: lấy tên POI gần nhất để hiển thị debug/trạng thái
        private string GetNearbyPoiName(SensorLocation location)
        {
            Poi? nearestPoi = null;
            double nearestDistance = double.MaxValue;

            foreach (var poi in geoPois)
            {
                // ❌ bỏ POI tọa độ sai (như latitude = 160)
                if (!IsValidCoordinate(poi.Latitude, poi.Longitude))
                    continue;

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

            // 🔥 QUAN TRỌNG: chỉ hiển thị khi ở gần (ví dụ 80m)
            if (nearestPoi != null && nearestDistance <= nearestPoi.NearRadiusMeters)
            {
                return nearestPoi.Name;
            }

            return "";
        }

        // Công dụng: di chuyển tâm bản đồ theo vị trí hiện tại của người dùng
        // Công dụng: đưa map tới vị trí hiện tại và zoom gần kiểu street-level
        private void MoveMapToLocation(double latitude, double longitude)
        {
            if (mapControl.Map == null) return;

            var point = SphericalMercator.FromLonLat(longitude, latitude);

            mapControl.Map.Navigator.CenterOn(point.ToMPoint());
            mapControl.Map.Navigator.ZoomTo(6);   // số càng nhỏ thì càng zoom gần
            mapControl.Refresh();
        }

        // Công dụng: hiển thị marker vị trí hiện tại của người dùng trên bản đồ
        private void ShowUserLocation(double latitude, double longitude)
        {
            if (mapControl.Map == null) return;

            var point = SphericalMercator.FromLonLat(longitude, latitude);

            var feature = new PointFeature(point.ToMPoint());

            feature.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                Fill = new MapsuiBrush(new MapsuiColor(0, 120, 255)),
                Outline = new MapsuiPen(new MapsuiColor(255, 255, 255), 2),
                SymbolScale = 0.8
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
            mapControl.Refresh();
        }

        // Công dụng: hiển thị marker và tên các POI trên bản đồ
        private void ShowPoisOnMap()
        {
            if (mapControl.Map == null || geoPois.Count == 0)
                return;

            if (poiLayer != null)
                mapControl.Map.Layers.Remove(poiLayer);

            var features = new List<IFeature>();

            foreach (var poi in geoPois)
            {
                var sphericalMercator = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);
                var feature = new PointFeature(new MPoint(sphericalMercator.x, sphericalMercator.y));

                feature["Label"] = poi.Name;

                feature.Styles.Add(new SymbolStyle
                {
                    SymbolType = SymbolType.Ellipse,
                    Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Red),
                    Outline = new Mapsui.Styles.Pen(Mapsui.Styles.Color.White, 2),
                    SymbolScale = 1.0
                });

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

            var firstPoi = geoPois.FirstOrDefault();
            if (firstPoi != null)
            {
                var center = SphericalMercator.FromLonLat(firstPoi.Longitude, firstPoi.Latitude);
                mapControl.Map.Navigator.CenterOn(new MPoint(center.x, center.y));
                mapControl.Map.Navigator.ZoomTo(5000);
            }
        }

        // Công dụng: vẽ vòng tròn geofence của từng POI trên bản đồ
        private void DrawGeofenceCircles()
        {
            if (mapControl.Map == null || geoPois == null || geoPois.Count == 0)
                return;

            var features = new List<IFeature>();

            foreach (var poi in geoPois)
            {
                var center = SphericalMercator.FromLonLat(poi.Longitude, poi.Latitude);

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

        // Công dụng: đọc nội dung thuyết minh bằng Text To Speech theo đúng ngôn ngữ
        private async Task SpeakText(string text, string languageCode)
        {
            if (string.IsNullOrWhiteSpace(text) || isSpeaking)
                return;

            try
            {
                isSpeaking = true;

                var locales = await TextToSpeech.Default.GetLocalesAsync();

                if (locales == null || !locales.Any())
                {
                    resultLabel.Text = "Thiết bị chưa có bộ máy TTS";
                    Debug.WriteLine("[TTS ERROR] Không tìm thấy locale TTS nào");
                    return;
                }

                string lang = (languageCode ?? "vi").Trim().ToLower();
                Locale? locale = null;

                locale = locales.FirstOrDefault(l =>
                    !string.IsNullOrWhiteSpace(l.Language) &&
                    l.Language.StartsWith(lang, StringComparison.OrdinalIgnoreCase));

                if (locale == null && lang.Contains("-"))
                {
                    string shortLang = lang.Split('-')[0];
                    locale = locales.FirstOrDefault(l =>
                        !string.IsNullOrWhiteSpace(l.Language) &&
                        l.Language.StartsWith(shortLang, StringComparison.OrdinalIgnoreCase));
                }

                if (locale == null)
                {
                    locale = locales.FirstOrDefault(l =>
                        !string.IsNullOrWhiteSpace(l.Language) &&
                        l.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase));
                }

                var options = new SpeechOptions
                {
                    Locale = locale,
                    Pitch = 1.0f,
                    Volume = 1.0f
                };

                Debug.WriteLine($"[TTS] Lang={languageCode} | Locale={locale?.Language ?? "default"} | Text={text}");

                await TextToSpeech.Default.SpeakAsync(text, options);

                Debug.WriteLine("[TTS OK] Đọc xong");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TTS ERROR] {ex}");
                resultLabel.Text = "Thiết bị/emulator chưa hỗ trợ Text-to-Speech";
            }
            finally
            {
                isSpeaking = false;
            }
        }

        // Công dụng: lấy nội dung thuyết minh theo ngôn ngữ đang chọn, có fallback về tiếng Việt
        private string GetPoiTextByLanguage(Poi poi, string currentLanguage)
        {
            if (poi == null)
                return "";

            if (poi.Translations != null && poi.Translations.Count > 0)
            {
                string lang = (currentLanguage ?? "vi").Trim().ToLower();

                // 1. Ưu tiên đúng ngôn ngữ
                var exact = poi.Translations.FirstOrDefault(t =>
                    !string.IsNullOrWhiteSpace(t.Language) &&
                    t.Language.Trim().ToLower() == lang &&
                    !string.IsNullOrWhiteSpace(t.Text));

                if (exact != null)
                    return exact.Text.Trim();

                // 2. Nếu không có → fallback tiếng Việt
                var vi = poi.Translations.FirstOrDefault(t =>
                    !string.IsNullOrWhiteSpace(t.Language) &&
                    t.Language.Trim().ToLower() == "vi" &&
                    !string.IsNullOrWhiteSpace(t.Text));

                if (vi != null)
                    return vi.Text.Trim();

                // 3. Nếu vẫn không có → lấy đại cái đầu tiên có text
                var first = poi.Translations.FirstOrDefault(t =>
                    !string.IsNullOrWhiteSpace(t.Text));

                if (first != null)
                    return first.Text.Trim();
            }

            // 4. Nếu không có translations → dùng description
            if (!string.IsNullOrWhiteSpace(poi.Description))
                return poi.Description.Trim();

            return "";
        }

        // Công dụng: tải cài đặt đã lưu của app như ngôn ngữ và bán kính geofence
        private void LoadAppSettings()
        {
            currentLanguage = Preferences.Get("app_language", "vi");
            currentGeofenceRadius = Preferences.Get("geofence_radius", 30.0);
        }

        // Công dụng: trả về message hiển thị tương ứng với ngôn ngữ hiện tại
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

        // Công dụng: đổi ngôn ngữ hiện tại và lưu lại vào Preferences
        private void SetLanguage(string lang)
        {
            currentLanguage = lang;
            Preferences.Set("app_language", lang);
            resultLabel.Text = GetLanguageMessage();
        }

        // Công dụng: xử lý khi trang hiện ra, nạp lại cài đặt và hiển thị ngôn ngữ hiện tại
        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadAppSettings();
            resultLabel.Text = GetLanguageMessage();
        }
        // Công dụng: kiểm tra latitude/longitude có nằm trong khoảng hợp lệ không
        private bool IsValidCoordinate(double latitude, double longitude)
        {
            return latitude >= -90 && latitude <= 90 &&
                   longitude >= -180 && longitude <= 180;
        }
    }
}