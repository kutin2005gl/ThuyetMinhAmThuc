namespace WebAPI.Models.Entities;

public class AudioFile
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string Language { get; set; } = "vi";
    public string FilePath { get; set; } = "";
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    public Poi? Poi { get; set; }
}