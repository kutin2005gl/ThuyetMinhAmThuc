using System.Net.Http;

namespace WebAPI.Services;

public class AudioService
{
    private readonly string _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "audio");
    private readonly IHttpClientFactory _httpClientFactory;

    public AudioService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public async Task<string> GenerateSpeech(int poiId, string text, string languageCode, string fileName)
    {
        try
        {
            string filePath = Path.Combine(_storagePath, fileName);

            var client = _httpClientFactory.CreateClient();

            // QUAN TRỌNG: Google Translate TTS chỉ hiểu mã ngắn (vi, en, fr, zh, ko, ja)
            // Nếu bạn để mã là 'eng-us' nó sẽ lỗi. Ta cần xử lý chuỗi lấy 2 chữ cái đầu nếu cần.
            string shortLang = languageCode.Split('-')[0].ToLower();

            string url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={Uri.EscapeDataString(text)}&tl={shortLang}&client=tw-ob";

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(filePath, bytes);
                return $"/audio/{fileName}";
            }

            Console.WriteLine($"--- [TTS Error]: Google returned {response.StatusCode} for lang {shortLang} ---");
            return "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"--- [TTS Error]: {ex.Message} ---");
            return "";
        }
    }
}