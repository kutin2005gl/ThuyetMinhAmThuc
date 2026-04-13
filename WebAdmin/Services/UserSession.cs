namespace WebAdmin.Services;

public class UserSession
{
    public string? Username { get; set; }
    public string? FullName { get; set; }
    public string? Role { get; set; }
    public int? PoiId { get; set; }

    // Kiểm tra trạng thái đăng nhập
    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(Username);

    // THÊM DÒNG NÀY: Kiểm tra xem có phải Admin không
    public bool IsAdmin => Role == "Admin";

    // Sự kiện thông báo thay đổi để NavMenu tự cập nhật
    public event Action? OnChange;

    public void NotifyStateChanged() => OnChange?.Invoke();

    public void Logout()
    {
        Username = null;
        FullName = null;
        Role = null;
        PoiId = null;
        NotifyStateChanged();
    }
}