using FoodGuideApp.Services;

namespace FoodGuideApp;

public partial class App : Application
{
    private readonly AppShell _shell;

    // Công dụng: nhận MainPage từ Dependency Injection
    // để MainPage có thể dùng các service đã đăng ký trong MauiProgram.
    public App(AppShell shell)
    {
        InitializeComponent();
        _shell = shell;
        MainPage = shell;
        _ = HandleInitialUriAsync();
    }

    protected override async void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        await HandlePoiLinkAsync(uri);
    }

    private async Task HandleInitialUriAsync()
    {
        if (Current == null || string.IsNullOrWhiteSpace(Environment.GetCommandLineArgs().LastOrDefault()))
        {
            return;
        }

        var possibleUri = Environment.GetCommandLineArgs().LastOrDefault();
        if (Uri.TryCreate(possibleUri, UriKind.Absolute, out var uri))
        {
            await HandlePoiLinkAsync(uri);
        }
    }

    private async Task HandlePoiLinkAsync(Uri uri)
    {
        if (!TryParsePoiId(uri, out var poiId))
        {
            return;
        }

        var loaded = await PoiNavigationService.LoadPoiToPreferencesAsync(poiId);
        if (!loaded)
        {
            return;
        }

        await _shell.GoToAsync("//pois");
        await _shell.Navigation.PushAsync(new PoiInfoPage());
    }

    private static bool TryParsePoiId(Uri uri, out int poiId)
    {
        poiId = 0;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments.Length == 2
            && segments[0].Equals("poi", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(segments[1], out poiId);
    }
}
