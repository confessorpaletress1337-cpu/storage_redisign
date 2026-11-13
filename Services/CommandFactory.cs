using StorageClient.Redesign.ViewModels;

namespace StorageClient.Redesign.Services;

public interface ICommandFactory
{
    RelayCommand CreateCommand(Action execute, Func<bool>? canExecute = null);
    AsyncRelayCommand CreateCommand(
        Func<Task> execute, string operationName, Action<string> onError, Func<bool>? canExecute = null);
    AsyncRelayCommand CreateCommand<T>(
        Func<T, Task> execute, string operationName, Action<string> onError, Predicate<T>? canExecute = null);
}

public sealed class CommandFactory(ITaskExecutionService taskExecutionService) : ICommandFactory
{
    public RelayCommand CreateCommand(Action execute, Func<bool>? canExecute = null) =>
        new(execute, canExecute);

    public AsyncRelayCommand CreateCommand(
        Func<Task> execute, string operationName, Action<string> onError, Func<bool>? canExecute = null) =>
        new(() => taskExecutionService.ExecuteAsync(execute, operationName, onError), canExecute);

    public AsyncRelayCommand CreateCommand<T>(
        Func<T, Task> execute, string operationName, Action<string> onError, Predicate<T>? canExecute = null) =>
        new(
            parameter => parameter is T value
                ? taskExecutionService.ExecuteAsync(() => execute(value), operationName, onError)
                : Task.CompletedTask,
            parameter => parameter is T value && (canExecute?.Invoke(value) ?? true));
}
