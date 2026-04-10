namespace FoodGuideApp;

public partial class App : Application
{
    // Công dụng: nhận MainPage từ Dependency Injection
    // để MainPage có thể dùng các service đã đăng ký trong MauiProgram.
    public App(AppShell shell)
    {
        InitializeComponent();
        MainPage = shell;
    }
}