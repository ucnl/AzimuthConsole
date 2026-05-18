// AzimuthConsole/Commands/ICommandContext.cs
namespace AzimuthConsole.Commands
{
    public interface ICommandContext
    {
        string SourceId { get; }
        Task SendResponseAsync(string cmdId, CommandResult result);
    }
}