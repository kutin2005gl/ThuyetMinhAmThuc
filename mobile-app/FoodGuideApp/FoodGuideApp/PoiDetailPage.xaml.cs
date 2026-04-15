using Microsoft.Maui.Controls;

namespace FoodGuideApp;

[QueryProperty(nameof(PoiName), "name")]
[QueryProperty(nameof(PoiDescription), "description")]
[QueryProperty(nameof(PoiImageUrl), "imageUrl")]
[QueryProperty(nameof(DistanceText), "distance")]
public partial class PoiDetailPage : ContentPage
{
    public PoiDetailPage()
    {
        InitializeComponent();
    }

    public string PoiName
    {
        set => titleLabel.Text = Uri.UnescapeDataString(value ?? "");
    }

    public string PoiDescription
    {
        set
        {
            var text = Uri.UnescapeDataString(value ?? "");
            descriptionLabel.Text = string.IsNullOrWhiteSpace(text)
                ? "Không có mô tả"
                : text;
        }
    }

    public string PoiImageUrl
    {
        set
        {
            var url = Uri.UnescapeDataString(value ?? "");

            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    poiImage.Source = ImageSource.FromUri(new Uri(url));
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
    }

    public string DistanceText
    {
        set => distanceLabel.Text = $"Khoảng cách: {Uri.UnescapeDataString(value ?? "--")} m";
    }
}