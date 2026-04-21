using FoodGuideApp.Models;
using FoodGuideApp.Services;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Media;
using System.Diagnostics;
using System.Text.Json;
using SensorLocation = Microsoft.Maui.Devices.Sensors.Location;

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

        private bool isMapReady = false;
        private Poi? nearestPoiCurrent = null;

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
        //private bool isSpeaking = false;
        private HashSet<int> spokenPois = new();
        private DateTime lastMapUpdateTime = DateTime.MinValue;
        private readonly AudioQueueManager audioManager;
        private int? nearestPoiId = null;
        private bool isManualViewingPoi = false;
        private const string LeafletHtmlContent = """
<!DOCTYPE html>
<html>
<head>
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
  <style>
    html, body, #map { height: 100%; margin: 0; padding: 0; }
  </style>
</head>
<body>
  <div id="map"></div>
  <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
  <script>
    let map;
    let userMarker = null;
    let poiLayer = L.layerGroup();
    let geofenceLayer = L.layerGroup();

    function initMap() {
      if (map) return;
      map = L.map('map').setView([16.0471, 108.2068], 15);
      L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; OpenStreetMap contributors'
      }).addTo(map);
      poiLayer.addTo(map);
      geofenceLayer.addTo(map);
    }

    function setView(lat, lng, zoom) {
      if (!map) return;
      map.setView([lat, lng], zoom ?? 17);
    }

    function setUserLocation(lat, lng) {
      if (!map) return;
      const icon = L.circleMarker([lat, lng], {
        radius: 8,
        color: '#ffffff',
        weight: 2,
        fillColor: '#0078ff',
        fillOpacity: 1
      });

      if (userMarker) {
        userMarker.setLatLng([lat, lng]);
      } else {
        userMarker = icon.addTo(map);
      }
    }

    function renderPois(poisJson) {
      if (!map) return;
      poiLayer.clearLayers();
      const pois = JSON.parse(poisJson);

      pois.forEach(p => {
        const marker = L.circleMarker([p.lat, p.lng], {
          radius: p.isNearest ? 10 : 8,
          color: p.isNearest ? '#000000' : '#ffffff',
          weight: p.isNearest ? 3 : 2,
          fillColor: p.isNearest ? '#ffd700' : '#ff0000',
          fillOpacity: 1
        });
        marker.bindPopup(p.name);
        marker.addTo(poiLayer);
      });
    }

    function renderGeofences(poisJson) {
      if (!map) return;
      geofenceLayer.clearLayers();
      const pois = JSON.parse(poisJson);
      pois.forEach(p => {
        L.circle([p.lat, p.lng], {
          radius: Math.max(p.radius, 80),
          color: 'rgba(255,0,0,0.9)',
          fillColor: 'rgba(255,0,0,0.3)',
          fillOpacity: 0.3,
          weight: 2
        }).addTo(geofenceLayer);
      });
    }
  </script>
</body>
</html>
""";
        public MainPage(IAudioFocusService audioFocusService)
        {
            InitializeComponent();

            // Công dụng: khởi tạo bộ quản lý audio
            // và nhận service xử lý audio focus từ hệ thống.
            audioManager = new AudioQueueManager(audioFocusService);
            mapWebView.Source = new HtmlWebViewSource
            {
                Html = LeafletHtmlContent
            };

            audioManager.Start();

            LoadAppSettings();
            GuestSessionService.AttachTo(httpClient);

            _ = InitializeData();
        }

        // Công dụng: khởi tạo dữ liệu ban đầu của trang
        private async Task InitializeData()
        {
            await LoadPois();
            InitializePoiStates();
            await ShowPoisOnMap();
            await DrawGeofenceCircles();
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
                        // fallback radius
                        if (poi.RadiusMeters <= 0)
                            poi.RadiusMeters = currentGeofenceRadius;

                        // 👇 tự tính thêm (backend không có)
                        poi.NearRadiusMeters = poi.RadiusMeters + 50;

                        Debug.WriteLine($"[POI OK] {poi.Name} | radius={poi.RadiusMeters} | near={poi.NearRadiusMeters}");
                    }
                }

                Debug.WriteLine($"Đã tải {geoPois.Count} POI hợp lệ");
                resultLabel.Text = LanguageManager.Get(
                    $"Đã tải {geoPois.Count} POI",
                    $"Loaded {geoPois.Count} POIs",
                    $"已加载 {geoPois.Count} 个 POI",
                    $"{geoPois.Count}개의 POI를 불러왔습니다",
                    $"{geoPois.Count} 件のPOIを読み込みました",
                    $"{geoPois.Count} POI chargés");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Lỗi load POI: " + ex);
                resultLabel.Text = LanguageManager.Get(
                     $"Lỗi tải POI: {ex.Message}",
                     $"Error loading POIs: {ex.Message}",
                     $"加载 POI 出错：{ex.Message}",
                     $"POI 로딩 오류: {ex.Message}",
                     $"POI 読み込みエラー: {ex.Message}",
                     $"Erreur de chargement des POI : {ex.Message}");
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

            resultLabel.Text = LanguageManager.Get(
                "Đang bắt đầu theo dõi...",
                "Starting tracking...",
                "开始追踪...",
                "추적 시작 중...",
                "追跡開始中...",
                "Démarrage du suivi...");

            locationLabel.Text = LanguageManager.Get(
                "Đang lấy vị trí...",
                "Getting location...",
                "正在获取位置...",
                "위치 가져오는 중...",
                "位置を取得中...",
                "Obtention de la position...");

            geofenceLabel.Text = LanguageManager.Get(
                "Đang kiểm tra geofence...",
                "Checking geofence...",
                "正在检查地理围栏...",
                "지오펜스 확인 중...",
                "ジオフェンス確認中...",
                "Vérification de la zone...");
            geofenceLabel.TextColor = Colors.Orange;

            // Reset trạng thái tracking
            isTracking = true;
            
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
            audioManager.StopAll();
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
                geofenceLabel.Text = LanguageManager.Get(
                    "Đã dừng theo dõi",
                    "Tracking stopped",
                    "已停止追踪",
                    "추적 중지됨",
                    "追跡停止",
                    "Suivi arrêté");

                resultLabel.Text = LanguageManager.Get(
                    "Đã dừng theo dõi",
                    "Tracking stopped",
                    "已停止追踪",
                    "추적 중지됨",
                    "追跡停止",
                    "Suivi arrêté");
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
                        Debug.WriteLine($"[UI CHECK] Lat={location.Latitude:F6}, Lng={location.Longitude:F6}");
                        string nearbyName = geoPois.Count > 0 ? GetNearbyPoiName(location) : "";

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            locationLabel.Text =
                             $"Lat: {location.Latitude:F6}\n" +
                             $"Lng: {location.Longitude:F6}" +
                             (string.IsNullOrEmpty(nearbyName)
                                 ? ""
                                 : LanguageManager.Get(
                                     $"\nGần quầy: {nearbyName}",
                                     $"\nNearby stall: {nearbyName}",
                                     $"\n附近摊位：{nearbyName}",
                                     $"\n근처 매장: {nearbyName}",
                                     $"\n近くの売店: {nearbyName}",
                                     $"\nStand proche : {nearbyName}"));

                            resultLabel.Text = LanguageManager.Get(
                                "Đang theo dõi vị trí...",
                                "Tracking location...",
                                "正在追踪位置...",
                                "위치 추적 중...",
                                "位置を追跡中...",
                                "Suivi de la position...");
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
                                nearestPoiId = nearestPoi.Poi.Id;
                                nearestPoiCurrent = nearestPoi.Poi;

                                Debug.WriteLine($"[TRACKING] POI gần nhất: {nearestPoi.Poi.Name} - {nearestPoi.Distance:F1}m / {nearestPoi.Poi.RadiusMeters}m");

                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    _ = HighlightNearestPoi();
                                });
                            }
                        }

                        var now = DateTime.Now;
                        if (geoPois.Count > 0 && now - lastPoiCheckTime >= poiDebounce)
                        {
                            lastPoiCheckTime = now;

                            CheckNearPoi(location);
                            await CheckEnterPoi(location);
                        }

                        if ((DateTime.Now - lastMapUpdateTime).TotalSeconds >= 1)
                        {
                            lastMapUpdateTime = DateTime.Now;

                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                _ = MoveMapToLocation(location.Latitude, location.Longitude);
                                _ = ShowUserLocation(location.Latitude, location.Longitude);
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
                                resultLabel.Text = LanguageManager.Get(
                                    "Tín hiệu GPS chưa ổn định...",
                                    "GPS signal is not stable yet...",
                                    "GPS 信号尚不稳定...",
                                    "GPS 신호가 아직 불안정합니다...",
                                    "GPS信号がまだ安定していません...",
                                    "Le signal GPS n'est pas encore stable...");
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
                        resultLabel.Text = LanguageManager.Get(
                             $"Lỗi theo dõi vị trí: {ex.Message}",
                             $"Tracking error: {ex.Message}",
                             $"追踪错误：{ex.Message}",
                             $"추적 오류: {ex.Message}",
                             $"追跡エラー: {ex.Message}",
                             $"Erreur de suivi : {ex.Message}");
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
                    TimeSpan.FromSeconds(5));

                Location? location = null;

                for (int i = 1; i <= 3; i++)
                {
                    location = await Geolocation.Default.GetLocationAsync(request);

                    if (location != null)
                    {
                        Debug.WriteLine($"[LOCATION] Lần {i}: {location.Latitude}, {location.Longitude}");
                        return new SensorLocation(location.Latitude, location.Longitude);
                    }

                    Debug.WriteLine($"[LOCATION] null lần {i}, thử lại...");
                    await Task.Delay(500);
                }

                Debug.WriteLine("[LOCATION] null sau 3 lần thử");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOCATION ERROR] {ex.Message}");
                return null;
            }
        }

        // Công dụng: kiểm tra khi người dùng đi vào geofence của POI nào
        // Công dụng: kiểm tra khi người dùng đi vào geofence của POI nào.
        // Hàm sẽ chọn POI phù hợp nhất theo ưu tiên và khoảng cách,
        // cập nhật trạng thái geofence trên giao diện,
        // rồi đưa nội dung thuyết minh vào hàng chờ audio nếu chưa phát trước đó.
        private async Task CheckEnterPoi(SensorLocation location)
        {
            Poi? bestPoi = null;
            double bestDistance = double.MaxValue;

            // 1. Tìm POI tốt nhất đang nằm trong geofence
            foreach (var poi in geoPois)
            {
                if (!IsValidCoordinate(poi.Latitude, poi.Longitude))
                {
                    Debug.WriteLine($"[ENTER SKIP INVALID] {poi.Name} | Lat={poi.Latitude} | Lng={poi.Longitude}");
                    continue;
                }

                // Lọc nhanh POI quá xa để đỡ tốn tính toán
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

                if (!isInside)
                    continue;

                if (bestPoi == null ||
                    poi.Priority > bestPoi.Priority ||
                    (poi.Priority == bestPoi.Priority && distanceMeters < bestDistance))
                {
                    bestPoi = poi;
                    bestDistance = distanceMeters;
                }
            }

            // 2. Cập nhật label geofence trên UI thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (bestPoi != null)
                {
                    geofenceLabel.Text = LanguageManager.Get(
                        $"Đã vào vùng geofence: {bestPoi.Name} ({bestDistance:F1}m)",
                        $"Entered geofence: {bestPoi.Name} ({bestDistance:F1}m)",
                        $"已进入地理围栏：{bestPoi.Name} ({bestDistance:F1}m)",
                        $"지오펜스 진입: {bestPoi.Name} ({bestDistance:F1}m)",
                        $"ジオフェンスに入りました: {bestPoi.Name} ({bestDistance:F1}m)",
                        $"Entrée dans la zone : {bestPoi.Name} ({bestDistance:F1}m)");
                    geofenceLabel.TextColor = Colors.Green;
                }
                else
                {
                    geofenceLabel.Text = LanguageManager.Get(
                        "Chưa vào vùng geofence",
                        "Not inside geofence",
                        "尚未进入地理围栏",
                        "지오펜스 영역 밖",
                        "ジオフェンス外です",
                        "Hors de la zone geofence");
                    geofenceLabel.TextColor = Colors.Red;
                }
            });

            // 3. Reset trạng thái các POI không phải bestPoi
            foreach (var poi in geoPois)
            {
                if (!poiStates.ContainsKey(poi.Id))
                    continue;

                var state = poiStates[poi.Id];
                bool isBestPoi = bestPoi != null && poi.Id == bestPoi.Id;

                if (!isBestPoi)
                {
                    state.WasInside = false;
                }
            }

            //
            if (bestPoi == null)
            {
                if (!isManualViewingPoi)
                {
                    ClearPoiDetails();
                }
                return;
            }

            if (!poiStates.ContainsKey(bestPoi.Id))
                return;

            var bestState = poiStates[bestPoi.Id];
            var now = DateTime.Now;

            // 5. Chỉ trigger khi vừa mới bước vào vùng
            if (!bestState.WasInside)
            {
                bestState.WasInside = true;
                bestState.LastTriggeredAt = now;

                isManualViewingPoi = false;

                SavePoiInfoToPreferences(bestPoi, bestDistance);
                ShowPoiDetails(bestPoi);

                if (!spokenPois.Contains(bestPoi.Id))
                {
                    string message = GetPoiTextByLanguage(bestPoi, currentLanguage);

                    if (string.IsNullOrWhiteSpace(message))
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            resultLabel.Text = LanguageManager.Get(
                            $"⚠ Không có nội dung thuyết minh cho: {bestPoi.Name}",
                            $"⚠ No narration content for: {bestPoi.Name}",
                            $"⚠ 没有讲解内容：{bestPoi.Name}",
                            $"⚠ 안내 음성이 없습니다: {bestPoi.Name}",
                            $"⚠ 説明音声がありません: {bestPoi.Name}",
                            $"⚠ Aucun contenu audio pour : {bestPoi.Name}");
                        });

                        Debug.WriteLine($"[TTS SKIP] {bestPoi.Name} không có text cho ngôn ngữ {currentLanguage}");
                        return;
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        resultLabel.Text = LanguageManager.Get(
                        $"🔊 Đang thuyết minh: {bestPoi.Name} ({bestDistance:F0}m)",
                        $"🔊 Narrating: {bestPoi.Name} ({bestDistance:F0}m)",
                        $"🔊 正在讲解：{bestPoi.Name} ({bestDistance:F0}m)",
                        $"🔊 안내 중: {bestPoi.Name} ({bestDistance:F0}m)",
                        $"🔊 音声案内中: {bestPoi.Name} ({bestDistance:F0}m)",
                        $"🔊 Lecture audio : {bestPoi.Name} ({bestDistance:F0}m)");
                    });

                    try
                    {
                        await audioManager.EnqueueAsync(new AudioJob
                        {
                            PoiId = bestPoi.Id,
                            Language = currentLanguage,
                            Text = message,
                            Priority = bestPoi.Priority
                        });

                        spokenPois.Add(bestPoi.Id);
                        Debug.WriteLine($"[TTS OK] {bestPoi.Name} | lang={currentLanguage}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[TTS ERROR] {bestPoi.Name}: {ex.Message}");

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            resultLabel.Text = LanguageManager.Get(
                        $"❌ Lỗi thuyết minh: {bestPoi.Name}",
                        $"❌ Narration error: {bestPoi.Name}",
                        $"❌ 讲解错误：{bestPoi.Name}",
                        $"❌ 안내 오류: {bestPoi.Name}",
                        $"❌ 音声案内エラー: {bestPoi.Name}",
                        $"❌ Erreur de narration : {bestPoi.Name}");
                        });
                    }
                
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
                            resultLabel.Text = LanguageManager.Get(
                                $"Bạn đang đến gần: {poi.Name} ({bestDistance:F1}m)",
                                $"You are approaching: {poi.Name} ({bestDistance:F1}m)",
                                $"您正在接近：{poi.Name} ({bestDistance:F1}m)",
                                $"가까워지고 있습니다: {poi.Name} ({bestDistance:F1}m)",
                                $"近づいています: {poi.Name} ({bestDistance:F1}m)",
                                $"Vous approchez de : {poi.Name} ({bestDistance:F1}m)");
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
                    resultLabel.Text = LanguageManager.Get(
                        "Đang theo dõi vị trí...",
                        "Tracking location...",
                        "正在追踪位置...",
                        "위치 추적 중...",
                        "位置を追跡中...",
                        "Suivi de la position...");
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

        private void OnMapWebViewNavigated(object sender, WebNavigatedEventArgs e)
        {
            isMapReady = true;
            _ = EvaluateMapScriptAsync("initMap();");
            _ = RenderMapDataAsync();
        }

        // Công dụng: di chuyển tâm bản đồ theo vị trí hiện tại của người dùng
        private async Task MoveMapToLocation(double latitude, double longitude)
            => await EvaluateMapScriptAsync($"setView({latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, 17);");

        // Công dụng: hiển thị marker vị trí hiện tại của người dùng trên bản đồ
        private async Task ShowUserLocation(double latitude, double longitude)
            => await EvaluateMapScriptAsync($"setUserLocation({latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)});");

        // Công dụng: hiển thị marker và tên các POI trên bản đồ
        private async Task ShowPoisOnMap()
        {
            await RenderMapDataAsync();

            var featureCount = geoPois.Count(p => IsValidCoordinate(p.Latitude, p.Longitude));
            MainThread.BeginInvokeOnMainThread(() =>
            {
                resultLabel.Text = LanguageManager.Get(
                    $"Đã hiển thị {featureCount} POI trên bản đồ",
                    $"Displayed {featureCount} POIs on the map",
                    $"地图上已显示 {featureCount} 个 POI",
                    $"지도에 {featureCount}개의 POI를 표시했습니다",
                    $"地図に {featureCount} 件のPOIを表示しました",
                    $"{featureCount} POI affichés sur la carte");
            });
        }

        //highlight POI gan nhat
        private async Task HighlightNearestPoi()
            => await RenderMapDataAsync();

        // Công dụng: vẽ vòng tròn geofence của từng POI trên bản đồ
        private async Task DrawGeofenceCircles()
            => await RenderMapDataAsync();

        private async Task RenderMapDataAsync()
        {
            var mapPois = geoPois
                .Where(p => IsValidCoordinate(p.Latitude, p.Longitude))
                .Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    lat = p.Latitude,
                    lng = p.Longitude,
                    radius = p.RadiusMeters,
                    isNearest = nearestPoiId.HasValue && p.Id == nearestPoiId.Value
                })
                .ToList();

            var poiJson = JsonSerializer.Serialize(mapPois);
            await EvaluateMapScriptAsync($"renderPois({JsonSerializer.Serialize(poiJson)});");
            await EvaluateMapScriptAsync($"renderGeofences({JsonSerializer.Serialize(poiJson)});");

            var firstPoi = mapPois.FirstOrDefault();
            if (firstPoi != null)
            {
                await MoveMapToLocation(firstPoi.lat, firstPoi.lng);
            }
        }

        private async Task EvaluateMapScriptAsync(string script)
        {
            if (!isMapReady)
                return;

            try
            {
                await mapWebView.EvaluateJavaScriptAsync(script);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAP JS ERROR] {ex.Message}");
            }
        }

        // Công dụng: đọc nội dung thuyết minh bằng Text To Speech theo đúng ngôn ngữ
        //private async Task SpeakText(string text, string languageCode)
        //{
        //    if (string.IsNullOrWhiteSpace(text) || isSpeaking)
        //        return;

        //    try
        //    {
        //        isSpeaking = true;

        //        var locales = await TextToSpeech.Default.GetLocalesAsync();

        //        if (locales == null || !locales.Any())
        //        {
        //            resultLabel.Text = "Thiết bị chưa có bộ máy TTS";
        //            Debug.WriteLine("[TTS ERROR] Không tìm thấy locale TTS nào");
        //            return;
        //        }

        //        string lang = (languageCode ?? "vi").Trim().ToLower();
        //        Locale? locale = null;

        //        locale = locales.FirstOrDefault(l =>
        //            !string.IsNullOrWhiteSpace(l.Language) &&
        //            l.Language.StartsWith(lang, StringComparison.OrdinalIgnoreCase));

        //        if (locale == null && lang.Contains("-"))
        //        {
        //            string shortLang = lang.Split('-')[0];
        //            locale = locales.FirstOrDefault(l =>
        //                !string.IsNullOrWhiteSpace(l.Language) &&
        //                l.Language.StartsWith(shortLang, StringComparison.OrdinalIgnoreCase));
        //        }

        //        if (locale == null)
        //        {
        //            locale = locales.FirstOrDefault(l =>
        //                !string.IsNullOrWhiteSpace(l.Language) &&
        //                l.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase));
        //        }

        //        var options = new SpeechOptions
        //        {
        //            Locale = locale,
        //            Pitch = 1.0f,
        //            Volume = 1.0f
        //        };

        //        Debug.WriteLine($"[TTS] Lang={languageCode} | Locale={locale?.Language ?? "default"} | Text={text}");

        //        await TextToSpeech.Default.SpeakAsync(text, options);

        //        Debug.WriteLine("[TTS OK] Đọc xong");
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"[TTS ERROR] {ex}");
        //        resultLabel.Text = "Thiết bị/emulator chưa hỗ trợ Text-to-Speech";
        //    }
        //    finally
        //    {
        //        isSpeaking = false;
        //    }
        //}

        // Công dụng: lấy nội dung thuyết minh theo ngôn ngữ đang chọn, có fallback về tiếng Việt
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
            ApplyLanguageToUI();

            int poiId = Preferences.Get("highlight_poi_id", -1);

            if (poiId != -1)
            {
                HighlightPoiById(poiId);
                Preferences.Remove("highlight_poi_id");
            }
        }
        // Công dụng: kiểm tra latitude/longitude có nằm trong khoảng hợp lệ không
        private bool IsValidCoordinate(double latitude, double longitude)
        {
            return latitude >= -90 && latitude <= 90 &&
                   longitude >= -180 && longitude <= 180;
        }
        // dừng audio đang phát
        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            audioManager.StopCurrent(); 
        }
        // Hiển thị thông tin POI (tên, mô tả, ảnh) lên giao diện theo ngôn ngữ hiện tại
        // Hiển thị thông tin POI (tên, mô tả, ảnh) lên giao diện theo ngôn ngữ hiện tại
        private void ShowPoiDetails(Poi poi)
        {
            if (poi == null)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                resultLabel.Text = LanguageManager.Get(
                    $"Đang hiển thị thông tin POI: {poi.Name}",
                    $"Showing POI details: {poi.Name}",
                    $"正在显示 POI 信息：{poi.Name}",
                    $"POI 정보 표시 중: {poi.Name}",
                    $"POI情報を表示中: {poi.Name}",
                    $"Affichage des détails du POI : {poi.Name}");
            });
        }
        // Xóa thông tin POI đang hiển thị trên giao diện (tên, mô tả, ảnh) khi không còn trong vùng geofence
        private void ClearPoiDetails()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                //poiNameLabel.Text = "Chưa có POI đang hiển thị";
                //poiDescriptionLabel.Text = "Mô tả POI sẽ hiển thị ở đây";
                //poiImage.Source = null;
                //poiImage.IsVisible = false;
            });
        }
        //chi tiết poi gần nhất
        private async void OnViewNearestPoiClicked(object sender, EventArgs e)
        {
            if (nearestPoiCurrent == null)
            {
                await DisplayAlert(
                    LanguageManager.Get("Thông báo", "Notice", "通知", "알림", "お知らせ", "Notification"),
                    LanguageManager.Get(
                        "Chưa xác định được POI gần nhất.",
                        "Nearest POI has not been determined yet.",
                        "尚未确定最近的 POI。",
                        "가장 가까운 POI가 아직 확인되지 않았습니다.",
                        "最寄りのPOIがまだ特定されていません。",
                        "Le POI le plus proche n'a pas encore été déterminé."),
                    "OK");
                return;
            }

            double distanceMeters = 0;

            var currentLocation = await GetLocation();
            if (currentLocation != null)
            {
                distanceMeters = SensorLocation.CalculateDistance(
                    currentLocation,
                    new SensorLocation(nearestPoiCurrent.Latitude, nearestPoiCurrent.Longitude),
                    DistanceUnits.Kilometers) * 1000;
            }

            SavePoiInfoToPreferences(nearestPoiCurrent, distanceMeters);

            isManualViewingPoi = true;
            await Navigation.PushAsync(new PoiInfoPage());
        }
        //lưu POI hiện tại để tab POI đọc và hiển thị
        private void SavePoiInfoToPreferences(Poi poi, double distanceMeters = 0)
        {
            if (poi == null) return;

            string description = GetPoiTextByLanguage(poi, currentLanguage);

            Preferences.Set(
                 "poi_name",
                 poi.Name ?? LanguageManager.Get(
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
            Preferences.Set("poi_image_url", poi.ImageUrl ?? "");
            Preferences.Set("poi_distance", distanceMeters.ToString("F1"));
        }
        //quet qr
        private async void OnScanQrClicked(object sender, EventArgs e)
        {
            try
            {
                var cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();

                if (cameraStatus != PermissionStatus.Granted)
                {
                    await DisplayAlert(
                        LanguageManager.Get("Lỗi", "Error", "错误", "오류", "エラー", "Erreur"),
                        LanguageManager.Get(
                            "Bạn chưa cấp quyền camera",
                            "Camera permission has not been granted",
                            "您尚未授予相机权限",
                            "카메라 권한이 허용되지 않았습니다",
                            "カメラ権限が許可されていません",
                            "L'autorisation de la caméra n'a pas été accordée"),
                        "OK");
                    return;
                }

                await Navigation.PushModalAsync(new QrScannerPage());
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    LanguageManager.Get("Lỗi", "Error", "错误", "오류", "エラー", "Erreur"),
                    LanguageManager.Get(
                        $"Không mở được màn hình quét QR: {ex.Message}",
                        $"Cannot open QR scanner screen: {ex.Message}",
                        $"无法打开二维码扫描界面：{ex.Message}",
                        $"QR 스캔 화면을 열 수 없습니다: {ex.Message}",
                        $"QRスキャン画面を開けません: {ex.Message}",
                        $"Impossible d'ouvrir l'écran de scan QR : {ex.Message}"),
                    "OK");
            }
        }
        private void HighlightPoiById(int poiId)
        {
            var poi = geoPois.FirstOrDefault(p => p.Id == poiId);
            if (poi == null)
                return;

            Debug.WriteLine($"[HIGHLIGHT] {poi.Name}");

            // 🔥 Gán lại POI đang chọn thành nearest
            nearestPoiId = poi.Id;
            nearestPoiCurrent = poi;

            // 🔥 Gọi lại hàm highlight sẵn có
            _ = HighlightNearestPoi();
            _ = MoveMapToLocation(poi.Latitude, poi.Longitude);
        }
        private void ApplyLanguageToUI()
        {
            Title = LanguageManager.Get("Trang chủ", "Home", "主页", "홈", "ホーム", "Accueil");

            appTitleLabel.Text = LanguageManager.Get(
                "HỆ THỐNG THUYẾT MINH ẨM THỰC",
                "FOOD AUDIO GUIDE SYSTEM",
                "美食语音导览系统",
                "음식 음성 안내 시스템",
                "グルメ音声ガイドシステム",
                "SYSTÈME DE GUIDE AUDIO CULINAIRE");

            subTitleLabel.Text = LanguageManager.Get(
                "PoC GPS Tracking",
                "PoC GPS Tracking",
                "PoC GPS 定位跟踪",
                "PoC GPS 추적",
                "PoC GPS追跡",
                "Suivi GPS PoC");

            startTrackingButton.Text = LanguageManager.Get(
                "Bắt đầu theo dõi",
                "Start tracking",
                "开始追踪",
                "추적 시작",
                "追跡開始",
                "Démarrer le suivi");

            stopTrackingButton.Text = LanguageManager.Get(
                "Dừng theo dõi",
                "Stop tracking",
                "停止追踪",
                "추적 중지",
                "追跡停止",
                "Arrêter le suivi");

            scanQrButton.Text = LanguageManager.Get(
                "Quét QR",
                "Scan QR",
                "扫描二维码",
                "QR 스캔",
                "QRをスキャン",
                "Scanner le QR");

            viewNearestPoiButton.Text = LanguageManager.Get(
                "Xem chi tiết POI gần nhất",
                "View nearest POI details",
                "查看最近兴趣点详情",
                "가장 가까운 POI 상세 보기",
                "最寄りPOIの詳細を見る",
                "Voir le POI le plus proche");

            if (string.IsNullOrWhiteSpace(locationLabel.Text) || locationLabel.Text == "Chưa có vị trí")
            {
                locationLabel.Text = LanguageManager.Get(
                    "Chưa có vị trí",
                    "No location yet",
                    "暂无位置",
                    "위치 정보 없음",
                    "位置情報がありません",
                    "Aucune position");
            }

            if (string.IsNullOrWhiteSpace(resultLabel.Text) || resultLabel.Text == "Chưa tải POI")
            {
                resultLabel.Text = LanguageManager.Get(
                    "Chưa tải POI",
                    "POIs not loaded",
                    "尚未加载 POI",
                    "POI가 아직 로드되지 않음",
                    "POIはまだ読み込まれていません",
                    "POI non chargés");
            }

            if (string.IsNullOrWhiteSpace(geofenceLabel.Text) || geofenceLabel.Text == "Chưa vào vùng geofence")
            {
                geofenceLabel.Text = LanguageManager.Get(
                    "Chưa vào vùng geofence",
                    "Not inside geofence",
                    "尚未进入地理围栏",
                    "지오펜스 영역 밖",
                    "ジオフェンス外です",
                    "Hors de la zone geofence");
            }
        }

    }
}
