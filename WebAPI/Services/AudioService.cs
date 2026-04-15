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

    public async Task<string> GenerateSpeech(int poiId, string text, string languageCode, string fileName, string voiceName)
    {
        try
        {
            string filePath = Path.Combine(_storagePath, fileName);

            // 1. XÓA FILE CŨ: Đảm bảo giọng đọc cũ bị loại bỏ hoàn toàn trên Server
            if (File.Exists(filePath)) File.Delete(filePath);

            var client = _httpClientFactory.CreateClient();

            // 2. TẠO URL: Link này sẽ lấy giọng mặc định theo mã ngôn ngữ
            // Lưu ý: Nếu muốn dùng đúng giọng Standard-A/B, bạn cần dùng Google Cloud TTS API chính thức
            string url = $"https://translate.google.com/translate_tts?ie=UTF-8&q={Uri.EscapeDataString(text)}&tl={languageCode}&client=tw-ob";

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                // 3. GHI FILE MỚI
                await File.WriteAllBytesAsync(filePath, bytes);
                return $"/audio/{fileName}";
            }
            return "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TTS Error: {ex.Message}");
            return "";
        }
    }
}