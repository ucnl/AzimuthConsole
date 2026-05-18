// AzimuthConsole/Contexts/UdpRctrlContext.cs
using System.Net;
using System.Net.Sockets;
using System.Text;
using AzimuthConsole.Commands;

namespace AzimuthConsole.Contexts
{
    public class UdpRctrlContext : ICommandContext
    {
        private readonly UdpClient _udp;
        private readonly IPEndPoint _remote;

        public string SourceId => "rctrl";

        public UdpRctrlContext(UdpClient udp, IPEndPoint remote)
        {
            _udp = udp;
            _remote = remote;
        }

        public async Task SendResponseAsync(string cmdId, CommandResult result)
        {
            var line = $"{cmdId},{result}";
            var data = Encoding.ASCII.GetBytes(line);
            await _udp.SendAsync(data, data.Length, _remote);
        }
    }
}