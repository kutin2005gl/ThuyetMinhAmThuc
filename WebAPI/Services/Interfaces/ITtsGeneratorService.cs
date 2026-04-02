namespace WebAPI.Services.Interfaces;

public interface ITtsGeneratorService
{
    Task<string> GenerateAsync(string poiId, string text, string language);
}