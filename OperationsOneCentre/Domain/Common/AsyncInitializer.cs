namespace OperationsOneCentre.Domain.Common;

/// <summary>
/// Thread-safe async initialization helper for singleton services.
/// Replaces the non-thread-safe "if (_isInitialized) return;" pattern
/// used across KnowledgeSearchService, ContextSearchService, ScriptSearchService,
/// JiraSolutionSearchService, and SapKnowledgeService.
/// </summary>
public sealed class AsyncInitializer : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private volatile bool _isInitialized;

    /// <summary>
    /// Whether initialization has completed successfully.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Execute the initialization action exactly once, in a thread-safe manner.
    /// Subsequent calls return immediately after the first successful initialization.
    /// </summary>
    public async Task InitializeOnceAsync(Func<Task> initAction, CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return; // Double-check
            await initAction();
            _isInitialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Reset initialization state (e.g., for hot-reload scenarios).
    /// </summary>
    public void Reset()
    {
        _isInitialized = false;
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
