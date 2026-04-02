namespace WebAPI.Models.Entities;

public class Translation
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string Language { get; set; } = "vi";
    public string Text { get; set; } = "";

    public Poi? Poi { get; set; }
}