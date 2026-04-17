using FoodGuideApp.Services;
using Microsoft.Maui.Storage;

namespace FoodGuideApp;

public partial class PoiInfoPage : ContentPage
{
    private CancellationTokenSource? speechCts;
    private bool isSpeaking = false;

    public PoiInfoPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyLanguageToButtons();
        LoadPoiInfo();
        await SpeakCurrentPoiAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopSpeaking();
    }

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

    private void LoadPoiInfo()
    {
        string name = Preferences.Get("poi_name", "Chưa có POI");
        string description = Preferences.Get("poi_description", "Mô tả POI sẽ hiển thị ở đây");
        string imageUrl = Preferences.Get("poi_image_url", "");
        string distance = Preferences.Get("poi_distance", "--");

        poiNameLabel.Text = name;
        poiDescriptionLabel.Text = description;

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
            try
            {
                poiImage.Source = ImageSource.FromUri(new Uri(imageUrl));
                poiImage.IsVisible = true;
            }
            catch
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

    private async Task SpeakCurrentPoiAsync()
    {
        string text = Preferences.Get("poi_description", "");
        string language = Preferences.Get("app_language", "vi");

        if (string.IsNullOrWhiteSpace(text))
        {
            audioStatusLabel.Text = LanguageManager.Get(
                "Không có nội dung để phát",
                "No content to play",
                "没有可播放内容",
                "재생할 내용이 없습니다",
                "再生する内容がありません",
                "Aucun contenu à lire"
            );
            return;
        }

        if (isSpeaking)
            return;

        try
        {
            isSpeaking = true;
            speechCts?.Cancel();
            speechCts = new CancellationTokenSource();

            audioStatusLabel.Text = LanguageManager.Get(
                "Đang phát audio...",
                "Playing audio...",
                "正在播放音频...",
                "오디오 재생 중...",
                "音声を再生中...",
                "Lecture en cours..."
            );

            var locales = await TextToSpeech.Default.GetLocalesAsync();
            Locale? locale = null;

            if (locales != null && locales.Any())
            {
                locale = locales.FirstOrDefault(l =>
                    !string.IsNullOrWhiteSpace(l.Language) &&
                    l.Language.StartsWith(language, StringComparison.OrdinalIgnoreCase));

                if (locale == null && language.Contains("-"))
                {
                    string shortLang = language.Split('-')[0];
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
            }

            var options = new SpeechOptions
            {
                Locale = locale,
                Pitch = 1.0f,
                Volume = 1.0f
            };

            await TextToSpeech.Default.SpeakAsync(text, options, speechCts.Token);

            audioStatusLabel.Text = LanguageManager.Get(
                "Đã phát xong",
                "Finished playing",
                "播放完成",
                "재생 완료",
                "再生完了",
                "Lecture terminée"
            );
        }
        catch (OperationCanceledException)
        {
            audioStatusLabel.Text = LanguageManager.Get(
                "Đã dừng audio",
                "Audio stopped",
                "音频已停止",
                "오디오 중지됨",
                "音声停止",
                "Audio arrêté"
            );
        }
        catch (Exception ex)
        {
            audioStatusLabel.Text = LanguageManager.Get(
                "Lỗi audio",
                "Audio error",
                "音频错误",
                "오디오 오류",
                "音声エラー",
                "Erreur audio"
            ) + $": {ex.Message}";
        }
        finally
        {
            isSpeaking = false;
        }
    }

    private void StopSpeaking()
    {
        if (speechCts != null && !speechCts.IsCancellationRequested)
        {
            speechCts.Cancel();
        }

        isSpeaking = false;
    }

    private async void OnReplayClicked(object sender, EventArgs e)
    {
        StopSpeaking();
        await SpeakCurrentPoiAsync();
    }

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
}