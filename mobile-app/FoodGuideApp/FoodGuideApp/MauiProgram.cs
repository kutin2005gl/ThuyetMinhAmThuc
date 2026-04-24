using Microsoft.Extensions.Logging;
using FoodGuideApp.Services;
using SkiaSharp.Views.Maui.Controls.Hosting;
using ZXing.Net.Maui.Controls;

namespace FoodGuideApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

#if ANDROID
            builder.Services.AddSingleton<IAudioFocusService, AndroidAudioFocusService>();
#else
            builder.Services.AddSingleton<IAudioFocusService, NullAudioFocusService>();
#endif

            builder.Services.AddSingleton<AppShell>();
            builder.Services.AddSingleton<MainPage>();

            builder.Services.AddTransient<QrScannerPage>();
            builder.Services.AddSingleton<PoiService>();
            builder.Services.AddSingleton<TourApiService>();
            builder.Services.AddSingleton<UsageTrackingService>();

            return builder.Build();
        }
    }
}