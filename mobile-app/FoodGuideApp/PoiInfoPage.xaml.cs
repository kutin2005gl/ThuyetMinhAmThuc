using FoodGuideApp.Services;
using Microsoft.Maui.Storage;

namespace FoodGuideApp;

public partial class PoiInfoPage : ContentPage
{
    private CancellationTokenSource? speechCts;
    private bool isSpeaking = false;

    private readonly PoiService _poiService = new();

    // Công dụng: khởi tạo giao diện chi tiết POI đang được lưu trong Preferences.
    public PoiInfoPage()
    {
        InitializeComponent();
    }

    // Công dụng: nạp lại thông tin POI và chuẩn bị audio, không tự phát khi mở trang.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLanguageToButtons();
        LoadPoiInfo();
        SetAudioReadyState();
    }

    // Công dụng: dừng audio khi rời khỏi trang chi tiết POI.
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopSpeaking();
    }

    // Công dụng: đổi nhãn nút phát lại/dừng theo ngôn ngữ đang chọn.
    private void ApplyLanguageToButtons()
    {
        replayButton.Text = LanguageManager.Get(
            "Nghe lại",
            "Replay",
            "重新播放",
            "다시 듣기",
            "再生",
            "Réécouter"
        );

        stopButton.Text = LanguageManager.Get(
            "Dừng",
            "Stop",
            "停止",
            "중지",
            "停止",
            "Arrêter"
        );
    }

    // Công dụng: giữ audio ở trạng thái sẵn sàng, chỉ phát khi người dùng bấm nghe lại.
    private void SetAudioReadyState()
    {
        audioStatusLabel.Text = LanguageManager.Get(
            "Sẵn sàng phát audio",
            "Ready to play",
            "可以播放音频",
            "오디오 재생 준비 완료",
            "音声の再生準備完了",
            "Prêt à lire"
        );
    }

    // Công dụng: đọc tên, mô tả, ảnh, khoảng cách và ngôn ngữ của POI từ Preferences để hiển thị.
    private void LoadPoiInfo()
    {
        string name = Preferences.Get("poi_name", "Chưa có POI");
        string description = Preferences.Get("poi_description", "Mô tả POI sẽ hiển thị ở đây");
        string imageUrl = Preferences.Get("poi_image_url", "");
        string distance = Preferences.Get("poi_distance", "--");
        string language = Preferences.Get("app_language", "vi");

        poiNameLabel.Text = name;
        poiDescriptionLabel.Text = description;
        poiLanguageLabel.Text = LanguageManager.Get(
            $"Ngôn ngữ: {GetLanguageDisplayName(language)}",
            $"Language: {GetLanguageDisplayName(language)}",
            $"语言：{GetLanguageDisplayName(language)}",
            $"언어: {GetLanguageDisplayName(language)}",
            $"言語: {GetLanguageDisplayName(language)}",
            $"Langue : {GetLanguageDisplayName(language)}"
        );

        if (string.IsNullOrWhiteSpace(distance) ||
            distance == "--" ||
            distance == "0" ||
            distance == "0.0" ||
            distance == "0.00")
        {
            poiDistanceLabel.Text = LanguageManager.Get(
                "Khoảng cách: chưa xác định",
                "Distance: unknown",
                "距离：未确定",
                "거리: 확인되지 않음",
                "距離: 未確認",
                "Distance : inconnue"
            );
        }
        else
        {
            poiDistanceLabel.Text = LanguageManager.Get(
                $"Khoảng cách: {distance} m",
                $"Distance: {distance} m",
                $"距离：{distance} 米",
                $"거리: {distance} m",
                $"距離: {distance} m",
                $"Distance : {distance} m"
            );
        }

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            var imageSource = CreateCachedImageSource(imageUrl);
            if (imageSource != null)
            {
                poiImage.Source = imageSource;
                poiImage.IsVisible = true;
            }
            else
            {
                poiImage.Source = null;
                poiImage.IsVisible = false;
            }
        }
        else
        {
            poiImage.Source = null;
            poiImage.IsVisible = false;
        }
    }

    // Công dụng: dùng cache ảnh remote để tránh tải lại ảnh lớn khi mở lại chi tiết POI.
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
            CacheValidity = TimeSpan.FromDays(7)
        };
    }

    // Công dụng: phát nội dung thuyết minh hiện tại bằng Text To Speech theo ngôn ngữ đã chọn.
    private async Task SpeakCurrentPoiAsync()
    {
        string text = Preferences.Get("poi_description", "");
        string language = Preferences.Get("app_language", "vi");

        if (string.IsNullOrWhiteSpace(text))
        {
            audioStatusLabel.Text = LanguageManager.Get(
                "Không có nội dung để phát", "No content to play", "没有可播放内容",
                "재생할 내용이 없습니다", "再生する内容がありません", "Aucun contenu à lire");
            return;
        }

        if (isSpeaking) return;

        // --- BƯỚC 1: KHAI BÁO BIẾN ĐO THỜI GIAN ---
        var startTime = DateTime.UtcNow;

        try
        {
            isSpeaking = true;

            speechCts?.Cancel();
            speechCts = new CancellationTokenSource();

            audioStatusLabel.Text = LanguageManager.Get(
                "Đang phát audio...", "Playing audio...", "正在播放音频...",
                "오디오 재생 중...", "音声を再生中...", "Lecture en cours...");

            var locales = await TextToSpeech.Default.GetLocalesAsync();
            Locale? locale = locales?.FirstOrDefault(l =>
                l.Language.StartsWith(language, StringComparison.OrdinalIgnoreCase))
                ?? locales?.FirstOrDefault(l => l.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase));

            var options = new SpeechOptions { Locale = locale, Pitch = 1.0f, Volume = 1.0f };

            // --- BƯỚC 2: PHÁT ÂM THANH VÀ CHỜ ĐỢI ---
            // TTS sẽ chạy cho đến khi hết bài hoặc bị Cancel
            await TextToSpeech.Default.SpeakAsync(text, options, speechCts.Token);

            audioStatusLabel.Text = LanguageManager.Get(
                "Đã phát xong", "Finished playing", "播放完成",
                "재생 완료", "再生完了", "Lecture terminée");
        }
        catch (OperationCanceledException)
        {
            audioStatusLabel.Text = LanguageManager.Get("Đã dừng audio", "Audio stopped", "音频已停止", "오디오 중지됨", "音声停止", "Audio arrêté");
        }
        catch (Exception ex)
        {
            audioStatusLabel.Text = LanguageManager.Get("Lỗi audio", "Audio error", "音频错误", "오디오 오류", "音声エラー", "Erreur audio") + $": {ex.Message}";
        }
        finally
        {
            // --- BƯỚC 3: TÍNH TOÁN VÀ GỬI LOG VỀ SERVER ---
            // Đặt ở finally để dù đang nghe mà nhấn "Dừng" (Cancel) thì vẫn ghi nhận số giây đã nghe được
            try
            {
                var endTime = DateTime.UtcNow;
                var durationInSeconds = (int)(endTime - startTime).TotalSeconds;

                // Chỉ gửi log nếu người dùng thực sự có nghe (ít nhất 1 giây)
                if (durationInSeconds >= 1)
                {
                    int poiId = Preferences.Get("highlight_poi_id", 0);
                    string sessionId = Preferences.Get("guest_session_id", "guest_user");

                    var analyticsData = new
                    {
                        SessionId = sessionId,
                        EventType = "poi_listen", // Dùng type chuẩn theo code Backend của ông
                        PoiId = poiId,
                        DurationSeconds = durationInSeconds // Gửi con số thực tế này lên
                    };

                    await _poiService.SendAnalyticsAsync(analyticsData);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Lỗi gửi log: " + ex.Message); }

            isSpeaking = false;
        }
    }

    // Công dụng: hủy yêu cầu phát âm thanh hiện tại nếu còn đang chạy.
    private void StopSpeaking()
    {
        if (speechCts != null && !speechCts.IsCancellationRequested)
        {
            speechCts.Cancel();
        }

        isSpeaking = false;
    }

    // Công dụng: xử lý nút nghe lại nội dung thuyết minh POI.
    private async void OnReplayClicked(object sender, EventArgs e)
    {
        StopSpeaking();
        await SpeakCurrentPoiAsync();
    }

    // Công dụng: xử lý nút dừng phát thuyết minh POI.
    private void OnStopClicked(object sender, EventArgs e)
    {
        StopSpeaking();
        audioStatusLabel.Text = LanguageManager.Get(
            "Đã dừng audio",
            "Audio stopped",
            "音频已停止",
            "오디오 중지됨",
            "音声停止",
            "Audio arrêté"
        );
    }

    // Công dụng: chuyển mã ngôn ngữ thành tên dễ đọc để hiển thị trên trang detail.
    private string GetLanguageDisplayName(string language)
    {
        return (language ?? "vi").Trim().ToLowerInvariant() switch
        {
            "en" => "English",
            "zh" => "中文",
            "ko" => "한국어",
            "ja" => "日本語",
            "fr" => "Français",
            _ => "Tiếng Việt"
        };
    }
}
