using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Maui.Media;

namespace FoodGuideApp.Services;

// Công dụng: quản lý hàng chờ audio của app.
// - Phát audio tuần tự, không chồng nhau
// - Chống phát trùng trong một khoảng thời gian ngắn
// - Xin audio focus trước khi phát
// - Tự dừng audio hiện tại khi hệ thống báo mất audio focus
public class AudioQueueManager
{
    private readonly Channel<AudioJob> _channel;
    private readonly ConcurrentDictionary<string, DateTime> _recentPlayed;
    private readonly TimeSpan _duplicateWindow = TimeSpan.FromSeconds(15);

    private readonly IAudioFocusService _audioFocusService;

    private CancellationTokenSource? _currentSpeakCts;
    private bool _isProcessing = false;
    private readonly object _lock = new();

    // Công dụng: khởi tạo queue audio, bộ nhớ chống trùng,
    // và gắn sự kiện mất audio focus để dừng audio hiện tại.
    public AudioQueueManager(IAudioFocusService audioFocusService)
    {
        _channel = Channel.CreateUnbounded<AudioJob>();
        _recentPlayed = new ConcurrentDictionary<string, DateTime>();
        _audioFocusService = audioFocusService;

        _audioFocusService.FocusLost += StopCurrent;
    }

    // Công dụng: khởi động worker nền để đọc queue và phát audio tuần tự.
    // Chỉ chạy 1 lần.
    public void Start()
    {
        lock (_lock)
        {
            if (_isProcessing) return;

            _isProcessing = true;
            _ = Task.Run(ProcessQueueAsync);
        }
    }

    // Công dụng: thêm 1 audio job vào hàng chờ.
    // Nếu cùng POI + ngôn ngữ vừa phát trong thời gian ngắn thì bỏ qua.
    public async Task EnqueueAsync(AudioJob job)
    {
        if (job == null || string.IsNullOrWhiteSpace(job.Text))
            return;

        string key = $"{job.PoiId}_{job.Language}";

        if (_recentPlayed.TryGetValue(key, out var lastTime))
        {
            if (DateTime.UtcNow - lastTime < _duplicateWindow)
            {
                System.Diagnostics.Debug.WriteLine($"[AUDIO] Bỏ qua audio trùng: {key}");
                return;
            }
        }

        await _channel.Writer.WriteAsync(job);
        System.Diagnostics.Debug.WriteLine($"[AUDIO] Enqueued: {job.Text}");
    }

    // Công dụng: lấy từng job từ queue và phát bằng Text-to-Speech.
    // Trước khi phát sẽ xin audio focus, phát xong sẽ nhả focus.
    private async Task ProcessQueueAsync()
    {
        await foreach (var job in _channel.Reader.ReadAllAsync())
        {
            try
            {
                string key = $"{job.PoiId}_{job.Language}";

                _currentSpeakCts?.Dispose();
                _currentSpeakCts = new CancellationTokenSource();

                bool focusGranted = _audioFocusService.RequestFocus();
                if (!focusGranted)
                {
                    System.Diagnostics.Debug.WriteLine("[AUDIO] Không xin được audio focus");
                    continue;
                }

                var locale = await ResolveLocaleAsync(job.Language);

                var options = new SpeechOptions
                {
                    Locale = locale,
                    Pitch = 1.0f,
                    Volume = 1.0f
                };

                _recentPlayed[key] = DateTime.UtcNow;

                System.Diagnostics.Debug.WriteLine(
                    $"[AUDIO] Playing | PoiId={job.PoiId} | Lang={job.Language} | Text={job.Text}");

                await TextToSpeech.Default.SpeakAsync(
                    job.Text,
                    options,
                    _currentSpeakCts.Token
                );

                System.Diagnostics.Debug.WriteLine($"[AUDIO] Done: {key}");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[AUDIO] Audio bị hủy");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AUDIO ERROR] {ex}");
            }
            finally
            {
                _audioFocusService.AbandonFocus();
            }
        }
    }

    // Công dụng: tìm locale TTS phù hợp với mã ngôn ngữ truyền vào.
    // Nếu không tìm thấy, fallback về tiếng Việt hoặc locale đầu tiên có sẵn.
    private async Task<Locale?> ResolveLocaleAsync(string languageCode)
    {
        var locales = await TextToSpeech.Default.GetLocalesAsync();

        if (locales == null || !locales.Any())
            return null;

        string lang = (languageCode ?? "vi").Trim().ToLower();

        var locale = locales.FirstOrDefault(l =>
            !string.IsNullOrWhiteSpace(l.Language) &&
            l.Language.StartsWith(lang, StringComparison.OrdinalIgnoreCase));

        if (locale == null && lang.Contains("-"))
        {
            string shortLang = lang.Split('-')[0];

            locale = locales.FirstOrDefault(l =>
                !string.IsNullOrWhiteSpace(l.Language) &&
                l.Language.StartsWith(shortLang, StringComparison.OrdinalIgnoreCase));
        }

        if (locale == null)
        {
            locale = locales.FirstOrDefault(l =>
                !string.IsNullOrWhiteSpace(l.Language) &&
                l.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase));
        }

        if (locale == null)
        {
            locale = locales.FirstOrDefault();
        }

        return locale;
    }

    // Công dụng: dừng audio đang phát ngay lập tức.
    // Dùng khi bấm STOP hoặc khi mất audio focus.
    public void StopCurrent()
    {
        try
        {
            _currentSpeakCts?.Cancel();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AUDIO STOP ERROR] {ex.Message}");
        }
    }

    // Công dụng: xóa toàn bộ audio còn đang chờ trong queue.
    public void ClearQueue()
    {
        while (_channel.Reader.TryRead(out _))
        {
        }
    }

    // Công dụng: dừng audio hiện tại và xóa toàn bộ hàng chờ.
    // Dùng khi người dùng dừng theo dõi.
    public void StopAll()
    {
        StopCurrent();
        ClearQueue();
        _audioFocusService.AbandonFocus();
    }
}