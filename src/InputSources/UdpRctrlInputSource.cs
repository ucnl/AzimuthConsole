// AzimuthConsole/InputSources/UdpRctrlInputSource.cs
using System.Net;
using System.Net.Sockets;
using System.Text;
using AzimuthConsole.Commands;
using AzimuthConsole.Contexts;

namespace AzimuthConsole.InputSources
{
    public class UdpRctrlInputSource : IInputSource
    {
        private readonly int _port;
        private UdpClient? _udp;
        private CancellationTokenSource? _cts;

        public string SourceId => "rctrl";
        public event EventHandler<CommandReceivedEventArgs>? CommandReceived;

        public UdpRctrlInputSource(int port)
        {
            _port = port;
        }

        public async Task StartAsync()
        {
            _cts = new CancellationTokenSource();
            _udp = new UdpClient(_port);
            await Task.Run(() => ReceiveLoop(_cts.Token));
        }

        public Task StopAsync()
        {
            _cts?.Cancel();
            _udp?.Close();
            _udp?.Dispose();
            return Task.CompletedTask;
        }

        private async Task ReceiveLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _udp != null)
            {
                try
                {
                    var result = await _udp.ReceiveAsync(ct);
                    var line = Encoding.ASCII.GetString(result.Buffer).Trim();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var ctx = new UdpRctrlContext(_udp, result.RemoteEndPoint);
                        CommandReceived?.Invoke(this, new CommandReceivedEventArgs(line, ctx));
                    }
                }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
            }
        }
    }
}