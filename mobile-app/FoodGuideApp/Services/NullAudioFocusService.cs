namespace FoodGuideApp.Services;

// Công dụng: service dự phòng cho platform không phải Android.
// App vẫn chạy bình thường nhưng không có xử lý audio focus hệ điều hành.
public class NullAudioFocusService : IAudioFocusService
{
    public event Action? FocusLost
    {
        add { }
        remove { }
    }

    public bool RequestFocus() => true;

    public void AbandonFocus()
    {
    }
}