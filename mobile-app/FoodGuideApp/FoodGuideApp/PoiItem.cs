namespace FoodGuideApp;

public class PoiItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusMeters { get; set; }
    public List<TranslationItem> Translations { get; set; } = new();
}

public class TranslationItem
{
    public string Language { get; set; } = "";
    public string Text { get; set; } = "";
}