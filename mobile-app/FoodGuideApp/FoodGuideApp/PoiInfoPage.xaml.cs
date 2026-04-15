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
        LoadPoiInfo();
        await SpeakCurrentPoiAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopSpeaking();
    }

    private void LoadPoiInfo()
    {
        string name = Preferences.Get("poi_name", "Chưa có POI");
        string description = Preferences.Get("poi_description", "Mô tả POI sẽ hiển thị ở đây");
        string imageUrl = Preferences.Get("poi_image_url", "");
        string distance = Preferences.Get("poi_distance", "--");

        poiNameLabel.Text = name;
        poiDescriptionLabel.Text = description;
        poiDistanceLabel.Text = $"Khoảng cách: {distance} m";

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
            audioStatusLabel.Text = "Không có nội dung để phát";
            return;
        }

        if (isSpeaking)
            return;

        try
        {
            isSpeaking = true;
            speechCts?.Cancel();
            speechCts = new CancellationTokenSource();

            audioStatusLabel.Text = "Đang phát audio...";

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

            audioStatusLabel.Text = "Đã phát xong";
        }
        catch (OperationCanceledException)
        {
            audioStatusLabel.Text = "Đã dừng audio";
        }
        catch (Exception ex)
        {
            audioStatusLabel.Text = $"Lỗi audio: {ex.Message}";
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
        audioStatusLabel.Text = "Đã dừng audio";
    }
}