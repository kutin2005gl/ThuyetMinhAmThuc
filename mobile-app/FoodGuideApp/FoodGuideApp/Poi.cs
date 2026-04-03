public class Translation
{
    public string Language { get; set; }
    public string Text { get; set; }
}

public class Poi
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusMeters { get; set; }
    public List<Translation> Translations { get; set; }
}