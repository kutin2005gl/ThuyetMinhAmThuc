namespace FoodGuideApp;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        // Công dụng: lấy MainPage từ DI thay vì new thủ công
        var mainPage = serviceProvider.GetService<MainPage>();

        Items.Add(new ShellContent
        {
            Title = "Home",
            Content = mainPage
        });

        Items.Add(new ShellContent
        {
            Title = "Settings",
            Content = new SettingsPage()
        });
    }
}