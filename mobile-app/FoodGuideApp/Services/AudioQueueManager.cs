using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Maui.Media;

namespace FoodGuideApp.Services;

// Công dụng: quản lý hàng chờ audio của app.
// - Phát audio tuần tự, không chồng nhau
// - Chống enqueue trùng do GPS rung
// - Cooldown sau khi phát để tránh đọc lặp
// - Bỏ job quá cũ hoặc không còn hợp lệ
// - Xin audio focus trước khi phát
// - Tự dừng audio hiện tại khi hệ thống báo mất audio focus
public class AudioQueueManager
{
    private readonly Channel<AudioJob> _channel;
    private readonly ConcurrentDictionary<string, DateTime> _recentQueued;
    private readonly ConcurrentDictionary<string, DateTime> _recentPlayed;

    // Thời gian chống enqueue trùng
    private readonly TimeSpan _duplicateWindow = TimeSpan.FromSeconds(10);

    // Thời gian cooldown sau khi đã phát xong
    private readonly TimeSpan _playCooldown = TimeSpan.FromSeconds(20);

    // Job chờ quá lâu thì bỏ
    private readonly TimeSpan _staleJobWindow = TimeSpan.FromSeconds(15);

    // Giữ cache dedupe vừa đủ để tránh phình bộ nhớ khi tracking lâu.
    private readonly TimeSpan _dedupeRetention = TimeSpan.FromMinutes(5);

    private readonly IAudioFocusService _audioFocusService;

    private CancellationTokenSource? _currentSpeakCts;
    private string? _currentPlayingKey;
    private bool _isProcessing = false;
    private readonly object _lock = new();

    // Callback từ MainPage để kiểm tra POI còn hợp lệ không
    // Ví dụ:
    // audioManager.IsStillRelevant = poiId => nearestPoiId == poiId;
    public Func<int, bool>? IsStillRelevant { get; set; }

    // Công dụng: khởi tạo queue audio, bộ nhớ chống trùng,
    // và gắn sự kiện mất audio focus để dừng audio hiện tại.
    public AudioQueueManager(IAudioFocusService audioFocusService)
    {
        _channel = Channel.CreateUnbounded<AudioJob>();
        _recentQueued = new ConcurrentDictionary<string, DateTime>();
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
    // - Bỏ nếu text rỗng
    // - Bỏ nếu vừa enqueue trùng
    // - Bỏ nếu vừa mới phát xong và còn cooldown
    public async Task EnqueueAsync(AudioJob job)
    {
        if (job == null || string.IsNullOrWhiteSpace(job.Text))
            return;

        string key = BuildKey(job);
        var now = DateTime.UtcNow;

        PruneDedupeCache(now);

        // Chống queue thêm đúng POI/ngôn ngữ đang phát.
        if (string.Equals(_currentPlayingKey, key, StringComparison.OrdinalIgnoreCase))
        {
            System.Diagnostics.Debug.WriteLine($"[AUDIO] Skip đang phát: {key}");
            return;
        }

        // Chống enqueue trùng liên tục do GPS rung
        if (_recentQueued.TryGetValue(key, out var lastQueued))
        {
            if (now - lastQueued < _duplicateWindow)
            {
                System.Diagnostics.Debug.WriteLine($"[AUDIO] Skip enqueue trùng: {key}");
                return;
            }
        }

        // Chống phát lại quá sớm sau khi vừa phát xong
        if (_recentPlayed.TryGetValue(key, out var lastPlayed))
        {
            if (now - lastPlayed < _playCooldown)
            {
                System.Diagnostics.Debug.WriteLine($"[AUDIO] Skip cooldown: {key}");
                return;
            }
        }

        _recentQueued[key] = now;

        await _channel.Writer.WriteAsync(job);
        System.Diagnostics.Debug.WriteLine($"[AUDIO] Enqueued | {key}");
    }

    // Công dụng: lấy từng job từ queue và phát bằng Text-to-Speech.
    // Trước khi phát sẽ xin audio focus, phát xong sẽ nhả focus.
    private async Task ProcessQueueAsync()
    {
        await foreach (var job in _channel.Reader.ReadAllAsync())
        {
            string? key = null;

            try
            {
                if (job == null || string.IsNullOrWhiteSpace(job.Text))
                    continue;

                key = BuildKey(job);

                // Bỏ job quá cũ
                if (DateTime.UtcNow - job.CreatedAt > _staleJobWindow)
                {
                    System.Diagnostics.Debug.WriteLine($"[AUDIO] Bỏ job cũ: {key}");
                    continue;
                }

                // Bỏ job không còn phù hợp với POI hiện tại
                if (IsStillRelevant != null && !IsStillRelevant(job.PoiId))
                {
                    System.Diagnostics.Debug.WriteLine($"[AUDIO] Bỏ job không còn hợp lệ: {key}");
                    continue;
                }

                _currentPlayingKey = key;

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

                System.Diagnostics.Debug.WriteLine(
                    $"[AUDIO] Playing | PoiId={job.PoiId} | Lang={job.Language} | Priority={job.Priority}");

                await TextToSpeech.Default.SpeakAsync(
                    job.Text,
                    options,
                    _currentSpeakCts.Token
                );

                _recentPlayed[key] = DateTime.UtcNow;

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
                if (key != null && string.Equals(_currentPlayingKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    _currentPlayingKey = null;
                }

                _audioFocusService.AbandonFocus();
            }
        }
    }

    // Công dụng: tạo key duy nhất cho từng audio job theo POI + ngôn ngữ
    private string BuildKey(AudioJob job)
    {
        string language = (job.Language ?? "vi").Trim().ToLowerInvariant();
        return $"{job.PoiId}_{language}";
    }

    // Công dụng: dọn các mốc dedupe cũ để queue chạy lâu không giữ dữ liệu không cần thiết.
    private void PruneDedupeCache(DateTime now)
    {
        foreach (var item in _recentQueued)
        {
            if (now - item.Value > _dedupeRetention)
            {
                _recentQueued.TryRemove(item.Key, out _);
            }
        }

        foreach (var item in _recentPlayed)
        {
            if (now - item.Value > _dedupeRetention)
            {
                _recentPlayed.TryRemove(item.Key, out _);
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

        string lang = (languageCode ?? "vi").Trim().ToLowerInvariant();

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
        _currentPlayingKey = null;
        _audioFocusService.AbandonFocus();
    }
}
