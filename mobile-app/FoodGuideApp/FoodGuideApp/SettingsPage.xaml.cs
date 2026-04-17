using FoodGuideApp.Services;
using Microsoft.Maui.Storage;

namespace FoodGuideApp;

public partial class SettingsPage : ContentPage
{
    private string selectedLanguage = "vi";

    public SettingsPage()
    {
        InitializeComponent();
        LoadSettings();
        ApplyLanguageToUI();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadSettings();
        ApplyLanguageToUI();
    }

    private void LoadSettings()
    {
        selectedLanguage = Preferences.Get("app_language", "vi");

        double savedRadius = Preferences.Get("geofence_radius", 30.0);
        if (savedRadius <= 0)
            savedRadius = 30.0;

        radiusEntry.Text = savedRadius.ToString("0");

        viRadioButton.IsChecked = selectedLanguage == "vi";
        enRadioButton.IsChecked = selectedLanguage == "en";
        zhRadioButton.IsChecked = selectedLanguage == "zh";
        koRadioButton.IsChecked = selectedLanguage == "ko";
        jaRadioButton.IsChecked = selectedLanguage == "ja";
        frRadioButton.IsChecked = selectedLanguage == "fr";
    }

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
            "⚙️ Cài đặt",
            "⚙️ Settings",
            "⚙️ 设置",
            "⚙️ 설정",
            "⚙️ 設定",
            "⚙️ Paramètres"
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
            "Nhập khoảng cách kích hoạt thuyết minh khi đến gần POI",
            "Enter the distance to trigger narration when approaching a POI",
            "输入接近 POI 时触发语音讲解的距离",
            "POI에 가까워졌을 때 음성 안내를 시작할 거리를 입력하세요",
            "POIに近づいたときに案内を開始する距離を入力してください",
            "Entrez la distance pour déclencher la narration à l’approche d’un POI"
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
            "Gợi ý: 20–40m ngoài trời, 10–20m khu vực đông/nhỏ",
            "Suggested: 20–40m outdoors, 10–20m in dense/small areas",
            "建议：室外20–40米，小区域10–20米",
            "권장: 실외 20–40m, 좁은 공간 10–20m",
            "推奨: 屋外20〜40m、小さい場所10〜20m",
            "Conseil : 20–40 m extérieur, 10–20 m zones denses"
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

    private void OnLanguageChecked(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value)
            return;

        if (sender is RadioButton radio && radio.Value != null)
        {
            selectedLanguage = radio.Value.ToString() ?? "vi";
        }
    }

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

        statusLabel.Text = LanguageManager.Get(
            $"Đã lưu: ngôn ngữ = {selectedLanguage}, bán kính = {radius:0}m",
            $"Saved: language = {selectedLanguage}, radius = {radius:0}m",
            $"已保存：语言 = {selectedLanguage}，半径 = {radius:0}m",
            $"저장됨: 언어 = {selectedLanguage}, 반경 = {radius:0}m",
            $"保存済み: 言語 = {selectedLanguage}, 半径 = {radius:0}m",
            $"Enregistré : langue = {selectedLanguage}, rayon = {radius:0}m"
        );
    }
}