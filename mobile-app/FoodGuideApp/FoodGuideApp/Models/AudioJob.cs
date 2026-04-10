public class AudioJob
{
    public int PoiId { get; set; }
    public string Language { get; set; } = "vi";
    public string Text { get; set; } = "";
    public int Priority { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}