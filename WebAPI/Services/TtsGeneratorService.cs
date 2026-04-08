using Microsoft.CognitiveServices.Speech;
using WebAPI.Services.Interfaces;

namespace WebAPI.Services;

public class TtsGeneratorService : ITtsGeneratorService
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public TtsGeneratorService(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    public async Task<string> GenerateAsync(string poiId, string text, string language)
    {
        var fileName = $"poi_{poiId}_{language}.mp3";
        var audioFolder = Path.Combine(_env.WebRootPath, "audio");
        Directory.CreateDirectory(audioFolder);
        var filePath = Path.Combine(audioFolder, fileName);

        if (File.Exists(filePath))
            return $"/audio/{fileName}";

        var config = SpeechConfig.FromSubscription(
            _config["AzureTts:Key"] ?? "",
            _config["AzureTts:Region"] ?? "");

        config.SpeechSynthesisVoiceName = GetVoiceName(language);

        using var synthesizer = new SpeechSynthesizer(config, null);
        var result = await synthesizer.SpeakTextAsync(text);

        await File.WriteAllBytesAsync(filePath, result.AudioData);
        return $"/audio/{fileName}";
    }

    private static string GetVoiceName(string lang) => lang switch
    {
        "vi" => "vi-VN-HoaiMyNeural",
        "en" => "en-US-JennyNeural",
        "zh" => "zh-CN-XiaoxiaoNeural",
        _    => "vi-VN-HoaiMyNeural"
    };
}