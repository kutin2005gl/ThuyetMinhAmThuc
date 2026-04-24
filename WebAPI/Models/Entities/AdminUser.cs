namespace WebAPI.Models.Entities;

public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "Staff";
    public int? PoiId { get; set; }
    public DateTime CreatedAt { get; set; } = new DateTime(2024, 1, 1);
    public bool IsActive { get; set; } = true;
}



