// AzimuthConsole/Web/WebServer.cs
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using AzimuthConsole.Commands;
using UCNLDrivers;

namespace AzimuthConsole.Web
{
    public class WebServer : IDisposable
    {
        private HttpListener _listener;
        private readonly int _port;
        private bool _isRunning;
        private readonly CancellationTokenSource _cts = new();
        private readonly List<WebSocketConnection> _webSockets = new();
        private readonly object _wsLock = new();

        private readonly CommandRouter _router;
        private readonly Action<string> _onLogAlways;
        private readonly Action<string>? _onLogCommands;

        private Func<byte[]>? _getLogArchive;
        private Func<byte[]>? _getCurrentLog;        

        public WebServer(int port, CommandRouter router,
        Action<string> onLogAlways, Action<string>? onLogCommands = null)
        {
            _port = port;
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _onLogAlways = onLogAlways ?? throw new ArgumentNullException(nameof(onLogAlways));
            _onLogCommands = onLogCommands;
        }

        public void Start()
        {
            if (TryStartWithPrefixes(new[] { $"http://localhost:{_port}/", $"http://+:{_port}/" }))
            {
                LogInfo($"Web server started on port {_port} (network access enabled)");
                LogNetworkAddresses();
            }
            else if (TryStartWithPrefixes(new[] { $"http://localhost:{_port}/" }))
            {
                LogInfo($"Web server started on port {_port} (localhost only)");
                LogInfo($"  Access locally at: http://localhost:{_port}/");
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
                    _listener.Prefixes.Add(prefix);
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

                var addedIps = new HashSet<string>();
                var hostName = Dns.GetHostName();
                var host = Dns.GetHostEntry(hostName);

                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ip))
                    {
                        var ipStr = ip.ToString();
                        if (addedIps.Add(ipStr))
                            LogInfo($"  Network: http://{ipStr}:{_port}/");
                    }
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

            // Отменяем все WebSocket-токены (ReceiveMessagesAsync выйдут сами)
            lock (_wsLock)
            {
                foreach (var ws in _webSockets)
                {
                    try
                    {
                        // Просто Dispose — он только отменит _cts, без Abort()
                        ws.Dispose();
                    }
                    catch { }
                }
                _webSockets.Clear();
            }

            // Ждём, пока все ReceiveMessagesAsync завершатся
            Thread.Sleep(200);

            // Теперь listener можно остановить
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
        }

        public void SetLogProviders(Func<byte[]>? getCurrentLog, Func<byte[]>? getLogArchive)
        {
            _getCurrentLog = getCurrentLog;
            _getLogArchive = getLogArchive;
        }

        private async Task ServeCurrentLogResponse(HttpListenerResponse response)
        {
            try
            {
                if (_getCurrentLog == null)
                {
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                var data = _getCurrentLog();
                if (data == null || data.Length == 0)
                {
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                response.ContentType = "text/plain";
                response.AddHeader("Content-Disposition", $"attachment; filename=\"current.log\"");
                response.ContentLength64 = data.Length;
                await response.OutputStream.WriteAsync(data);
                response.Close();
            }
            catch (Exception ex)
            {
                LogError($"Error serving current log: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }

        private async Task ServeLogsResponse(HttpListenerResponse response)
        {
            try
            {
                if (_getLogArchive == null)
                {
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                var data = _getLogArchive();
                if (data == null || data.Length == 0)
                {
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                response.ContentType = "application/zip";
                response.AddHeader("Content-Disposition", $"attachment; filename=\"azimuth_logs_{DateTime.Now:yyyyMMdd_HHmmss}.zip\"");
                response.ContentLength64 = data.Length;
                await response.OutputStream.WriteAsync(data);
                response.Close();
            }
            catch (Exception ex)
            {
                LogError($"Error serving logs archive: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }

        /// <summary>
        /// Отправить строку всем WebSocket-клиентам
        /// </summary>
        public void Broadcast(string line)
        {
            List<WebSocketConnection> sockets;
            lock (_wsLock)
            {
                sockets = _webSockets.ToList();
            }

            foreach (var ws in sockets)
                _ = ws.SendAsync(line);
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
                    path = "index.html";

                if (path == "api/logs")
                {
                    await ServeLogsResponse(response);
                    return;
                }

                if (path == "api/logs/current")
                {
                    await ServeCurrentLogResponse(response);
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

                lock (_wsLock) { _webSockets.Add(connection); }

                LogInfo("WebSocket client connected");

                try
                {
                    var schemaJson = _router.GetHelpJson();
                    await connection.SendAsync($"!SCHEMA,{schemaJson}");
                }
                catch (Exception ex)
                {
                    LogError($"Failed to send schema: {ex.Message}");
                }

                await connection.ReceiveMessagesAsync(async message =>
                {
                    try
                    {
                        var trimmed = message.Trim();
                        _onLogCommands?.Invoke($">> {trimmed}");
                        var ctx = new WebCommandContext(connection, _onLogCommands);
                        await _router.ProcessAsync(trimmed, ctx);
                    }
                    catch (Exception ex)
                    {
                        LogError($"WebSocket command error: {ex.Message}");
                    }
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
                    lock (_wsLock) { _webSockets.Remove(connection); }
                    connection.Dispose();
                }
            }
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

        private static string GetContentType(string path)
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

        private void LogInfo(string message) =>
            _onLogAlways($"[WEB] {message}");

        private void LogError(string message) =>
            _onLogAlways($"[WEB ERROR] {message}");

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
        }
    }

    /// <summary>
    /// Контекст команды от WebSocket-клиента.
    /// Ответ отправляется обратно через тот же WebSocket.
    /// </summary>
    public class WebCommandContext : ICommandContext
    {
        private readonly WebSocketConnection _connection;
        private readonly Action<string>? _onLog;

        public string SourceId => "web";

        public WebCommandContext(WebSocketConnection connection, Action<string>? onLog = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _onLog = onLog;
        }

        public async Task SendResponseAsync(string cmdId, CommandResult result)
        {
            var line = $"{cmdId},{result}";
            _onLog?.Invoke($"<< {line}");
            await _connection.SendAsync(line);
        }
    }

    public class WebSocketConnection : IDisposable
    {
        private readonly WebSocket _socket;
        private readonly CancellationTokenSource _cts = new();
        private bool _disposed;

        public string Id { get; } = Guid.NewGuid().ToString();
        public bool IsConnected => !_disposed && _socket.State == WebSocketState.Open;

        public WebSocketConnection(WebSocket socket)
        {
            _socket = socket;
        }

        public async Task SendAsync(string message)
        {
            if (_disposed || _socket.State != WebSocketState.Open) return;

            try
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await _socket.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }

        public async Task ReceiveMessagesAsync(Func<string, Task> onMessage)
        {
            var buffer = new byte[4096];

            try
            {
                while (_socket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        _cts.Token.ThrowIfCancellationRequested();

                        var result = await _socket.ReceiveAsync(
                            new ArraySegment<byte>(buffer), _cts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            // На закрытие отвечаем без привязки к _cts (он уже может быть отменён)
                            try
                            {
                                await _socket.CloseAsync(
                                    WebSocketCloseStatus.NormalClosure,
                                    "Closed by client",
                                    CancellationToken.None);
                            }
                            catch { }
                            break;
                        }

                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await onMessage(message);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (WebSocketException)
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
                // Не трогаем сокет здесь — WebServer.Stop() разберётся
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Только отменяем токен — ReceiveMessagesAsync выйдет сам
            _cts.Cancel();

            // Не вызываем ни Abort(), ни CloseAsync() — они требуют TakeLocks
            // Сокетом займётся HttpListener при закрытии

            try { _cts.Dispose(); } catch { }
        }
    }
}