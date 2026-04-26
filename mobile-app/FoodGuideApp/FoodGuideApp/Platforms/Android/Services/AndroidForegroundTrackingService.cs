#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;

namespace FoodGuideApp.Services;

// Công dụng: bật/tắt Android ForegroundService khi người dùng chủ động theo dõi GPS.
public class AndroidForegroundTrackingService : IForegroundTrackingService
{
    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        var context = Android.App.Application.Context;
        var intent = new Intent(context, typeof(FoodGuideForegroundService));

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }

        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        var context = Android.App.Application.Context;
        var intent = new Intent(context, typeof(FoodGuideForegroundService));
        context.StopService(intent);

        IsRunning = false;
    }
}
#endif
