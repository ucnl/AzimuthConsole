// AzimuthConsole/Contexts/TerminalContext.cs
using AzimuthConsole.Commands;

namespace AzimuthConsole.Contexts
{
    public class TerminalContext : ICommandContext
    {
        public string SourceId => "terminal";

        public Task SendResponseAsync(string cmdId, CommandResult result)
        {
            Console.WriteLine($"{cmdId},{result}");
            return Task.CompletedTask;
        }
    }
}