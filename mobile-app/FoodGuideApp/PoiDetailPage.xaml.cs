using Microsoft.Maui.Controls;
using FoodGuideApp.Services;

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
        Title = LanguageManager.Get(
            "Chi tiết POI",
            "POI Details",
            "POI详情",
            "POI 상세정보",
            "POI詳細",
            "Détails du POI"
        );
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
                ? LanguageManager.Get(
                    "Không có mô tả",
                    "No description",
                    "没有描述",
                    "설명이 없습니다",
                    "説明がありません",
                    "Aucune description"
                )
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
        set
        {
            var distance = Uri.UnescapeDataString(value ?? "--");

            if (string.IsNullOrWhiteSpace(distance) ||
                distance == "--" ||
                distance == "0" ||
                distance == "0.0" ||
                distance == "0.00")
            {
                distanceLabel.Text = LanguageManager.Get(
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
                distanceLabel.Text = LanguageManager.Get(
                    $"Khoảng cách: {distance} m",
                    $"Distance: {distance} m",
                    $"距离：{distance} 米",
                    $"거리: {distance} m",
                    $"距離: {distance} m",
                    $"Distance : {distance} m"
                );
            }
        }
    }
}