namespace FoodGuideApp.Models;

public class Poi
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // bán kính kích hoạt (mét)
    public double RadiusMeters { get; set; }
    public double NearRadiusMeters { get; set; } = 80;
    // mức ưu tiên, số lớn hơn => ưu tiên hơn
    public int Priority { get; set; } = 1;

    public bool IsActive { get; set; } = true;
    public List<Translation> Translations { get; set; } = new();

}