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

        private bool isMapReady = false;
        private Poi? nearestPoiCurrent = null;

        // Thời điểm check POI gần nhất để tránh spam
        private DateTime lastPoiCheckTime = DateTime.MinValue;
        private DateTime lastAnalyticsLocationTime = DateTime.MinValue;

        // Debounce kiểm tra POI
        private readonly TimeSpan poiDebounce = TimeSpan.FromSeconds(2);

        // Cooldown tránh TTS đọc lặp liên tục
        private readonly TimeSpan poiCooldown = TimeSpan.FromSeconds(5);
        private readonly TimeSpan nearPoiAlertCooldown = TimeSpan.FromSeconds(20);
        private readonly TimeSpan trackingInterval = TimeSpan.FromSeconds(4);
        private readonly TimeSpan trackingRetryInterval = TimeSpan.FromSeconds(8);
        private readonly TimeSpan locationTimeout = TimeSpan.FromSeconds(6);
        private readonly TimeSpan locationRetryDelay = TimeSpan.FromMilliseconds(700);
        private readonly TimeSpan analyticsLocationInterval = TimeSpan.FromSeconds(15);

        // Ngôn ngữ hiện tại đang chọn
        private string currentLanguage = "vi";

        // Bán kính geofence mặc định nếu POI từ API không có radius
        private double currentGeofenceRadius = 30.0;

        // Tránh chồng nhiều lệnh TTS cùng lúc
        //private bool isSpeaking = false;
        private HashSet<int> spokenPois = new();
        private DateTime lastMapUpdateTime = DateTime.MinValue;
        private readonly AudioQueueManager audioManager;
        private readonly IForegroundTrackingService foregroundTrackingService;
        private int? nearestPoiId = null;
        private bool isManualViewingPoi = false;
        private int? stablePoiId = null;
        private int? pendingPoiId = null;
        private DateTime pendingPoiSince = DateTime.MinValue;
        private readonly TimeSpan poiSwitchDelay = TimeSpan.FromSeconds(2);
        private readonly TimeSpan poiReplayCooldown = TimeSpan.FromSeconds(30);
        private readonly AnalyticsService analytics = new();
        private readonly PoiService poiService = new();
        private const string LeafletHtmlContent = """
<!DOCTYPE html>
<html>
<head>
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
  <style>
    html, body, #map { height: 100%; margin: 0; padding: 0; background: #e5eef6; }
    .leaflet-popup-content-wrapper { border-radius: 14px; }
    .leaflet-control-attribution { font-size: 10px; }
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
        radius: 9,
        color: '#ffffff',
        weight: 2,
        fillColor: '#2563eb',
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
          radius: p.isNearest ? 12 : 7,
          color: p.isNearest ? '#7c2d12' : '#ffffff',
          weight: p.isNearest ? 3 : 2,
          fillColor: p.isNearest ? '#f59e0b' : '#ef4444',
          fillOpacity: p.isNearest ? 1 : 0.92
        });
        marker.bindPopup(`<strong>${p.name}</strong>${p.isNearest ? '<br/><span style="color:#b45309;font-weight:700">POI gần nhất</span>' : ''}`);
        marker.addTo(poiLayer);
      });
    }

    function renderGeofences(poisJson) {
      if (!map) return;
      geofenceLayer.clearLayers();
      const pois = JSON.parse(poisJson);
      pois.forEach(p => {
        const stroke = p.isNearest ? 'rgba(245,158,11,0.95)' : 'rgba(239,68,68,0.65)';
        const fill = p.isNearest ? 'rgba(245,158,11,0.18)' : 'rgba(239,68,68,0.12)';
        L.circle([p.lat, p.lng], {
          radius: Math.max(p.radius, 80),
          color: stroke,
          fillColor: fill,
          fillOpacity: p.isNearest ? 0.26 : 0.18,
          weight: 2
        }).addTo(geofenceLayer);
      });
    }
  </script>
</body>
</html>
""";
        // Công dụng: khởi tạo trang map, audio queue và dữ liệu ban đầu.
        public MainPage(IAudioFocusService audioFocusService, IForegroundTrackingService foregroundTrackingService)
        {
            InitializeComponent();
            this.foregroundTrackingService = foregroundTrackingService;

            // audio
            audioManager = new AudioQueueManager(audioFocusService);
            audioManager.IsStillRelevant = poiId =>
                !isTracking ||
                stablePoiId == poiId ||
                nearestPoiId == poiId;

            mapWebView.Source = new HtmlWebViewSource
            {
                Html = LeafletHtmlContent
            };

            audioManager.Start();

            LoadAppSettings();

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

        // Công dụng: tải danh sách POI từ API và loại bỏ POI có tọa độ sai
        private async Task LoadPois()
        {
            try
            {
                var data = await poiService.GetPoisAsync();
                geoPois = NormalizePoisForTracking(data);
                var cacheSuffix = poiService.LastResultFromCache ? " (cache)" : "";

                Debug.WriteLine($"Đã tải {geoPois.Count} POI hợp lệ");
                resultLabel.Text = LanguageManager.Get(
                    $"Đã tải {geoPois.Count} POI{cacheSuffix}",
                    $"Loaded {geoPois.Count} POIs{cacheSuffix}",
                    $"已加载 {geoPois.Count} 个 POI{cacheSuffix}",
                    $"{geoPois.Count}개의 POI를 불러왔습니다{cacheSuffix}",
                    $"{geoPois.Count} 件のPOIを読み込みました{cacheSuffix}",
                    $"{geoPois.Count} POI chargés{cacheSuffix}");
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

        // Công dụng: lọc POI có tọa độ hợp lệ và chuẩn hóa bán kính trước khi dùng cho map/geofence.
        private List<Poi> NormalizePoisForTracking(IEnumerable<Poi> data)
        {
            var validPois = (data ?? Array.Empty<Poi>())
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

            foreach (var poi in validPois)
            {
                if (poi.RadiusMeters <= 0)
                {
                    poi.RadiusMeters = currentGeofenceRadius;
                }

                poi.NearRadiusMeters = poi.NearRadiusMeters > 0
                    ? poi.NearRadiusMeters
                    : poi.RadiusMeters + 50;

                Debug.WriteLine($"[POI OK] {poi.Name} | radius={poi.RadiusMeters} | near={poi.NearRadiusMeters}");
            }

            return validPois;
        }

        // Công dụng: xử lý nút bắt đầu theo dõi vị trí.
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
            lastAnalyticsLocationTime = DateTime.MinValue;

            trackingCts?.Cancel();
            trackingCts = new CancellationTokenSource();

            try
            {
                foregroundTrackingService.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FOREGROUND TRACKING ERROR] {ex.Message}");
            }

            _ = Task.Run(() => StartTracking(trackingCts.Token));
        }
        // Công dụng: xử lý nút dừng theo dõi vị trí.
        private void OnStopTrackingClicked(object sender, EventArgs e)
        {
            if (!isTracking) return;
            audioManager.StopAll();
            foregroundTrackingService.Stop();
            isTracking = false;
            trackingCts.Cancel();
            trackingCts = new CancellationTokenSource();
            stablePoiId = null;
            pendingPoiId = null;
            pendingPoiSince = DateTime.MinValue;
            nearestPoiId = null;

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

        // Công dụng: chạy vòng lặp theo dõi vị trí và cập nhật UI theo dữ liệu GPS hiện có.
        private async Task StartTracking(CancellationToken cancellationToken)
        {
            int nullLocationCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var nextDelay = trackingInterval;

                try
                {
                    var location = await GetLocation(cancellationToken);

                    if (location != null)
                    {
                        nullLocationCount = 0;
                        TrackLocationIfNeeded(location);
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
                                    UpdateNearestPoiSummary(nearestPoi.Poi, nearestPoi.Distance);
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

                        if (DateTime.Now - lastMapUpdateTime >= trackingInterval)
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
                        nextDelay = trackingRetryInterval;
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

                    await Task.Delay(nextDelay, cancellationToken);
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

                    try
                    {
                        await Task.Delay(trackingRetryInterval, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        // Công dụng: gửi analytics vị trí theo khoảng thời gian giãn cách để tránh ghi quá dày.
        private void TrackLocationIfNeeded(SensorLocation location)
        {
            var now = DateTime.UtcNow;
            if (now - lastAnalyticsLocationTime < analyticsLocationInterval)
            {
                return;
            }

            lastAnalyticsLocationTime = now;
            _ = analytics.TrackLocation(location.Latitude, location.Longitude);
        }

        // Công dụng: lấy vị trí hiện tại hoặc vị trí gần nhất của thiết bị.
        private async Task<SensorLocation?> GetLocation(CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new GeolocationRequest(
                    GeolocationAccuracy.Best,
                    locationTimeout);

                Location? location = null;

                for (int i = 1; i <= 2; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    location = await Geolocation.Default.GetLocationAsync(request, cancellationToken);

                    if (location != null)
                    {
                        Debug.WriteLine($"[LOCATION] Lần {i}: {location.Latitude}, {location.Longitude}");
                        return new SensorLocation(location.Latitude, location.Longitude);
                    }

                    Debug.WriteLine($"[LOCATION] null lần {i}, thử lại...");
                    await Task.Delay(locationRetryDelay, cancellationToken);
                }

                var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
                if (lastKnown != null && DateTimeOffset.UtcNow - lastKnown.Timestamp <= TimeSpan.FromMinutes(2))
                {
                    Debug.WriteLine($"[LOCATION] Dùng last known: {lastKnown.Latitude}, {lastKnown.Longitude}");
                    return new SensorLocation(lastKnown.Latitude, lastKnown.Longitude);
                }

                Debug.WriteLine("[LOCATION] null sau các lần thử");
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOCATION ERROR] {ex.Message}");
                return null;
            }
        }

        // Công dụng: kiểm tra khi người dùng đi vào geofence của POI nào.
        // Hàm sẽ chọn POI phù hợp nhất theo ưu tiên và khoảng cách,
        // cập nhật trạng thái geofence trên giao diện,
        // rồi đưa nội dung thuyết minh vào hàng chờ audio nếu chưa phát trước đó.
        private async Task CheckEnterPoi(SensorLocation location)
        {
            Poi? bestPoi = null;
            double bestDistance = double.MaxValue;
            var now = DateTime.Now;

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

                // Ưu tiên: Priority cao hơn, nếu bằng thì gần hơn, nếu vẫn bằng thì giữ POI ổn định.
                if (IsBetterPoiCandidate(poi, distanceMeters, bestPoi, bestDistance))
                {
                    bestPoi = poi;
                    bestDistance = distanceMeters;
                }
            }

            // 2. Ổn định POI theo thời gian để tránh nhảy qua lại do GPS rung
            var instantPoi = bestPoi;
            bestPoi = ResolveStablePoi(instantPoi);

            if (bestPoi != null)
            {
                bestDistance = SensorLocation.CalculateDistance(
                    location,
                    new SensorLocation(bestPoi.Latitude, bestPoi.Longitude),
                    DistanceUnits.Kilometers) * 1000;
            }

            // 3. Cập nhật label geofence trên UI thread
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

            // 4. Reset trạng thái các POI không phải bestPoi
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

            // 5. Nếu không có POI hợp lệ thì clear chi tiết
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

            // 6. Chỉ trigger khi:
            // - vừa mới bước vào vùng
            // - và đã qua cooldown phát lại
            bool canTrigger =
                !bestState.WasInside &&
                (now - bestState.LastTriggeredAt >= poiReplayCooldown);

            if (!canTrigger)
                return;

            bestState.WasInside = true;
            bestState.LastTriggeredAt = now;
            _ = analytics.TrackEnterPoi(bestPoi.Id);
            isManualViewingPoi = false;

            SavePoiInfoToPreferences(bestPoi, bestDistance);
            ShowPoiDetails(bestPoi);

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
                _ = analytics.TrackListen(bestPoi.Id, 10);
                await audioManager.EnqueueAsync(new AudioJob
                {
                    PoiId = bestPoi.Id,
                    Language = currentLanguage,
                    Text = message,
                    Priority = bestPoi.Priority,
                    CreatedAt = DateTime.UtcNow
                });

                Debug.WriteLine($"[QUEUE OK] {bestPoi.Name} | lang={currentLanguage}");
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
                    if (IsBetterPoiCandidate(poi, distanceMeters, bestPoi, bestDistance))
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

                    var now = DateTime.Now;
                    var canNotifyNear = now - state.LastNearAlertAt >= nearPoiAlertCooldown;

                    if (!state.WasNear && canNotifyNear)
                    {
                        state.WasNear = true;
                        state.LastNearAlertAt = now;

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

        // Công dụng: đánh dấu WebView bản đồ đã sẵn sàng rồi render POI/geofence lên map.
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

        // Công dụng: render lại marker để POI gần nhất được highlight trên bản đồ.
        private async Task HighlightNearestPoi()
            => await RenderMapDataAsync();

        // Công dụng: vẽ vòng tròn geofence của từng POI trên bản đồ
        private async Task DrawGeofenceCircles()
            => await RenderMapDataAsync();

        // Công dụng: cập nhật card POI gần nhất trên màn map mà không thay đổi logic tracking.
        private void UpdateNearestPoiSummary(Poi? poi, double? distanceMeters)
        {
            if (poi == null)
            {
                nearestPoiNameLabel.Text = LanguageManager.Get(
                    "Chưa xác định POI gần nhất",
                    "Nearest POI not determined",
                    "尚未确定最近 POI",
                    "가장 가까운 POI 미확인",
                    "最寄りPOIは未確認",
                    "POI le plus proche non déterminé");

                nearestPoiDistanceLabel.Text = LanguageManager.Get(
                    "Bật theo dõi để hệ thống tự tìm điểm gần nhất.",
                    "Start tracking so the app can find the nearest place.",
                    "开启追踪以自动查找最近地点。",
                    "추적을 시작하면 가장 가까운 장소를 찾습니다.",
                    "追跡を開始すると最寄りスポットを探します。",
                    "Lancez le suivi pour trouver le lieu le plus proche.");

                nearestStatusBadgeLabel.Text = LanguageManager.Get(
                    "ĐANG CHỜ",
                    "WAITING",
                    "等待中",
                    "대기 중",
                    "待機中",
                    "EN ATTENTE");

                viewNearestPoiButton.IsEnabled = false;
                return;
            }

            nearestPoiNameLabel.Text = string.IsNullOrWhiteSpace(poi.Name)
                ? LanguageManager.Get("Chưa có POI", "No POI", "暂无 POI", "POI 없음", "POIなし", "Aucun POI")
                : poi.Name;

            nearestPoiDistanceLabel.Text = distanceMeters.HasValue
                ? LanguageManager.Get(
                    $"Cách bạn khoảng {distanceMeters.Value:0} m",
                    $"About {distanceMeters.Value:0} m away",
                    $"距离约 {distanceMeters.Value:0} 米",
                    $"약 {distanceMeters.Value:0} m 거리",
                    $"約 {distanceMeters.Value:0} m 先",
                    $"À environ {distanceMeters.Value:0} m")
                : LanguageManager.Get(
                    "Sẵn sàng mở chi tiết POI này.",
                    "Ready to open this POI detail.",
                    "可打开此 POI 详情。",
                    "이 POI 상세 정보를 열 수 있습니다.",
                    "このPOI詳細を開けます。",
                    "Prêt à ouvrir ce POI.");

            nearestStatusBadgeLabel.Text = LanguageManager.Get(
                "GẦN NHẤT",
                "NEAREST",
                "最近",
                "가장 가까움",
                "最寄り",
                "LE PLUS PROCHE");

            viewNearestPoiButton.IsEnabled = true;
        }

        // Công dụng: render dữ liệu POI và geofence lên bản đồ WebView.
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

            var focusPoi = mapPois.FirstOrDefault(p => p.isNearest) ?? mapPois.FirstOrDefault();
            if (focusPoi != null)
            {
                await MoveMapToLocation(focusPoi.lat, focusPoi.lng);
            }
        }

        // Công dụng: chạy JavaScript an toàn trên WebView bản đồ khi map đã sẵn sàng.
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
        // Công dụng: tạo thông báo ngôn ngữ hiện tại để hiển thị trên UI.
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
        // Công dụng: lưu ngôn ngữ được chọn và cập nhật lại giao diện.
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
            _ = analytics.TrackAppOpen();
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
        // Công dụng: kiểm tra tọa độ có nằm trong giới hạn hợp lệ hay không.
        private bool IsValidCoordinate(double latitude, double longitude)
        {
            return latitude >= -90 && latitude <= 90 &&
                   longitude >= -180 && longitude <= 180;
        }
        // dừng audio đang phát
        // Công dụng: dừng audio queue khi rời khỏi trang map.
        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            audioManager.StopCurrent(); 
        }
        // Công dụng: hiển thị thông tin POI theo ngôn ngữ hiện tại.
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
        // Công dụng: xóa thông tin POI đang hiển thị khi không còn trong vùng geofence.
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
        // Công dụng: mở trang chi tiết cho POI gần nhất đang được xác định.
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
        // Công dụng: lưu POI hiện tại vào Preferences để trang chi tiết đọc và hiển thị.
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
        // Công dụng: mở màn hình quét QR sau khi kiểm tra quyền camera.
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
        // Công dụng: highlight POI được chọn từ tab danh sách và đưa bản đồ tới đúng vị trí.
        private void HighlightPoiById(int poiId)
        {
            var poi = geoPois.FirstOrDefault(p => p.Id == poiId);
            if (poi == null)
                return;

            Debug.WriteLine($"[HIGHLIGHT] {poi.Name}");

            // 🔥 Gán lại POI đang chọn thành nearest
            nearestPoiId = poi.Id;
            nearestPoiCurrent = poi;
            UpdateNearestPoiSummary(poi, null);

            // 🔥 Gọi lại hàm highlight sẵn có
            _ = HighlightNearestPoi();
            _ = MoveMapToLocation(poi.Latitude, poi.Longitude);
        }
        // Công dụng: áp dụng ngôn ngữ đang chọn cho các thành phần UI chính của trang map.
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

            UpdateNearestPoiSummary(nearestPoiCurrent, null);
        }
        // Công dụng: so sánh ứng viên POI theo Priority, khoảng cách, rồi ưu tiên POI đang ổn định để tránh nhảy liên tục.
        private bool IsBetterPoiCandidate(Poi candidate, double candidateDistance, Poi? currentBest, double currentBestDistance)
        {
            if (currentBest == null)
            {
                return true;
            }

            if (candidate.Priority != currentBest.Priority)
            {
                return candidate.Priority > currentBest.Priority;
            }

            const double sameDistanceToleranceMeters = 3;
            var distanceDelta = candidateDistance - currentBestDistance;
            if (Math.Abs(distanceDelta) > sameDistanceToleranceMeters)
            {
                return candidateDistance < currentBestDistance;
            }

            if (stablePoiId.HasValue)
            {
                if (candidate.Id == stablePoiId.Value && currentBest.Id != stablePoiId.Value)
                {
                    return true;
                }

                if (currentBest.Id == stablePoiId.Value && candidate.Id != stablePoiId.Value)
                {
                    return false;
                }
            }

            return candidate.Id < currentBest.Id;
        }

        // Công dụng: chọn POI ổn định theo thời gian, tránh nhảy liên tục giữa 2 POI
        private Poi? ResolveStablePoi(Poi? candidatePoi)
        {
            var now = DateTime.Now;

            // Không có ứng viên nào
            if (candidatePoi == null)
            {
                pendingPoiId = null;
                pendingPoiSince = DateTime.MinValue;
                stablePoiId = null;
                return null;
            }

            // Nếu chưa có POI ổn định thì nhận luôn
            if (stablePoiId == null)
            {
                stablePoiId = candidatePoi.Id;
                pendingPoiId = null;
                pendingPoiSince = DateTime.MinValue;
                return candidatePoi;
            }

            // Nếu ứng viên hiện tại chính là POI đang ổn định thì giữ nguyên
            if (candidatePoi.Id == stablePoiId.Value)
            {
                pendingPoiId = null;
                pendingPoiSince = DateTime.MinValue;
                return candidatePoi;
            }

            var currentStablePoi = geoPois.FirstOrDefault(p => p.Id == stablePoiId.Value);
            if (currentStablePoi != null && candidatePoi.Priority > currentStablePoi.Priority)
            {
                stablePoiId = candidatePoi.Id;
                pendingPoiId = null;
                pendingPoiSince = DateTime.MinValue;
                return candidatePoi;
            }

            // Nếu là POI mới, bắt đầu đếm thời gian chờ đổi
            if (pendingPoiId == null || pendingPoiId.Value != candidatePoi.Id)
            {
                pendingPoiId = candidatePoi.Id;
                pendingPoiSince = now;

                // Tạm thời vẫn giữ POI cũ
                return geoPois.FirstOrDefault(p => p.Id == stablePoiId.Value);
            }

            // Nếu POI mới giữ ưu thế đủ lâu thì đổi sang nó
            if (now - pendingPoiSince >= poiSwitchDelay)
            {
                stablePoiId = candidatePoi.Id;
                pendingPoiId = null;
                pendingPoiSince = DateTime.MinValue;
                return candidatePoi;
            }

            // Chưa đủ lâu -> vẫn giữ POI cũ
            return geoPois.FirstOrDefault(p => p.Id == stablePoiId.Value);
        }

    }
}
