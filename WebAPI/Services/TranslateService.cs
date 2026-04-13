using GTranslate.Translators;

namespace WebAPI.Services;

public class TranslateService
{
    private readonly GoogleTranslator _translator = new();

    public async Task<string> TranslateAsync(string text, string fromLang, string toLang)
    {
        try
        {
            // Fix lại mã ngôn ngữ cho đúng chuẩn GTranslate
            string targetLang = toLang.ToLower() switch
            {
                "zh" => "zh-CN",
                _ => toLang
            };

            var result = await _translator.TranslateAsync(text, targetLang, fromLang);
            return result.Translation;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Lỗi Dịch]: {ex.Message}");
            return text; // Trả về text gốc nếu lỗi
        }
    }
}