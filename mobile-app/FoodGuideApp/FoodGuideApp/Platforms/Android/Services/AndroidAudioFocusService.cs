#if ANDROID
using Android.Content;
using Android.Media;
using Android.Runtime;

namespace FoodGuideApp.Services;

// Công dụng: xin quyền audio focus trên Android trước khi phát TTS.
// Nếu có âm thanh khác chen vào như thông báo, nhạc, cuộc gọi,
// service sẽ báo mất focus để app dừng audio hiện tại.
public class AndroidAudioFocusService : Java.Lang.Object, IAudioFocusService, AudioManager.IOnAudioFocusChangeListener
{
    private readonly AudioManager? _audioManager;

    public event Action? FocusLost;

    // Công dụng: khởi tạo AudioManager từ hệ thống Android.
    public AndroidAudioFocusService()
    {
        _audioManager = Android.App.Application.Context.GetSystemService(Context.AudioService) as AudioManager;
    }

    // Công dụng: xin audio focus trước khi app phát thuyết minh.
    // Trả về true nếu xin thành công, false nếu thất bại.
    public bool RequestFocus()
    {
        if (_audioManager == null)
            return false;

        var result = _audioManager.RequestAudioFocus(
            this,
            Android.Media.Stream.Music,
            AudioFocus.GainTransient);

        return result == AudioFocusRequest.Granted;
    }

    // Công dụng: trả lại audio focus cho hệ thống sau khi phát xong
    // hoặc khi người dùng dừng audio.
    public void AbandonFocus()
    {
        if (_audioManager == null)
            return;

        _audioManager.AbandonAudioFocus(this);
    }

    // Công dụng: nhận sự kiện thay đổi audio focus từ Android.
    // Nếu app mất focus thì phát sự kiện FocusLost để dừng audio hiện tại.
    public void OnAudioFocusChange([GeneratedEnum] AudioFocus focusChange)
    {
        switch (focusChange)
        {
            case AudioFocus.Loss:
            case AudioFocus.LossTransient:
            case AudioFocus.LossTransientCanDuck:
                FocusLost?.Invoke();
                break;
        }
    }
}
#endif