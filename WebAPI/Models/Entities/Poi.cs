namespace WebAPI.Models.Entities;

public class Poi
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? ImagePath { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusMeters { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Translation> Translations { get; set; } = new List<Translation>();
    public ICollection<AudioFile> AudioFiles { get; set; } = new List<AudioFile>();
}