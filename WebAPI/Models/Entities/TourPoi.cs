namespace WebAPI.Models.Entities;

public class TourPoi
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public int PoiId { get; set; }
    public int Order { get; set; } // thứ tự trong tour

    public Tour? Tour { get; set; }
    public Poi? Poi { get; set; }
}