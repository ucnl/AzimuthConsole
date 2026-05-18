// AzimuthConsole/Contexts/WebContext.cs
using System.Net.WebSockets;
using System.Text;
using AzimuthConsole.Commands;

namespace AzimuthConsole.Contexts
{
    public class WebContext : ICommandContext
    {
        private readonly WebSocket _socket;

        public string SourceId => "web";

        public WebContext(WebSocket socket)
        {
            _socket = socket;
        }

        public async Task SendResponseAsync(string cmdId, CommandResult result)
        {
            var line = $"{cmdId},{result}";
            var data = Encoding.UTF8.GetBytes(line);
            await _socket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}