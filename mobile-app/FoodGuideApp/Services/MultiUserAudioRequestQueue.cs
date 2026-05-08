using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace FoodGuideApp.Services;

public sealed class MultiUserAudioRequestQueue : IAsyncDisposable
{
    private readonly ConcurrentQueue<QueuedAudioRequest> _queue = new();
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly SemaphoreSlim _workerGate = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _queuedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _recentlyCompleted = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<AudioJob, CancellationToken, Task> _audioHandler;
    private readonly TimeSpan _duplicateCooldown;
    private readonly TimeSpan _dedupeRetention;
    private readonly CancellationTokenSource _shutdownCts = new();

    private Task? _workerTask;
    private string? _currentKey;

    public MultiUserAudioRequestQueue(
        Func<AudioJob, CancellationToken, Task> audioHandler,
        TimeSpan? duplicateCooldown = null,
        TimeSpan? dedupeRetention = null)
    {
        _audioHandler = audioHandler ?? throw new ArgumentNullException(nameof(audioHandler));
        _duplicateCooldown = duplicateCooldown ?? TimeSpan.FromSeconds(20);
        _dedupeRetention = dedupeRetention ?? TimeSpan.FromMinutes(5);
    }

    public int PendingCount => _queue.Count;

    public bool IsRunning => _workerTask is { IsCompleted: false };

    public void Start()
    {
        if (IsRunning)
            return;

        _workerTask = Task.Run(ProcessQueueAsync);
    }

    public Task<bool> EnqueueAsync(AudioJob job, string? userId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (job == null || string.IsNullOrWhiteSpace(job.Text))
            return Task.FromResult(false);

        var now = DateTime.UtcNow;
        var key = BuildKey(job);

        PruneDedupeCache(now);

        if (string.Equals(_currentKey, key, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(false);

        if (_queuedKeys.ContainsKey(key))
            return Task.FromResult(false);

        if (_recentlyCompleted.TryGetValue(key, out var completedAt) &&
            now - completedAt < _duplicateCooldown)
        {
            return Task.FromResult(false);
        }

        if (!_queuedKeys.TryAdd(key, 0))
            return Task.FromResult(false);

        _queue.Enqueue(new QueuedAudioRequest(job, key, userId, now));
        _queueSignal.Release();

        Start();

        return Task.FromResult(true);
    }

    public void ClearPending()
    {
        while (_queue.TryDequeue(out var request))
        {
            _queuedKeys.TryRemove(request.Key, out _);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdownCts.Cancel();
        _queueSignal.Release();

        if (_workerTask != null)
        {
            try
            {
                await _workerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _shutdownCts.Dispose();
        _queueSignal.Dispose();
        _workerGate.Dispose();
    }

    private async Task ProcessQueueAsync()
    {
        while (!_shutdownCts.IsCancellationRequested)
        {
            await _queueSignal.WaitAsync(_shutdownCts.Token).ConfigureAwait(false);

            if (!_queue.TryDequeue(out var request))
                continue;

            _queuedKeys.TryRemove(request.Key, out _);

            await _workerGate.WaitAsync(_shutdownCts.Token).ConfigureAwait(false);

            try
            {
                _currentKey = request.Key;

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
                await _audioHandler(request.Job, linkedCts.Token).ConfigureAwait(false);

                _recentlyCompleted[request.Key] = DateTime.UtcNow;
            }
            catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MULTI USER AUDIO QUEUE ERROR] {ex}");
            }
            finally
            {
                if (string.Equals(_currentKey, request.Key, StringComparison.OrdinalIgnoreCase))
                    _currentKey = null;

                _workerGate.Release();
            }
        }
    }

    private string BuildKey(AudioJob job)
    {
        var language = string.IsNullOrWhiteSpace(job.Language)
            ? "vi"
            : job.Language.Trim().ToLowerInvariant();

        var textHash = StringComparer.Ordinal.GetHashCode(job.Text.Trim()).ToString("X");

        return $"{job.PoiId}_{language}_{textHash}";
    }

    private void PruneDedupeCache(DateTime now)
    {
        foreach (var item in _recentlyCompleted)
        {
            if (now - item.Value > _dedupeRetention)
                _recentlyCompleted.TryRemove(item.Key, out _);
        }
    }

    private sealed record QueuedAudioRequest(
        AudioJob Job,
        string Key,
        string? UserId,
        DateTime CreatedAt);
}
