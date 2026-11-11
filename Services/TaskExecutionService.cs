namespace StorageClient.Redesign.Services;

public interface ITaskExecutionService
{
    Task ExecuteAsync(Func<Task> execute, string operationName, Action<string> onError);
    Task ExecuteWithDebounceAsync(
        string key, Func<CancellationToken, Task> execute, int delayMs,
        string operationName, Action<string> onError);
    void CancelDebounce(string key);
}

// Called from the WPF UI thread. Await keeps that context for VM updates.
public sealed class TaskExecutionService : ITaskExecutionService
{
    private readonly Dictionary<string, CancellationTokenSource> _pending = new();
    private Task _queueTail = Task.CompletedTask;

    public Task ExecuteAsync(Func<Task> execute, string operationName, Action<string> onError) =>
        ExecuteAsync(execute, operationName, onError, CancellationToken.None);

    public async Task ExecuteWithDebounceAsync(
        string key, Func<CancellationToken, Task> execute, int delayMs,
        string operationName, Action<string> onError)
    {
        CancelDebounce(key);
        using CancellationTokenSource source = new();
        _pending[key] = source;
        CancellationToken token = source.Token;

        try
        {
            await Task.Delay(delayMs, token);
            await ExecuteInQueueAsync(execute, operationName, onError, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            if (_pending.TryGetValue(key, out var current) && current == source)
            {
                _pending.Remove(key);
            }
        }
    }

    public void CancelDebounce(string key)
    {
        if (_pending.Remove(key, out var source))
        {
            source.Cancel();
        }
    }

    private async Task ExecuteInQueueAsync(
        Func<CancellationToken, Task> execute, string operationName,
        Action<string> onError, CancellationToken token)
    {
        Task previous = _queueTail;
        TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _queueTail = finished.Task;

        try
        {
            await previous;
            if (!token.IsCancellationRequested)
            {
                await ExecuteAsync(() => execute(token), operationName, onError, token);
            }
        }
        finally
        {
            finished.SetResult();
        }
    }

    private static async Task ExecuteAsync(
        Func<Task> execute, string operationName, Action<string> onError, CancellationToken token)
    {
        try
        {
            await execute();
        }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
            {
                string message = exception is HttpRequestException httpException
                    ? ApiErrorFormatter.Format(httpException)
                    : exception.Message;
                onError($"{operationName}: {message}");
            }
        }
    }
}
