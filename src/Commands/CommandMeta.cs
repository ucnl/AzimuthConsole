// Commands/CommandMeta.cs
namespace AzimuthConsole.Commands
{
    public class CommandMeta
    {
        public string Id { get; init; } = "";
        public string Category { get; init; } = "General";
        public string Sources { get; init; } = "T,R,W";  // Terminal, Rctrl, Web
        public string Parameters { get; init; } = "—";
        public string Response { get; init; } = "";
        public string Description { get; init; } = "";
    }
}