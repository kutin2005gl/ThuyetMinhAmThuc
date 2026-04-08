using WebAdmin.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<UserSession>();

// Kết nối tới WebAPI
builder.Services.AddHttpClient("WebAPI", client =>
{
    client.BaseAddress = new Uri("http://localhost:5000");
});

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<WebAdmin.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();