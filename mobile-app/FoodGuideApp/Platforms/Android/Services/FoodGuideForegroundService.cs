#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace FoodGuideApp.Services;

[Service(
    Name = "com.companyname.foodguideapp.FoodGuideForegroundService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeLocation)]
public class FoodGuideForegroundService : Service
{
    private const int NotificationId = 1001;
    private const string ChannelId = "foodguide_tracking";
    private const string ChannelName = "FoodGuide GPS tracking";

    public override IBinder? OnBind(Intent? intent) => null;

    // Công dụng: tạo notification channel cần thiết cho Android 8+ trước khi chạy foreground service.
    public override void OnCreate()
    {
        base.OnCreate();
        EnsureNotificationChannel();
    }

    // Công dụng: hiển thị notification cố định để phiên theo dõi GPS foreground ít bị hệ điều hành dừng.
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForeground(NotificationId, BuildNotification());
        return StartCommandResult.Sticky;
    }

    // Công dụng: tạo notification nhẹ, không phát âm, dùng để báo app đang theo dõi vị trí.
    private Notification BuildNotification()
    {
        var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName ?? string.Empty);
        var pendingIntent = launchIntent == null
            ? null
            : PendingIntent.GetActivity(
                this,
                0,
                launchIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(this, ChannelId)
            : new Notification.Builder(this);

        builder
            .SetContentTitle("FoodGuide đang theo dõi vị trí")
            .SetContentText("GPS đang bật để phát thuyết minh đúng địa điểm.")
            .SetSmallIcon(global::FoodGuideApp.Resource.Mipmap.appicon)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .SetCategory(Notification.CategoryService);

        if (pendingIntent != null)
        {
            builder.SetContentIntent(pendingIntent);
        }

        return builder.Build();
    }

    // Công dụng: đảm bảo Android 8+ có notification channel mức thấp cho foreground tracking.
    private void EnsureNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            return;
        }

        var notificationManager = GetSystemService(NotificationService) as NotificationManager;
        if (notificationManager == null)
        {
            return;
        }

        var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.Low)
        {
            Description = "Thông báo khi FoodGuide đang theo dõi vị trí để kích hoạt thuyết minh."
        };

        notificationManager.CreateNotificationChannel(channel);
    }
}
#endif
