using StorageClient.Redesign.ViewModels;

namespace StorageClient.Redesign.Services;

public interface ICommandFactory
{
    RelayCommand CreateCommand(Action execute, Func<bool>? canExecute = null);
    AsyncRelayCommand CreateCommand(
        Func<Task> execute, string operationName, Action<string> onError, Func<bool>? canExecute = null);
}

public sealed class CommandFactory(ITaskExecutionService taskExecutionService) : ICommandFactory
{
    public RelayCommand CreateCommand(Action execute, Func<bool>? canExecute = null) =>
        new(execute, canExecute);

    public AsyncRelayCommand CreateCommand(
        Func<Task> execute, string operationName, Action<string> onError, Func<bool>? canExecute = null) =>
        new(() => taskExecutionService.ExecuteAsync(execute, operationName, onError), canExecute);
}
