namespace FoodGuideApp.Services;

// Công dụng: định nghĩa lớp điều khiển foreground tracking để Android giữ phiên theo dõi ổn định khi app còn đang tracking.
public interface IForegroundTrackingService
{
    bool IsRunning { get; }

    void Start();

    void Stop();
}
