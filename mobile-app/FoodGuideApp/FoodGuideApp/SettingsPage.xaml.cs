namespace FoodGuideApp;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        string savedLanguage = Preferences.Get("app_language", "vi");
        double savedRadius = Preferences.Get("geofence_radius", 30.0);

        languagePicker.SelectedItem = savedLanguage;
        radiusEntry.Text = savedRadius.ToString();
    }

    private async void OnSaveSettingsClicked(object sender, EventArgs e)
    {
        string selectedLanguage = languagePicker.SelectedItem?.ToString() ?? "vi";

        if (!double.TryParse(radiusEntry.Text, out double radius) || radius <= 0)
        {
            await DisplayAlert("Lỗi", "Bán kính không hợp lệ", "OK");
            return;
        }

        Preferences.Set("app_language", selectedLanguage);
        Preferences.Set("geofence_radius", radius);

        statusLabel.Text = $"Đã lưu: ngôn ngữ = {selectedLanguage}, bán kính = {radius}m";
    }
}