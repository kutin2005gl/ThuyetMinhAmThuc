namespace WebAdmin.Services;

public class UserSession
{
    public string? Username { get; set; }
    public string? FullName { get; set; }
    public string? Role { get; set; }
    public int? PoiId { get; set; }
    public bool IsLoggedIn => Username != null;
    public bool IsAdmin => Role == "Admin";
}