using Microsoft.Maui.Storage;

namespace FoodGuideApp.Services;

public static class GuestSessionService
{
    private const string GuestSessionKey = "guest_session_id";

    public static string GetOrCreateSessionId()
    {
        var existing = Preferences.Get(GuestSessionKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var created = Guid.NewGuid().ToString("N");
        Preferences.Set(GuestSessionKey, created);
        return created;
    }

    public static void AttachTo(HttpClient client)
    {
        var sessionId = GetOrCreateSessionId();
        client.DefaultRequestHeaders.Remove(AppConfig.GuestSessionHeader);
        client.DefaultRequestHeaders.Add(AppConfig.GuestSessionHeader, sessionId);
    }
}
