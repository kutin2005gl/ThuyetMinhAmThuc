using FoodGuideApp.Services;

namespace FoodGuideApp;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        var mainPage = serviceProvider.GetRequiredService<MainPage>();

        var tabBar = new TabBar();

        tabBar.Items.Add(new ShellContent
        {
            Title = LanguageManager.Get(
                "Trang chủ",
                "Home",
                "首页",
                "홈",
                "ホーム",
                "Accueil"
            ),
            Route = "home",
            Content = mainPage
        });

        tabBar.Items.Add(new ShellContent
        {
            Title = LanguageManager.Get(
                "Tour",
                "Tours",
                "旅游",
                "투어",
                "ツアー",
                "Circuits"
            ),
            Route = "tours",
            Content = new TourPage()
        });

        tabBar.Items.Add(new ShellContent
        {
            Title = LanguageManager.Get(
                "Địa điểm",
                "POIs",
                "地点",
                "장소",
                "場所",
                "Lieux"
            ),
            Route = "pois",
            Content = new PoiListPage()
        });

        tabBar.Items.Add(new ShellContent
        {
            Title = LanguageManager.Get(
                "Cài đặt",
                "Settings",
                "设置",
                "설정",
                "設定",
                "Paramètres"
            ),
            Route = "settings",
            Content = new SettingsPage()
        });

        Items.Add(tabBar);
    }
}