using FoodGuideApp.Services;
using Microsoft.Maui.Storage;

namespace FoodGuideApp;

public partial class SettingsPage : ContentPage
{
    private string selectedLanguage = "vi";
    private bool isSyncingRadius = false;

    // Công dụng: khởi tạo giao diện cài đặt và nạp cấu hình đã lưu.
    public SettingsPage()
    {
        InitializeComponent();
        LoadSettings();
        ApplyLanguageToUI();
    }

    // Công dụng: làm mới lựa chọn ngôn ngữ và bán kính mỗi khi mở lại trang cài đặt.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadSettings();
        ApplyLanguageToUI();
    }

    // Công dụng: đọc ngôn ngữ và bán kính geofence từ Preferences để đồng bộ UI.
    private void LoadSettings()
    {
        selectedLanguage = Preferences.Get("app_language", "vi");

        double savedRadius = Preferences.Get("geofence_radius", 30.0);
        if (savedRadius <= 0)
            savedRadius = 30.0;

        isSyncingRadius = true;
        radiusEntry.Text = savedRadius.ToString("0");
        radiusSlider.Value = Math.Clamp(savedRadius, radiusSlider.Minimum, radiusSlider.Maximum);
        radiusValueLabel.Text = $"{savedRadius:0} m";
        isSyncingRadius = false;

        viRadioButton.IsChecked = selectedLanguage == "vi";
        enRadioButton.IsChecked = selectedLanguage == "en";
        zhRadioButton.IsChecked = selectedLanguage == "zh";
        koRadioButton.IsChecked = selectedLanguage == "ko";
        jaRadioButton.IsChecked = selectedLanguage == "ja";
        frRadioButton.IsChecked = selectedLanguage == "fr";

        UpdateLanguageCards();
    }

    // Công dụng: áp dụng toàn bộ chữ trên trang Settings theo ngôn ngữ đang chọn.
    private void ApplyLanguageToUI()
    {
        Title = LanguageManager.Get(
            "Cài đặt",
            "Settings",
            "设置",
            "설정",
            "設定",
            "Paramètres"
        );

        titleLabel.Text = LanguageManager.Get(
            "Cài đặt",
            "Settings",
            "设置",
            "설정",
            "設定",
            "Paramètres"
        );

        subTitleLabel.Text = LanguageManager.Get(
            "Tùy chỉnh ngôn ngữ và bán kính geofence cho ứng dụng",
            "Customize language and geofence radius",
            "自定义语言和地理围栏半径",
            "언어 및 지오펜스 반경 설정",
            "言語とジオフェンス半径を設定",
            "Personnaliser la langue et le rayon"
        );

        languageTitleLabel.Text = LanguageManager.Get(
            "Ngôn ngữ",
            "Language",
            "语言",
            "언어",
            "言語",
            "Langue"
        );

        languageDescLabel.Text = LanguageManager.Get(
            "Chọn ngôn ngữ hiển thị / thuyết minh",
            "Choose display / narration language",
            "选择显示 / 讲解语言",
            "표시 / 음성 안내 언어 선택",
            "表示 / 音声案内の言語を選択",
            "Choisir la langue d'affichage / narration"
        );

        radiusTitleLabel.Text = LanguageManager.Get(
            "Bán kính geofence",
            "Geofence radius",
            "地理围栏半径",
            "지오펜스 반경",
            "ジオフェンス半径",
            "Rayon de géorepérage"
        );

        radiusHintLabel.Text = LanguageManager.Get(
            "Chỉnh nhẹ khoảng cách kích hoạt thuyết minh khi đến gần POI",
            "Fine-tune the distance that triggers narration near a POI",
            "微调接近 POI 时触发语音讲解的距离",
            "POI에 가까워졌을 때 음성 안내를 시작할 거리를 조정하세요",
            "POIに近づいたときに案内を開始する距離を調整します",
            "Ajuster la distance qui déclenche la narration près d'un POI"
        );

        radiusEntry.Placeholder = LanguageManager.Get(
            "Ví dụ: 30",
            "Example: 30",
            "例如：30",
            "예: 30",
            "例: 30",
            "Exemple : 30"
        );

        radiusSuggestLabel.Text = LanguageManager.Get(
            "Gợi ý: 20-40m ngoài trời, 10-20m khu vực đông/nhỏ",
            "Suggested: 20-40m outdoors, 10-20m in dense/small areas",
            "建议：室外20-40米，小区域10-20米",
            "권장: 실외 20-40m, 좁은 공간 10-20m",
            "推奨: 屋外20-40m、小さい場所10-20m",
            "Conseil : 20-40 m extérieur, 10-20 m zones denses"
        );

        saveButton.Text = LanguageManager.Get(
            "Lưu cài đặt",
            "Save settings",
            "保存设置",
            "설정 저장",
            "設定を保存",
            "Enregistrer les paramètres"
        );

        statusLabel.Text = LanguageManager.Get(
            "Chưa có thay đổi",
            "No changes yet",
            "尚未更改",
            "변경 사항 없음",
            "変更なし",
            "Aucun changement"
        );
    }

    // Công dụng: nhận thay đổi từ RadioButton và cập nhật lựa chọn ngôn ngữ hiện tại.
    private void OnLanguageChecked(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value)
            return;

        if (sender is RadioButton radio && radio.Value != null)
        {
            selectedLanguage = radio.Value.ToString() ?? "vi";
            UpdateLanguageCards();
        }
    }

    // Công dụng: cho phép chạm vào cả card ngôn ngữ để chọn nhanh.
    private void OnLanguageOptionTapped(object sender, TappedEventArgs e)
    {
        if (sender is TapGestureRecognizer tap && tap.CommandParameter is string language)
        {
            SelectLanguage(language);
        }
    }

    // Công dụng: đồng bộ radio và card khi người dùng chọn một ngôn ngữ.
    private void SelectLanguage(string language)
    {
        selectedLanguage = language;

        viRadioButton.IsChecked = selectedLanguage == "vi";
        enRadioButton.IsChecked = selectedLanguage == "en";
        zhRadioButton.IsChecked = selectedLanguage == "zh";
        koRadioButton.IsChecked = selectedLanguage == "ko";
        jaRadioButton.IsChecked = selectedLanguage == "ja";
        frRadioButton.IsChecked = selectedLanguage == "fr";

        UpdateLanguageCards();
    }

    // Công dụng: đồng bộ ô nhập bán kính khi kéo slider.
    private void OnRadiusSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (isSyncingRadius)
            return;

        double radius = Math.Round(e.NewValue);

        isSyncingRadius = true;
        radiusEntry.Text = radius.ToString("0");
        radiusValueLabel.Text = $"{radius:0} m";
        isSyncingRadius = false;
    }

    // Công dụng: đồng bộ slider khi người dùng nhập bán kính bằng bàn phím.
    private void OnRadiusEntryTextChanged(object sender, TextChangedEventArgs e)
    {
        if (isSyncingRadius)
            return;

        string radiusText = e.NewTextValue?.Trim() ?? "";
        if (!double.TryParse(radiusText, out double radius))
            return;

        radiusValueLabel.Text = $"{radius:0} m";

        if (radius < radiusSlider.Minimum || radius > radiusSlider.Maximum)
            return;

        isSyncingRadius = true;
        radiusSlider.Value = radius;
        isSyncingRadius = false;
    }

    // Công dụng: lưu ngôn ngữ và bán kính geofence vào Preferences.
    private async void OnSaveSettingsClicked(object sender, EventArgs e)
    {
        string radiusText = radiusEntry.Text?.Trim() ?? "";

        if (!double.TryParse(radiusText, out double radius))
        {
            await DisplayAlert(
                LanguageManager.Get("Lỗi", "Error", "错误", "오류", "エラー", "Erreur"),
                LanguageManager.Get(
                    "Vui lòng nhập số hợp lệ",
                    "Please enter a valid number",
                    "请输入有效数字",
                    "유효한 숫자를 입력하세요",
                    "有効な数値を入力してください",
                    "Veuillez entrer un nombre valide"
                ),
                "OK"
            );
            return;
        }

        if (radius < 5 || radius > 100)
        {
            await DisplayAlert(
                LanguageManager.Get("Thông báo", "Notice", "通知", "알림", "お知らせ", "Notification"),
                LanguageManager.Get(
                    "Nên nhập bán kính từ 5 đến 100 mét",
                    "Radius should be between 5 and 100 meters",
                    "半径应在 5 到 100 米之间",
                    "반경은 5~100미터 사이여야 합니다",
                    "半径は5〜100メートルにしてください",
                    "Le rayon doit être compris entre 5 et 100 mètres"
                ),
                "OK"
            );
            return;
        }

        Preferences.Set("app_language", selectedLanguage);
        Preferences.Set("geofence_radius", radius);

        ApplyLanguageToUI();
        UpdateLanguageCards();

        statusLabel.Text = LanguageManager.Get(
            $"Đã lưu: ngôn ngữ = {selectedLanguage}, bán kính = {radius:0}m",
            $"Saved: language = {selectedLanguage}, radius = {radius:0}m",
            $"已保存：语言 = {selectedLanguage}，半径 = {radius:0}m",
            $"저장됨: 언어 = {selectedLanguage}, 반경 = {radius:0}m",
            $"保存済み: 言語 = {selectedLanguage}, 半径 = {radius:0}m",
            $"Enregistré : langue = {selectedLanguage}, rayon = {radius:0}m"
        );
    }

    // Công dụng: cập nhật màu viền/nền để thấy rõ ngôn ngữ đang được chọn.
    private void UpdateLanguageCards()
    {
        ApplyLanguageCardStyle(langViBorder, selectedLanguage == "vi");
        ApplyLanguageCardStyle(langEnBorder, selectedLanguage == "en");
        ApplyLanguageCardStyle(langZhBorder, selectedLanguage == "zh");
        ApplyLanguageCardStyle(langKoBorder, selectedLanguage == "ko");
        ApplyLanguageCardStyle(langJaBorder, selectedLanguage == "ja");
        ApplyLanguageCardStyle(langFrBorder, selectedLanguage == "fr");
    }

    // Công dụng: áp dụng style cho từng card ngôn ngữ theo trạng thái chọn/bỏ chọn.
    private void ApplyLanguageCardStyle(Border border, bool isSelected)
    {
        border.BackgroundColor = Color.FromArgb(isSelected ? "#ECFDF5" : "#FFFFFF");
        border.Stroke = new SolidColorBrush(Color.FromArgb(isSelected ? "#0F766E" : "#CBD5E1"));
        border.StrokeThickness = isSelected ? 2 : 1;
    }
}
