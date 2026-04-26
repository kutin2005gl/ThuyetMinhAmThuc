namespace FoodGuideApp.Services;

// Công dụng: service dự phòng cho platform không hỗ trợ Android ForegroundService.
public class NullForegroundTrackingService : IForegroundTrackingService
{
    public bool IsRunning { get; private set; }

    public void Start()
    {
        IsRunning = true;
    }

    public void Stop()
    {
        IsRunning = false;
    }
}
