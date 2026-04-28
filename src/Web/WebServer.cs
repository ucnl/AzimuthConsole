using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using UCNLDrivers;

namespace AzimuthConsole.Web
{
    public class WebCommandEventArgs : EventArgs
    {
        public string Command { get; }
        public object? Data { get; }

        public WebCommandEventArgs(string command, object? data = null)
        {
            Command = command;
            Data = data;
        }
    }
    public class WebServer : IDisposable
    {
        private HttpListener _listener;
        private readonly int _port;
        private bool _isRunning;
        private readonly CancellationTokenSource _cts = new();
        private readonly List<WebSocketConnection> _webSockets = new();
        private readonly object _wsLock = new();

        private WebUIData _currentData = new();
        private readonly object _dataLock = new();

        private bool _networkAccessEnabled = false;

        private WebDataConverter? _webDataConverter;

        public void SetWebDataConverter(WebDataConverter converter)
        {
            _webDataConverter = converter;
        }

        public WebServer(int port = 8080)
        {
            _port = port;
        }

        public void Start()
        {            
            if (TryStartWithPrefixes(new[] { $"http://localhost:{_port}/", $"http://+:{_port}/" }))
            {
                _networkAccessEnabled = true;
                LogInfo($"Web server started on port {_port} (network access enabled)");
                LogNetworkAddresses();
            }            
            else if (TryStartWithPrefixes(new[] { $"http://localhost:{_port}/" }))
            {
                _networkAccessEnabled = false;
                LogInfo($"Web server started on port {_port} (localhost only)");
                LogInfo($"  Access locally at: http://localhost:{_port}/"); // ← Добавили локальный адрес
                LogInfo($"To enable network access, run as Administrator once or execute:");
                LogInfo($"  netsh http add urlacl url=http://+:{_port}/ user=%USERDOMAIN%\\%USERNAME%");
            }            
            else
            {
                throw new InvalidOperationException($"Failed to start web server on port {_port}");
            }

            _ = Task.Run(ListenAsync);
        }

        private bool TryStartWithPrefixes(string[] prefixes)
        {
            try
            {
                _listener = new HttpListener();
                foreach (var prefix in prefixes)
                {
                    _listener.Prefixes.Add(prefix);
                }
                _listener.Start();
                _isRunning = true;
                return true;
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5)
            {                
                return false;
            }
            catch (Exception ex)
            {                
                LogError($"Start error: {ex.Message}");
                return false;
            }
        }

        private void LogNetworkAddresses()
        {
            try
            {
                LogInfo("Web interface available at:");
                LogInfo($"  Local: http://localhost:{_port}/");

                // Способ 1: через DNS (работает везде)
                var hostName = System.Net.Dns.GetHostName();
                var host = System.Net.Dns.GetHostEntry(hostName);
                var addedIps = new HashSet<string>();

                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        !System.Net.IPAddress.IsLoopback(ip))
                    {
                        var ipStr = ip.ToString();
                        if (addedIps.Add(ipStr))
                        {
                            LogInfo($"  Network: http://{ipStr}:{_port}/");
                        }
                    }
                }

                // Способ 2: через NetworkInterface (дополнительно для Linux)
                try
                {
                    var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                    foreach (var ni in networkInterfaces)
                    {
                        if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                            ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                        {
                            foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                            {
                                if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                {
                                    var ipStr = ip.Address.ToString();
                                    if (addedIps.Add(ipStr))
                                    {
                                        LogInfo($"  Network: http://{ipStr}:{_port}/");
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Игнорируем ошибки второго способа — первый уже сработал
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to get network addresses: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _cts.Cancel();
            _listener.Stop();

            lock (_wsLock)
            {
                foreach (var ws in _webSockets)
                {
                    ws.Dispose();
                }
                _webSockets.Clear();
            }
        }

        private async Task ListenAsync()
        {
            while (_isRunning && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequestAsync(context));
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                        LogError($"Listen error: {ex.Message}");
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                if (request.IsWebSocketRequest)
                {
                    await HandleWebSocketAsync(context);
                    return;
                }

                var path = request.Url?.AbsolutePath.TrimStart('/');

                if (string.IsNullOrEmpty(path))
                {
                    path = "index.html";
                }

                if (path == "api/data")
                {
                    // Всегда отдаём свежие данные с Calibration
                    var data = _webDataConverter?.ConvertToWebData() ?? _currentData;
                    await SendJsonResponse(response, data);
                    return;
                }

                if (path == "api/emulate" && request.HttpMethod == "POST")
                {
                    using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                    var json = await reader.ReadToEndAsync();
                    var emulData = JsonSerializer.Deserialize<EmulationData>(json);

                    if (emulData != null)
                    {
                        LogInfo($"Emulation data received: {JsonSerializer.Serialize(emulData)}");
                    }

                    await SendJsonResponse(response, new { status = "ok" });
                    return;
                }

                await ServeStaticFile(response, path);
            }
            catch (Exception ex)
            {
                LogError($"Request processing error: {ex.Message}");
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }

        private async Task HandleWebSocketAsync(HttpListenerContext context)
        {
            WebSocketConnection? connection = null;

            try
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                connection = new WebSocketConnection(wsContext.WebSocket);

                lock (_wsLock)
                {
                    _webSockets.Add(connection);
                }

                LogInfo("WebSocket client connected");

                var initialData = _webDataConverter?.ConvertToWebData() ?? _currentData;
                var json = JsonSerializer.Serialize(initialData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                _ = connection.SendAsync(json);

                await connection.ReceiveMessagesAsync(message =>
                {
                    try
                    {
                        var cmd = JsonSerializer.Deserialize<WebCommand>(message);
                        if (cmd != null)
                        {
                            ProcessCommand(cmd);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"WebSocket message error: {ex.Message}");
                    }

                    return Task.CompletedTask;
                });

                LogInfo("WebSocket client disconnected");
            }
            catch (Exception ex)
            {
                LogError($"WebSocket error: {ex.Message}");
            }
            finally
            {
                if (connection != null)
                {
                    lock (_wsLock)
                    {
                        _webSockets.Remove(connection);
                    }
                    connection.Dispose();
                }
            }
        }


        public event EventHandler<WebCommandEventArgs>? WebCommandReceived;

        private void ProcessCommand(WebCommand cmd)
        {
            LogInfo($"Web command: {cmd.Type}");
            WebCommandReceived?.Invoke(this, new WebCommandEventArgs(cmd.Type, cmd.Data));
        }

        private async Task ServeStaticFile(HttpListenerResponse response, string path)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"AzimuthConsole.wwwroot.{path.Replace('/', '.')}";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                response.ContentType = GetContentType(path);
                response.ContentLength64 = stream.Length;

                await stream.CopyToAsync(response.OutputStream);
                response.Close();
            }
            catch (Exception ex)
            {
                LogError($"Error serving file {path}: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }

        private string GetContentType(string path)
        {
            var ext = Path.GetExtension(path).ToLower();
            return ext switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                ".json" => "application/json",
                _ => "application/octet-stream"
            };
        }

        private async Task SendJsonResponse(HttpListenerResponse response, object data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;

            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        public void UpdateData(WebUIData data)
        {
            lock (_dataLock)
            {
                _currentData = data;
            }

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            List<WebSocketConnection> socketsToUpdate;

            lock (_wsLock)
            {
                socketsToUpdate = _webSockets.ToList();
            }

            foreach (var ws in socketsToUpdate)
            {
                _ = SendToClientAsync(ws, json);
            }
        }

        private async Task SendToClientAsync(WebSocketConnection ws, string json)
        {
            try
            {
                await ws.SendAsync(json);
            }
            catch (Exception ex)
            {
                LogError($"Error sending to client: {ex.Message}");

                lock (_wsLock)
                {
                    if (_webSockets.Contains(ws))
                    {
                        _webSockets.Remove(ws);
                        ws.Dispose();
                    }
                }
            }
        }

        private void LogInfo(string message)
        {
            LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.INFO, $"[WEB] {message}"));
        }

        private void LogError(string message)
        {
            LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, $"[WEB ERROR] {message}"));
        }

        public void Dispose()
        {
            Stop();
            _listener.Close();
            _cts.Dispose();
        }

        public EventHandler<LogEventArgs>? LogEventHandler;
    }

    public class WebSocketConnection : IDisposable
    {
        private readonly System.Net.WebSockets.WebSocket _socket;
        private readonly CancellationTokenSource _cts = new();
        private bool _disposed;
        public string Id { get; } = Guid.NewGuid().ToString();
        public bool IsConnected => !_disposed && _socket.State == System.Net.WebSockets.WebSocketState.Open;

        public WebSocketConnection(System.Net.WebSockets.WebSocket socket)
        {
            _socket = socket;
        }

        public async Task SendAsync(string message)
        {
            if (_disposed) return;

            if (_socket.State != System.Net.WebSockets.WebSocketState.Open) return;
            
            try
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await _socket.SendAsync(
                    new ArraySegment<byte>(buffer),
                    System.Net.WebSockets.WebSocketMessageType.Text,
                    true,
                    _cts.Token);
            }
            catch (OperationCanceledException)
            {
            }                            
        }

        public async Task ReceiveMessagesAsync(Func<string, Task> onMessage)
        {
            var buffer = new byte[4096];

            try
            {
                while (_socket.State == System.Net.WebSockets.WebSocketState.Open &&
                       !_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _socket.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            _cts.Token);

                        if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                        {
                            await _socket.CloseAsync(
                                System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                                "Closed by client",
                                _cts.Token);
                            break;
                        }

                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await onMessage(message);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            }
            finally
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();

            try
            {
                if (_socket?.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    _ = _socket.CloseAsync(
                        System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                        "Disposed",
                        CancellationToken.None)
                        .ContinueWith(t => _socket?.Dispose());
                }
                else
                {
                    _socket?.Dispose();
                }
            }
            catch
            {
            }
        }
    }

    public class DeviceInfo
    {
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Z { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Heading { get; set; }
        public bool HasHeading { get; set; }
        public double? Pitch { get; set; }
        public double? Roll { get; set; }
        public double? Course { get; set; }
        public double? Speed { get; set; }
        public double? Depth { get; set; }
        public double? Temperature { get; set; }
        public double? Pressure { get; set; }
        public double? RError { get; set; }
        public double? DataAge { get; set; }
    }

    public class BeaconInfo
    {
        public int Address { get; set; }
        public double? Distance { get; set; }
        public double? Azimuth { get; set; }
        public double? Elevation { get; set; }
        public double? AbsoluteDistance { get; set; }
        public double? AbsoluteAzimuth { get; set; }
        public double? ReverseAzimuth { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Z { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? Depth { get; set; }
        public double? SignalLevel { get; set; }
        public double? Battery { get; set; }
        public double? Temperature { get; set; }
        public double? PropagationTime { get; set; }
        public string? Message { get; set; }
        public bool IsTimeout { get; set; }
        public double? SuccessRate { get; set; }
        public int? TotalRequests { get; set; }
        public string CoordinateType { get; set; } = "unknown";
        public double? DataAge { get; set; }
    }

    public class SystemInfo
    {
        public string DeviceType { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public string Version { get; set; } = "";
        public bool ConnectionActive { get; set; }
        public bool InterrogationActive { get; set; }
        public bool AzimuthDetected { get; set; }
        public bool Aux1Detected { get; set; }
        public bool Aux2Detected { get; set; }
        public bool LocationOverride { get; set; }
    }

    public class LogEntry
    {
        public string Timestamp { get; set; } = "";
        public string Message { get; set; } = "";
        public string Type { get; set; } = "info";
    }

    public class WebUIData
    {
        public string Mode { get; set; } = "unknown";
        public DeviceInfo LocalDevice { get; set; } = new();
        public List<BeaconInfo> Beacons { get; set; } = new();
        public List<LogEntry> RecentLogs { get; set; } = new();
        public SystemInfo SystemInfo { get; set; } = new();
        public DateTime Timestamp { get; set; }

        public CalibrationWebStatus? Calibration { get; set; }
    }

    public class WebCommand
    {
        public string Type { get; set; } = "";
        public object? Data { get; set; }
    }

    public class EmulationData
    {
        public string Mode { get; set; } = "polar";
        public DeviceInfo? LocalDevice { get; set; }
        public List<BeaconInfo>? Beacons { get; set; }
    }
}