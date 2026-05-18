// AzimuthConsole/InputSources/IInputSource.cs
using AzimuthConsole.Commands;

namespace AzimuthConsole.InputSources
{
    public class CommandReceivedEventArgs : EventArgs
    {
        public string Line { get; }
        public ICommandContext Context { get; }

        public CommandReceivedEventArgs(string line, ICommandContext context)
        {
            Line = line;
            Context = context;
        }
    }

    public interface IInputSource
    {
        string SourceId { get; }
        event EventHandler<CommandReceivedEventArgs>? CommandReceived;
        Task StartAsync();
        Task StopAsync();
    }
}
