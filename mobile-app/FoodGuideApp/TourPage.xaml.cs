using FoodGuideApp.Models;
using FoodGuideApp.Services;

namespace FoodGuideApp;

public partial class TourPage : ContentPage
{
    private readonly TourApiService _tourApiService = new();
    private List<Tour> tours = new();

    public TourPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        pageTitleLabel.Text = LanguageManager.Get(
            "Danh sách tour",
            "Tours",
            "旅游列表",
            "투어 목록",
            "ツアー一覧",
            "Liste des circuits"
        );

        Title = pageTitleLabel.Text;

        await LoadToursAsync();
    }

    private async Task LoadToursAsync()
    {
        try
        {
            tours = await _tourApiService.GetToursAsync();
            tourCollectionView.ItemsSource = null;
            tourCollectionView.ItemsSource = tours;
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                LanguageManager.Get("Lỗi", "Error", "错误", "오류", "エラー", "Erreur"),
                ex.Message,
                "OK");
        }
    }

    private async void OnTourSelected(object sender, SelectionChangedEventArgs e)
    {
        var selectedTour = e.CurrentSelection.FirstOrDefault() as Tour;
        if (selectedTour == null)
            return;

        ((CollectionView)sender).SelectedItem = null;

        await Navigation.PushAsync(new TourDetailPage(selectedTour));
    }
}