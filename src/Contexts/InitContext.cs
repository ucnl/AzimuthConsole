// AzimuthConsole/Contexts/InitContext.cs
using AzimuthConsole.Commands;

namespace AzimuthConsole.Contexts
{
    public class InitContext : ICommandContext
    {
        private readonly Action<string> _log;

        public string SourceId => "init";

        public InitContext(Action<string> log)
        {
            _log = log;
        }

        public Task SendResponseAsync(string cmdId, CommandResult result)
        {
            _log($"[init] {cmdId},{result}");
            return Task.CompletedTask;
        }
    }
}