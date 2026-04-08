namespace WebAPI.Models.Entities;

public class Tour
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = new DateTime(2024, 1, 1);

    public ICollection<TourPoi> TourPois { get; set; } = new List<TourPoi>();
}