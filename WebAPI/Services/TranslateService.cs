using GTranslate.Translators;

namespace WebAPI.Services;

public class TranslateService
{
    private readonly GoogleTranslator _translator = new();
    private readonly ILogger<TranslateService> _logger;

    public TranslateService(ILogger<TranslateService> logger)
    {
        _logger = logger;
    }

    // Công dụng: dịch nội dung thuyết minh sang ngôn ngữ đích, fallback về text gốc nếu dịch lỗi.
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
            _logger.LogWarning(ex, "Translation service failed from {SourceLanguage} to {TargetLanguage}", fromLang, toLang);
            return text; // Trả về text gốc nếu lỗi
        }
    }
}
