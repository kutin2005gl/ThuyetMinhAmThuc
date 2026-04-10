using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp.Views.Maui.Controls.Hosting;
using FoodGuideApp.Services;

namespace FoodGuideApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder.Services.AddSingleton<AppShell>();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
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

            builder.Services.AddSingleton<MainPage>();

            return builder.Build();
        }
    }
}