using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace WebAPI.Services;

// Model yêu cầu audio từ một user cụ thể.
public sealed class MultiUserAudioRequest
{
    public string UserId { get; set; } = "";
    public int PoiId { get; set; }
    public string Language { get; set; } = "vi";
    public string Text { get; set; } = "";
    public int Priority { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Cầu nối async giữa producer (HTTP request) và consumer (worker)
    internal TaskCompletionSource<string> ResultSource { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public interface IMultiUserAudioQueueService
{
    // Thêm yêu cầu vào hàng đợi và chờ kết quả (audioKey hoặc rỗng nếu bị skip)
    Task<string> EnqueueAsync(MultiUserAudioRequest request, CancellationToken cancellationToken = default);

    int PendingCount { get; }
}

// Dịch vụ hàng đợi audio cho nhiều user đồng thời.
//
// Mô hình producer-consumer:
//   Producer: EnqueueAsync() → đẩy vào ConcurrentQueue → release _itemAvailable
//   Consumer: WorkerLoopAsync() → WaitAsync(_itemAvailable) → Dequeue → ProcessWithLimiterAsync()
//
// Chống trùng:
//   - _recentQueued:    skip nếu cùng (PoiId + lang) vừa được enqueue trong _duplicateWindow
//   - _recentProcessed: skip nếu cùng key vừa được xử lý xong trong _processCooldown
//
// Cách tích hợp (KHÔNG sửa code cũ):
//   1. Đăng ký DI trong Program.cs:
//        builder.Services.AddSingleton<IMultiUserAudioQueueService, MultiUserAudioQueueService>();
//   2. Gắn TTS engine thực tế qua property TtsProcessor (hoặc dùng stub mặc định).
//   3. Gọi từ controller MỚI AudioQueueController (xem AudioQueueController.cs).
public sealed class MultiUserAudioQueueService : IMultiUserAudioQueueService, IDisposable
{
    // Hàng chờ thread-safe (không giới hạn)
    private readonly ConcurrentQueue<MultiUserAudioRequest> _queue = new();

    // Semaphore đóng vai trò signal: Release 1 mỗi khi enqueue, WaitAsync 1 mỗi khi dequeue
    private readonly SemaphoreSlim _itemAvailable = new(0);

    // Giới hạn số request được xử lý đồng thời (mặc định 1 = tuần tự)
    private readonly SemaphoreSlim _concurrencyLimiter;

    // Chống enqueue trùng: key -> thời điểm enqueue gần nhất
    private readonly ConcurrentDictionary<string, DateTime> _recentQueued = new();

    // Chống phát lại quá sớm: key -> thời điểm xử lý xong gần nhất
    private readonly ConcurrentDictionary<string, DateTime> _recentProcessed = new();

    private readonly TimeSpan _duplicateWindow = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _processCooldown = TimeSpan.FromSeconds(20);
    private readonly TimeSpan _staleJobWindow = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _cacheRetention = TimeSpan.FromMinutes(5);

    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _workerTask;

    private int _pendingCount = 0;
    public int PendingCount => Volatile.Read(ref _pendingCount);

    // Gắn TTS engine thực tế vào đây.
    // Signature: (text, language, cancellationToken) → Task<string> (trả về audioKey/URL)
    // Nếu null → dùng stub (trả về key giả để test)
    public Func<string, string, CancellationToken, Task<string>>? TtsProcessor { get; set; }

    public MultiUserAudioQueueService(int maxConcurrent = 1)
    {
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        _workerTask = Task.Run(() => WorkerLoopAsync(_shutdownCts.Token));
    }

    // Producer: thêm request vào queue và chờ kết quả bất đồng bộ.
    public async Task<string> EnqueueAsync(
        MultiUserAudioRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("Request không hợp lệ hoặc text rỗng.");

        string key = BuildKey(request);
        DateTime now = DateTime.UtcNow;

        PruneCache(now);

        // Chống enqueue trùng do nhiều request cùng lúc
        if (_recentQueued.TryGetValue(key, out DateTime lastQueued) &&
            now - lastQueued < _duplicateWindow)
        {
            System.Diagnostics.Debug.WriteLine($"[MULTI-AUDIO] Skip trùng enqueue: {key}");
            return string.Empty;
        }

        // Chống xử lý lại quá sớm (cooldown)
        if (_recentProcessed.TryGetValue(key, out DateTime lastProcessed) &&
            now - lastProcessed < _processCooldown)
        {
            System.Diagnostics.Debug.WriteLine($"[MULTI-AUDIO] Skip cooldown: {key}");
            return string.Empty;
        }

        _recentQueued[key] = now;
        Interlocked.Increment(ref _pendingCount);
        _queue.Enqueue(request);
        _itemAvailable.Release();          // signal consumer

        System.Diagnostics.Debug.WriteLine(
            $"[MULTI-AUDIO] Enqueued | user={request.UserId} | key={key} | pending={PendingCount}");

        // Await kết quả từ consumer, tôn trọng cancellation của caller
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _shutdownCts.Token);

        return await request.ResultSource.Task.WaitAsync(linked.Token);
    }

    // Consumer: vòng lặp chính, tuần tự dequeue và dispatch xử lý
    private async Task WorkerLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Chặn tại đây cho đến khi có item hoặc bị shutdown
                await _itemAvailable.WaitAsync(cancellationToken);

                if (!_queue.TryDequeue(out MultiUserAudioRequest? request))
                    continue;

                // Chạy có kiểm soát concurrency; không await để worker tiếp tục dequeue
                _ = ProcessWithLimiterAsync(request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        DrainQueueOnShutdown();
    }

    // Bao bọc xử lý bằng semaphore giới hạn concurrency
    private async Task ProcessWithLimiterAsync(
        MultiUserAudioRequest request,
        CancellationToken cancellationToken)
    {
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            await ProcessRequestAsync(request, cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _pendingCount);
            _concurrencyLimiter.Release();
        }
    }

    // Xử lý một request: kiểm tra stale → gọi TtsProcessor → ghi kết quả
    private async Task ProcessRequestAsync(
        MultiUserAudioRequest request,
        CancellationToken cancellationToken)
    {
        string key = BuildKey(request);

        try
        {
            // Bỏ job quá cũ
            if (DateTime.UtcNow - request.CreatedAt > _staleJobWindow)
            {
                System.Diagnostics.Debug.WriteLine($"[MULTI-AUDIO] Bỏ job cũ: {key}");
                request.ResultSource.TrySetResult(string.Empty);
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[MULTI-AUDIO] Processing | user={request.UserId} | key={key}");

            string result;

            if (TtsProcessor != null)
            {
                result = await TtsProcessor(request.Text, request.Language, cancellationToken);
            }
            else
            {
                // Stub mặc định: thay bằng TTS engine thực tế qua TtsProcessor
                await Task.Delay(200, cancellationToken);
                result = $"audio_{key}_{DateTime.UtcNow.Ticks}";
            }

            _recentProcessed[key] = DateTime.UtcNow;

            System.Diagnostics.Debug.WriteLine($"[MULTI-AUDIO] Done | key={key} | result={result}");
            request.ResultSource.TrySetResult(result);
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[MULTI-AUDIO] Canceled: {key}");
            request.ResultSource.TrySetCanceled();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MULTI-AUDIO ERROR] {key}: {ex.Message}");
            request.ResultSource.TrySetException(ex);
        }
    }

    // Key duy nhất per (PoiId, Language) — giống logic AudioQueueManager
    private static string BuildKey(MultiUserAudioRequest request)
    {
        string lang = (request.Language ?? "vi").Trim().ToLowerInvariant();
        return $"{request.PoiId}_{lang}";
    }

    // Dọn cache cũ để không giữ bộ nhớ vô thời hạn
    private void PruneCache(DateTime now)
    {
        foreach (var pair in _recentQueued)
            if (now - pair.Value > _cacheRetention)
                _recentQueued.TryRemove(pair.Key, out _);

        foreach (var pair in _recentProcessed)
            if (now - pair.Value > _cacheRetention)
                _recentProcessed.TryRemove(pair.Key, out _);
    }

    // Khi shutdown: hủy tất cả request đang chờ trong queue
    private void DrainQueueOnShutdown()
    {
        while (_queue.TryDequeue(out MultiUserAudioRequest? req))
        {
            req.ResultSource.TrySetCanceled();
            Interlocked.Decrement(ref _pendingCount);
        }
    }

    public void Dispose()
    {
        _shutdownCts.Cancel();
        _workerTask.Wait(TimeSpan.FromSeconds(5));
        _shutdownCts.Dispose();
        _itemAvailable.Dispose();
        _concurrencyLimiter.Dispose();
    }
}