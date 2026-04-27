using System;
using System.Collections.Generic;
using System.Linq;

namespace FoodGuideApp.Models;

public class Poi
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? ImagePath { get; set; }
    public string? ImageUrl { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusMeters { get; set; } = 30;

    // bán kính cảnh báo gần, mobile tự tính thêm
    public double NearRadiusMeters { get; set; }

    // backend chưa trả field này, nên để mặc định 1 để app vẫn chạy
    public int Priority { get; set; } = 1;

    public List<Translation> Translations { get; set; } = new();
}