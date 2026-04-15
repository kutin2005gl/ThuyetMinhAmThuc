namespace FoodGuideApp;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        var mainPage = serviceProvider.GetService<MainPage>();

        var tabBar = new TabBar();

        tabBar.Items.Add(new ShellContent
        {
            Title = "Home",
            Route = "home",
            Content = mainPage
        });

        tabBar.Items.Add(new ShellContent
        {
            Title = "POI",
            Route = "poi",
            Content = new PoiInfoPage()
        });

        tabBar.Items.Add(new ShellContent
        {
            Title = "Settings",
            Route = "settings",
            Content = new SettingsPage()
        });

        Items.Add(tabBar);
    }
}