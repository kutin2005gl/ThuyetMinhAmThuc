namespace FoodGuideApp.Services;

// Công dụng: định nghĩa service xin quyền audio focus của hệ thống.
// Khi app phát TTS, service này sẽ xin focus;
// khi mất focus do audio khác chen vào thì phát sự kiện để dừng audio hiện tại.
public interface IAudioFocusService
{
    event Action? FocusLost;

    bool RequestFocus();

    void AbandonFocus();
}