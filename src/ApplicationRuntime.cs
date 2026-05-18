// AzimuthConsole/ApplicationRuntime.cs
using AzimuthConsole.Commands;
using AzimuthConsole.Contexts;
using AzimuthConsole.InputSources;
using AzimuthConsole.Web;
using AZMLib;
using AZMLib.Output;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UCNLDrivers;
using UCNLDrivers.uAux;

namespace AzimuthConsole
{
    public class ApplicationRuntime : IAsyncDisposable
    {
        private bool _disposed;

        #region Settings

        private AZMSettings _azmSettings = new();
        private AuxSettings _auxSettings = new();
        private OutputSettings _outputSettings = new();
        private ApplicationSettings _appSettings = new();

        #endregion

        #region Core components

        private TSLogProvider? _logger;
        private CommandRouter _router = new();
        private readonly List<IInputSource> _inputs = new();
        private CancellationTokenSource _cts = new();

        private AuxManager? _auxManager;
        private AuxDataProvider? _auxData;
        private PortManager? _portManager;
        private AuxAZMPort? _azmPort;
        private AZMManager? _azmManager;
        private CalibrationManager? _calibrationManager;
        private WebServer? _webServer;
        private LogPlayer? _logPlayer;

        private bool _webLogCommands = false;

        #endregion

        #region RCTRL

        private UdpClient? _rctrlUdpIn;
        private UdpClient? _rctrlUdpOut;
        private IPEndPoint? _rctrlOutEndpoint;

        #endregion

        #region Public properties for commands

        public string AzmStatus => _azmPort?.Status.ToString() ?? "Inactive";
        public bool InterrogationActive => _azmManager?.InterrogationActive ?? false;
        public bool HasValidPosition => _azmManager?.State.Lat_deg.IsInitializedAndNotObsolete ?? false;
        public bool LocationOverrideActive => _azmManager?.LocationOverrideActive ?? false;

        public ushort AddressMask => _azmManager?.AddressMask ?? 0;
        public double Salinity => _azmManager?.Salinity_PSU ?? 0;
        public double MaxDistance => _azmManager?.MaxDist_m ?? 0;
        public double SoundSpeed => _azmManager?.SpeedOfSound_mps ?? double.NaN;
        public double AntennaXOffset => _azmManager?.AntennaXOffset_m ?? 0;
        public double AntennaYOffset => _azmManager?.AntennaYOffset_m ?? 0;
        public double AntennaPhi => _azmManager?.AntennaPhi_deg ?? 0;

        #endregion

        #region Initialization

        public ApplicationRuntime()
        {
            CommandRegistration.RegisterAll(_router, this);
        }

        public async Task InitializeAsync(string[] args)
        {
            // Логгер
            var logFileName = StrUtils.GetTimeDirTreeFileName(
           DateTime.Now, AppContext.BaseDirectory, "log", "log", true);
            _logger = new TSLogProvider(logFileName);

            _logger.TextAddedEvent += (_, e) =>
            {
                switch (_appSettings.ConsoleLogMode)
                {
                    case ConsoleLogMode.Silent:
                        break;
                    case ConsoleLogMode.ErrorsOnly:
                        if (e.Text.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
                            Console.Write(e.Text);
                        break;
                    default:
                        Console.Write(e.Text);
                        break;
                }
            };

            _logger.WriteStart();
            _logger.Write($"{AppUtils.GetFullVersionInfo()} (C) UC&NL, https://unavlab.com");

            _logger.Write("Checking log size...");
            var logRoot = Path.Combine(AppContext.BaseDirectory, "log");
            int filesDeleted = 0;
            long bytesFreed = 0;
            try
            {
                _logger.CleanOldLogs(logRoot, 100L * 1024 * 1024, "*.log", out filesDeleted, out bytesFreed);
            }
            catch (Exception ex)
            {
                _logger.Write($"ERROR during cleaning old log files: {ex.Message}");
            }

            if (filesDeleted > 0)
                _logger.Write($"Cleaned up {filesDeleted} old log files ({bytesFreed / 1024 / 1024} MB freed)");

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                _logger.Write($"FATAL: {e.ExceptionObject}");


            _router.OnLog += line =>
            {
                // Не пишем веб-команды в лог (они слишком частые)
                if (line.StartsWith("[web]") && !_webLogCommands)
                    return;
                _logger?.WriteSilent(line);
            };

            // Парсим аргументы командной строки
            var initCtx = new InitContext(line => _logger.Write($"[init] {line}"));
            foreach (var arg in args)
                await _router.ProcessAsync(arg, initCtx);

            // Создаём AuxManager и порты
            _auxManager = new AuxManager();
            _portManager = new PortManager(_auxManager);
            SubscribeToPortEvents();
            InitializePorts();

            // Создаём AZMManager
            _azmPort = (AuxAZMPort?)_auxManager.GetSource("azm");
            if (_azmPort == null)
            {
                _azmPort = new AuxAZMPort("azm", _auxSettings.AzmPortBaudrate)
                {
                    ProposedPortName = _auxSettings.AzmPrefPortName,
                    IsTryAlways = true,
                    IsLogIncoming = true
                };
                _auxManager.Register(_azmPort);
            }

            _auxData = new AuxDataProvider(_auxManager);
            _azmManager = new AZMManager(_azmPort, _azmSettings, _outputSettings, _auxData);
            _azmManager.LogEventHandler += (_, e) => _logger?.Write($"[AZM] {e.EventType}: {e.LogString}");
            _azmManager.InterrogationActiveChangedHandler += (_, _) => _logger?.Write($"[AZM] Interrogation: {_azmManager.InterrogationActive}");            

            // Калибровочная таблица
            if (!string.IsNullOrEmpty(_appSettings.AntennaCalibrationTableFile))
            {
                try
                {
                    var (angles, errors) = AZMAntennaCorrector.LoadFromFile(
                        _appSettings.AntennaCalibrationTableFile);
                    _azmManager.LoadCalibrationTable(angles, errors);
                    _logger?.Write("[AZM] Calibration table loaded");
                }
                catch (Exception ex)
                {
                    _logger?.Write($"[AZM] Failed to load calibration table: {ex.Message}");
                }
            }

            // Логирование статусов портов
            _auxManager.OnSourceStatusChanged += (_, e) => _logger?.Write($"[PORT] {e.Info.Id}: {e.Info.Status}");

            if (_appSettings.WebServerEnabled)
            {
                _webServer = new WebServer(8080, _router, 
                    onLogAlways: msg => _logger?.Write(msg),
                    onLogCommands: msg => { if (_webLogCommands) _logger?.Write(msg); }
                );

                _webServer?.SetLogProviders(
                    getCurrentLog: () =>
                    {
                        try { return File.Exists(logFileName) ? File.ReadAllBytes(logFileName) : null; }
                        catch { return null; }
                    },
                    getLogArchive: () =>
                    {
                        try
                        {
                            var logDir = Path.Combine(AppContext.BaseDirectory, "log");
                            if (!Directory.Exists(logDir)) return null;

                            var tempFile = Path.Combine(Path.GetTempPath(), $"azimuth_logs_{Guid.NewGuid()}.zip");
                            System.IO.Compression.ZipFile.CreateFromDirectory(logDir, tempFile);
                            var data = File.ReadAllBytes(tempFile);
                            try { File.Delete(tempFile); } catch { }
                            return data;
                        }
                        catch { return null; }
                    }
);

                _webServer?.Start();
                _logger?.Write("[WEB] Server started on port 8080");

                _azmManager.OnLineGenerated += line => _webServer?.Broadcast(line);
                _azmPort.DeviceInfoValidChanged += (_, _) => { _webServer?.Broadcast("!DINFO_UPDATED"); };
            }

            await TryLoadInitScript();

            // Запускаем источники ввода
            StartInputSources();

            _logPlayer = new LogPlayer();
            _logPlayer.NewLogLineHandler += (_, e) =>
            {
                _logger?.WriteSilent($"[LOGPLAY] {e.Line}");
                EmulateLine(e.Line);
            };
            _logPlayer.LogPlaybackFinishedHandler += (_, _) =>
            {
                _logger?.Write("[LOGPLAY] Playback finished");
                _azmManager?.Disconnect();
            };            

            _logger?.Write("[APP] Initialization complete");                      
        }

        private async Task TryLoadInitScript()
        {
            var initFile = Path.Combine(AppContext.BaseDirectory, "init.cmd");

            if (File.Exists(initFile))
            {
                _logger?.Write($"[INIT] Found init.cmd, executing...");
                try
                {
                    await ExecuteScript(initFile);
                    _logger?.Write("[INIT] Init script executed successfully");
                }
                catch (Exception ex)
                {
                    _logger?.Write($"[INIT] Error executing init script: {ex.Message}");
                }
            }
            else
            {
                _logger?.Write("[INIT] No init.cmd found, using default settings");
            }
        }

        private void SubscribeToPortEvents()
        {
            if (_portManager == null) return;

            _portManager.OnSerialOutputChanged += (port, baud) =>
            {
                if (port == "OFF")
                    _azmManager?.SetSerialOutput("", BaudRate.baudRate9600);
                else
                    _azmManager?.SetSerialOutput(port, baud);
                _logger?.Write($"[OUTS] {(port == "OFF" ? "Disabled" : $"{port}@{baud}")}");
            };

            _portManager.OnUdpOutputChanged += (ep) =>
            {
                _azmManager?.SetUDPOutput(ep!);
                _logger?.Write($"[OUTU] {(ep == null ? "Disabled" : ep.ToString())}");
            };

            _portManager.OnBeaconUdpChanged += (addr, ep) =>
            {
                _azmManager?.SetBeaconUDPOutput(addr, ep!);
                _logger?.Write($"[SIOC] Beacon {addr}: {(ep == null ? "Disabled" : ep.ToString())}");
            };

            _portManager.OnRctrlInChanged += (ep) =>
            {
                StopRctrlInput();
                _rctrlUdpIn = new UdpClient();
                _rctrlUdpIn.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _rctrlUdpIn.Client.Bind(new IPEndPoint(IPAddress.Any, ep.Port));
                _ = RctrlReceiveLoop(_cts.Token);
                _logger?.Write($"[RCTRL] IN: listening on {ep}");
            };

            _portManager.OnRctrlOutChanged += (ep) =>
            {
                _rctrlOutEndpoint = ep;
                _rctrlUdpOut?.Close();
                _rctrlUdpOut = new UdpClient();
                _logger?.Write($"[RCTRL] OUT: {ep}");
            };

            _portManager.OnRctrlDisabled += () =>
            {
                StopRctrlInput();
                _rctrlUdpOut?.Close();
                _rctrlUdpOut = null;
                _logger?.Write("[RCTRL] Disabled");
            };
        }

        private void StopRctrlInput()
        {
            var client = Interlocked.Exchange(ref _rctrlUdpIn, null);
            if (client != null)
            {
                try { client.Close(); } catch { }
                try { client.Dispose(); } catch { }
            }
        }

        private async Task RctrlReceiveLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _rctrlUdpIn != null)
            {
                try
                {
                    var result = await _rctrlUdpIn.ReceiveAsync(ct);
                    var line = Encoding.ASCII.GetString(result.Buffer).Trim();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        ICommandContext ctx;

                        if (_rctrlUdpOut != null && _rctrlOutEndpoint != null)
                        {
                            // Отвечаем на OUT endpoint
                            ctx = new UdpRctrlContext(_rctrlUdpOut, _rctrlOutEndpoint);
                        }
                        else
                        {
                            // Отвечаем обратно отправителю через IN
                            ctx = new UdpRctrlContext(_rctrlUdpIn, result.RemoteEndPoint);
                        }

                        await _router.ProcessAsync(line, ctx);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
                catch (Exception ex)
                {
                    _logger?.Write($"[RCTRL] Receive error: {ex.Message}");
                }
            }
        }

        private void InitializePorts()
        {
            if (_auxSettings.Aux1Enabled)
            {
                var aux1 = new uAuxGNSSPort("aux1", _auxSettings.Aux1PortBaudrate)
                {
                    ProposedPortName = _auxSettings.Aux1PrefPortName,
                    Mode = _auxSettings.Aux1Alternative ? GNSSMode.Auto : GNSSMode.GNSSOnly
                };
                _auxManager!.Register(aux1);
            }

            if (_auxSettings.Aux2Enabled)
            {
                var aux2 = new uAuxGNSSPort("aux2", _auxSettings.Aux2PortBaudrate)
                {
                    ProposedPortName = _auxSettings.Aux2PrefPortName,
                    Mode = GNSSMode.CompassOnly
                };
                _auxManager!.Register(aux2);
            }
        }

        private void StartInputSources()
        {
            var terminal = new TerminalInputSource();
            terminal.CommandReceived += async (_, e) =>
                await _router.ProcessAsync(e.Line, e.Context);

            terminal.OnToggleLogMode += () =>
            {
                var modes = new[] { ConsoleLogMode.Normal, ConsoleLogMode.ErrorsOnly, ConsoleLogMode.Silent };
                var idx = Array.IndexOf(modes, _appSettings.ConsoleLogMode);
                _appSettings.ConsoleLogMode = modes[(idx + 1) % modes.Length];
                Console.WriteLine($"\n[LOG] Console mode: {_appSettings.ConsoleLogMode}");
            };

            _inputs.Add(terminal);
            _ = terminal.StartAsync();
        }

        #endregion

        #region Runtime

        public async Task RunAsync()
        {
            try
            {
                await Task.Delay(-1, _cts.Token);
            }
            catch (OperationCanceledException) { }
        }

        public void RequestShutdown()
        {
            // Отменяем в фоне, чтобы не блокировать поток, из которого вызван EXIT
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger?.Write("[APP] Shutdown requested...");
                    await Task.Delay(200);
                    
                    _cts.Cancel();
                }
                catch (Exception ex)
                {
                    _logger?.Write($"[APP] Error during shutdown: {ex.Message}");
                }
            });
        }

        #endregion        

        #region Command handlers

        public async Task<CommandResult> ConfigurePortAsync(string portId, Dictionary<string, string> args)
        {
            return _portManager == null
                ? CommandResult.Error("not initialized")
                : await _portManager.ConfigureAsync(portId, args);
        }

        public IEnumerable<string> GetAllPortsInfo()
        {
            return _portManager?.GetAllInfo() ?? Enumerable.Empty<string>();
        }

        public async Task ConnectAsync()
        {
            if (_auxSettings.ActivationOrder.Count > 0)
                _auxManager?.ActivateChain(_auxSettings.ActivationOrder.ToArray());
            else
                _auxManager?.Activate("azm");

            _logger?.Write("[APP] Connections opened");
        }

        public async Task DisconnectAsync()
        {
            _azmManager?.Disconnect();
            _auxManager?.Deactivate("azm");
            _auxManager?.Deactivate("aux1");
            _auxManager?.Deactivate("aux2");
            _logger?.Write("[APP] Connections closed");
        }

        public void ResumeInterrogation() => _azmManager?.ResumeInterrogation();
        public void PauseInterrogation() => _azmManager?.PauseInterrogation();
        public void UpdateAddressMask(ushort mask) => _azmManager?.UpdateAddressMask(mask);
        public void UpdateSalinity(double val) => _azmManager?.UpdateSalinity(val);
        public void UpdateMaxDistance(double val) => _azmManager?.UpdateMaxDistance(val);
        public void UpdateSoundSpeed(double val) => _azmManager?.UpdateSoundSpeed(val);
        public void UpdateAntennaOffsets(double x, double y, double phi) =>
            _azmManager?.UpdateAntennaOffsets(x, y, phi);

        public void OverrideLocation(double lat, double lon, double hdg)
        {
            var aux1 = _auxManager?.GetSource("aux1");
            if (aux1 != null && aux1.Status != AuxStatus.Inactive)
                _auxManager?.Deactivate("aux1");

            var aux2 = _auxManager?.GetSource("aux2");
            if (aux2 != null && aux2.Status != AuxStatus.Inactive)
                _auxManager?.Deactivate("aux2");

            _azmManager?.OverrideLocation(lat, lon, hdg);
            _logger?.Write($"[LHOV] Override: {lat:F6}, {lon:F6}, {hdg:F1}°");
        }

        public void DisableLocationOverride()
        {
            _azmManager?.DisableLocationOverride();

            var aux1 = _auxManager?.GetSource("aux1");
            if (aux1 != null)
                _auxManager?.Activate("aux1");

            var aux2 = _auxManager?.GetSource("aux2");
            if (aux2 != null)
                _auxManager?.Activate("aux2");

            _logger?.Write("[LHOV] Override disabled");
        }

        public bool IsDeviceDetected(string id) =>
            _auxManager?.GetSource(id)?.Status == AuxStatus.Detected;

        public void RequestBeaconData(int addr, int code) =>
            _azmManager?.RequestDataFromBeacon((REMOTE_ADDR_Enum)(addr - 1), (CDS_REQ_CODES_Enum)code);

        public void QueryLocalAddress() => _azmManager?.QueryLocalAddress();
        public void SetLocalAddress(int addr) =>
            _azmManager?.QueryLocalAddressSet((REMOTE_ADDR_Enum)(addr - 1));

        public void SetLBLResponders(string mode, double[] coords)
        {
            switch (mode)
            {
                case "1":
                    _azmManager?.Set3RespondersLocalCoordinates(
                        coords[0], coords[1], coords[2], coords[3], coords[4], coords[5]);
                    break;
                case "2":
                    _azmManager?.Set3RespondersGeographicCoordinates(
                        coords[0], coords[1], coords[2], coords[3], coords[4], coords[5]);
                    break;
                default:
                    _azmManager?.Discard3RespondersCoordinates();
                    break;
            }
        }

        public void StartCalibration(double start, double step, int n)
        {
            if (_calibrationManager != null && _calibrationManager.State != CalibrationState.Idle)
            {
                _logger?.Write("[CAL] Calibration already in progress");
                return;
            }

            var rotator = _auxManager?.GetSource("rdt") as uAuxRadantPort;
            if (rotator == null)
            {
                _logger?.Write("[CAL] Antenna rotator not configured");
                return;
            }

            if (_azmManager == null)
            {
                _logger?.Write("[CAL] AZM Manager not initialized");
                return;
            }

            _azmManager.PauseInterrogation();

            _calibrationManager = new CalibrationManager(
                rotator,
                _azmManager,
                msg => _logger?.Write($"[CAL] {msg}"),
                AppContext.BaseDirectory);

            _calibrationManager.Start(start, step, n);
            _logger?.Write($"[CAL] Started: start={start:F1}°, step={step:F1}°, n={n}");
        }

        public void StopCalibration()
        {
            if (_calibrationManager == null) return;
            _calibrationManager.Stop();
            _azmManager?.ResumeInterrogation();
            _logger?.Write("[CAL] Stopped");
        }

        public Dictionary<string, string> GetCalibrationStatus()
        {
            var result = new Dictionary<string, string>();

            // Поворотная калибровка (SCAL)
            if (_calibrationManager != null)
            {
                result["state"] = _calibrationManager.State.ToString();
                result["points"] = _calibrationManager.CollectedPoints.ToString();
                result["total"] = _calibrationManager.TotalPoints.ToString();
                result["angle"] = _calibrationManager.CurrentRotatorAngle.ToString("F1");
            }
            else
            {
                result["state"] = "idle";
                result["points"] = "0";
                result["total"] = "0";
                result["angle"] = "0.0";
            }

            // Угловая калибровка (ACAL)
            if (_azmManager != null)
            {
                if (_azmManager.AngularCalibration)
                {
                    result["acal_state"] = "collecting";
                    result["acal_collected"] = _azmManager.AngularCalibrationCollected.ToString();
                    result["acal_total"] = _azmManager.AngularCalibrationTotal.ToString();
                }
                else if (_azmManager.AngularCalibrationCollected >= _azmManager.AngularCalibrationTotal &&
                         _azmManager.AngularCalibrationTotal > 0)
                {
                    result["acal_state"] = "completed";
                    result["acal_collected"] = _azmManager.AngularCalibrationCollected.ToString();
                    result["acal_total"] = _azmManager.AngularCalibrationTotal.ToString();
                    result["acal_phi"] = _azmManager.AntennaPhi_deg.ToString("F1");
                }
                else
                {
                    result["acal_state"] = "idle";
                    result["acal_collected"] = "0";
                    result["acal_total"] = "0";
                }
            }
            else
            {
                result["acal_state"] = "idle";
                result["acal_collected"] = "0";
                result["acal_total"] = "0";
            }

            return result;
        }

        public Dictionary<string, string> GetSystemState()
        {
            return new()
            {
                ["azm_status"] = AzmStatus,
                ["interrogation"] = InterrogationActive.ToString(),
                ["position_valid"] = HasValidPosition.ToString(),
                ["address_mask"] = AddressMask.ToString(),
                ["salinity"] = Salinity.ToString("F1"),
                ["max_distance"] = MaxDistance.ToString("F0"),
                ["sound_speed"] = SoundSpeed.ToString("F1"),
                ["antenna_x"] = AntennaXOffset.ToString("F2"),
                ["antenna_y"] = AntennaYOffset.ToString("F2"),
                ["antenna_phi"] = AntennaPhi.ToString("F1"),
                ["device_type"] = _azmManager?.DeviceType.ToString() ?? "unknown",
                ["serial_number"] = _azmPort?.SerialNumber ?? "",
                ["location_override"] = LocationOverrideActive.ToString(),
            };
        }

        public void StartAngularCalibration(double st, double nd, double step, int n, int addr) =>
            _azmManager?.AngularCalibrationStart(st, nd, step, n, (REMOTE_ADDR_Enum)(addr - 1));

        public string GetOutputFormat()
        {
            var loc = _azmManager?.State.GetStationParametersToStringFormat() ?? "";
            var rem = new ResponderBeacon(REMOTE_ADDR_Enum.REM_ADDR_1).GetToStringFormat();
            return $"@AZMLOC:\r\n{loc}\r\n@AZMREM:\r\n{rem}";
        }

        public bool GetPSIMSSBOutput() => _azmManager?.IsPSIMSSBOutputEnabled ?? false;

        public void SetPSIMSSBOutput(bool on)
        {
            if (_azmManager != null)
                _azmManager.IsPSIMSSBOutputEnabled = on;
            _logger?.Write($"[OUTPUT] PSIMSSB {(on ? "enabled" : "disabled")}");
        }

        public void SetWebLogging(bool enable)
        {
            _webLogCommands = enable;
            _logger?.Write($"[WEB] Command logging: {(enable ? "ON" : "OFF")}");
        }

        public bool GetWebLogging() => _webLogCommands;

        public bool StartLogPlayback(bool isInstant, string fileName)
        {
            if (_logPlayer == null || _logPlayer.IsRunning)
                return false;

            // Создаём AZM если нет
            if (_auxManager?.GetSource("azm") == null)
            {
                _azmPort = new AuxAZMPort("azm", BaudRate.baudRate9600)
                {
                    IsTryAlways = false,
                    IsLogIncoming = false
                };
                _auxManager?.Register(_azmPort);
                _logger?.Write("[LOGPLAY] AZM port created for emulation");
            }
            _auxManager?.Activate("azm");

            if (isInstant)
                _logPlayer.PlaybackInstant(fileName);
            else
                _logPlayer.Playback(fileName);

            _logger?.Write($"[LOGPLAY] Started: {fileName}");
            return true;
        }

        public void EmulateLine(string line)
        {
            if (line.Contains("(AZM)"))
            {
                var idx = line.IndexOf(">>");
                if (idx > 0)
                {
                    var nmea = line.Substring(idx + 2).Trim();
                    _azmPort?.EmulateInput(nmea);
                }
            }
            else if (line.Contains("(GNSS)"))
            {
                EnsureAux1Exists(isBP: false);
                var aux1 = _auxManager?.GetSource("aux1") as uAuxGNSSPort;
                var idx = line.IndexOf(">>");
                if (idx > 0 && aux1 != null)
                {
                    var nmea = line.Substring(idx + 2).Trim();
                    aux1.EmulateInput(nmea);
                }
            }
            else if (line.Contains("(BPS)"))
            {
                EnsureAux1Exists(isBP: true);
                var aux1 = _auxManager?.GetSource("aux1") as uAuxBPPort;
                var idx = line.IndexOf(">>");
                if (idx > 0 && aux1 != null)
                {
                    var nmea = line.Substring(idx + 2).Trim();
                    aux1.EmulateInput(nmea);
                }
            }
        }

        private void EnsureAux1Exists(bool isBP)
        {
            var existing = _auxManager?.GetSource("aux1");
            if (existing != null) return;

            uAuxPort port = isBP
                ? new uAuxBPPort("aux1", BaudRate.baudRate9600)
                : new uAuxGNSSPort("aux1", BaudRate.baudRate9600);

            port.IsTryAlways = false;
            port.IsLogIncoming = false;
            _auxManager?.Register(port);
            _auxManager?.Activate("aux1");
            _logger?.Write($"[LOGPLAY] AUX1 {(isBP ? "BP" : "GNSS")} created for emulation");
        }

        public bool StopLogPlayback()
        {
            if (_logPlayer == null || !_logPlayer.IsRunning) return false;

            _logPlayer.RequestToStop();
            _auxManager?.Deactivate("azm");
            _auxManager?.Deactivate("aux1");
            _logger?.Write("[LOGPLAY] Stopped");
            return true;
        }

        public bool ExportCommandReference(string filePath)
        {
            try
            {
                var markdown = _router.ExportMarkdown("AzimuthConsole Command Reference");
                File.WriteAllText(filePath, markdown);
                _logger?.Write($"[EXPORT] Command reference saved to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Write($"[EXPORT] Failed: {ex.Message}");
                return false;
            }
        }

        public async Task ExecuteScript(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger?.Write($"[SCRIPT] Script file not found: {filePath}");
                return;
            }

            _logger?.Write($"[SCRIPT] Executing script: {filePath}");
            var ctx = new InitContext(line => _logger?.Write($"[SCRIPT] {line}"));

            foreach (var line in File.ReadLines(filePath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                await _router.ProcessAsync(trimmed, ctx);
            }

            _logger?.Write("[SCRIPT] Script execution completed");
        }

        public bool CleanOldLogs()
        {
            try
            {
                var logDir = Path.Combine(AppContext.BaseDirectory, "log");
                if (!Directory.Exists(logDir)) return false;

                var currentLogPath = _logger?.FileName;
                var deletedFiles = 0;
                var deletedDirs = 0;
                long freedBytes = 0;

                // Удаляем старые логи
                foreach (var file in Directory.GetFiles(logDir, "*.log", SearchOption.AllDirectories))
                {
                    if (!string.IsNullOrEmpty(currentLogPath) &&
                        string.Equals(Path.GetFullPath(file), Path.GetFullPath(currentLogPath), StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        var fi = new FileInfo(file);
                        freedBytes += fi.Length;
                        fi.Delete();
                        deletedFiles++;
                    }
                    catch { }
                }

                // Удаляем пустые папки (кроме корневой log)
                foreach (var dir in Directory.GetDirectories(logDir))
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir);
                            deletedDirs++;
                        }
                    }
                    catch { }
                }

                _logger?.Write($"[DELGS] Deleted {deletedFiles} files, {deletedDirs} empty dirs, freed {freedBytes / 1024 / 1024} MB");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Write($"[DELGS] Error: {ex.Message}");
                return false;
            }
        }

        #region Wait commands

        public async Task<CommandResult> WaitForCalibration(Dictionary<string, string> args)
        {
            var timeout = args.TryGetValue("timeout", out var ts) ? int.Parse(ts) : 300000;
            var cts = new CancellationTokenSource(timeout);

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var status = GetCalibrationStatus();
                    if (status.TryGetValue("state", out var state) &&
                        (state == "Completed" || state == "Failed" || state == "Idle"))
                    {
                        return state == "Completed"
                            ? CommandResult.Ok("state", state)
                            : CommandResult.Error($"calibration ended with state: {state}");
                    }
                    await Task.Delay(500, cts.Token);
                }
                return CommandResult.Error("timeout");
            }
            catch (OperationCanceledException)
            {
                return CommandResult.Error("timeout");
            }
        }

        public async Task<CommandResult> WaitForAngularCalibration(Dictionary<string, string> args)
        {
            var timeout = args.TryGetValue("timeout", out var ts) ? int.Parse(ts) : 300000;
            var cts = new CancellationTokenSource(timeout);

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var status = GetCalibrationStatus();
                    if (status.TryGetValue("acal_state", out var state) &&
                        (state == "completed" || state == "idle"))
                    {
                        if (state == "completed" && status.TryGetValue("acal_phi", out var phi))
                            return CommandResult.Ok("acal_state", state);
                        return CommandResult.Error("angular calibration ended without completion");
                    }
                    await Task.Delay(500, cts.Token);
                }
                return CommandResult.Error("timeout");
            }
            catch (OperationCanceledException)
            {
                return CommandResult.Error("timeout");
            }
        }

        public async Task<CommandResult> WaitForConnection(Dictionary<string, string> args)
        {
            var timeout = args.TryGetValue("timeout", out var ts) ? int.Parse(ts) : 30000;
            var cts = new CancellationTokenSource(timeout);

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    if (AzmStatus == "Detected")
                        return CommandResult.Ok("status", "Detected");
                    await Task.Delay(500, cts.Token);
                }
                return CommandResult.Error("timeout");
            }
            catch (OperationCanceledException)
            {
                return CommandResult.Error("timeout");
            }
        }

        public async Task<CommandResult> WaitForDetected(Dictionary<string, string> args)
        {
            var portId = args.GetValueOrDefault("port", "azm");
            var timeout = args.TryGetValue("timeout", out var ts) ? int.Parse(ts) : 30000;
            var cts = new CancellationTokenSource(timeout);

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    if (IsDeviceDetected(portId))
                        return CommandResult.Ok("port", portId);
                    await Task.Delay(500, cts.Token);
                }
                return CommandResult.Error("timeout");
            }
            catch (OperationCanceledException)
            {
                return CommandResult.Error("timeout");
            }
        }

        #endregion

        public bool SaveSettings(string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# AzimuthConsole settings");
                sb.AppendLine($"# Saved: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();

                // Порты
                sb.AppendLine("# Ports");
                var auxManager = _auxManager;
                if (auxManager != null)
                {
                    foreach (var info in auxManager.GetAllSources())
                    {
                        var id = info.Id;
                        var port = info.PortName ?? "AUTO";
                        if (port == "N/A") port = "AUTO";

                        if (id == "azm")
                            sb.AppendLine($"AZM,port={port},baud=9600");
                        else if (id == "aux1")
                        {
                            var proto = (info.Kind == UCNLDrivers.uAux.AuxSourceKind.GNSS) ? "NMEA" : "BP";
                            sb.AppendLine($"AUX1,proto={proto},port={port},baud=9600");
                        }
                        else if (id == "aux2")
                            sb.AppendLine($"AUX2,port={port},baud=9600");
                        else if (id == "rdt")
                            sb.AppendLine($"RDT,port={port},baud=9600");
                    }
                }
                sb.AppendLine();

                // RCTRL
                sb.AppendLine("# RCTRL");
                if (_portManager != null)
                {
                    var info = _portManager.GetAllInfo().FirstOrDefault(i => i.StartsWith("rctrl"));
                    if (info != null)
                    {
                        var parts = info.Split('|');
                        // rctrl|IN:port|OUT:ip:port|status
                        if (parts.Length >= 3)
                        {
                            var inPart = parts[1].StartsWith("IN:") ? parts[1].Substring(3) : "";
                            var outPart = parts[2].StartsWith("OUT:") ? parts[2].Substring(4) : "";
                            if (!string.IsNullOrEmpty(inPart) || !string.IsNullOrEmpty(outPart))
                            {
                                var cmd = "RCTRL";
                                if (!string.IsNullOrEmpty(inPart)) cmd += $",in={inPart}";
                                if (!string.IsNullOrEmpty(outPart)) cmd += $",out={outPart}";
                                sb.AppendLine(cmd);
                            }
                        }
                    }
                }
                sb.AppendLine();

                // System
                sb.AppendLine("# System parameters");
                sb.AppendLine($"MSK,mask={AddressMask}");
                sb.AppendLine($"SLN,val={Salinity.ToString("F1", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"MDST,val={MaxDistance.ToString("F0", CultureInfo.InvariantCulture)}");
                var sos = SoundSpeed.ToString("F1", CultureInfo.InvariantCulture);
                if (sos == "NaN") sos = "";
                sb.AppendLine($"SOS,val={sos}");
                sb.AppendLine($"OFS,x={AntennaXOffset.ToString("F2", CultureInfo.InvariantCulture)},y={AntennaYOffset.ToString("F2", CultureInfo.InvariantCulture)},phi={AntennaPhi.ToString("F1", CultureInfo.InvariantCulture)}");
                sb.AppendLine();

                // Output
                sb.AppendLine("# Output");
                sb.AppendLine($"PSIMSSB,on={(GetPSIMSSBOutput() ? "TRUE" : "FALSE")}");

                // Serial output
                if (_portManager != null)
                {
                    var outsInfo = _portManager.GetAllInfo().FirstOrDefault(i => i.StartsWith("outs"));
                    if (outsInfo != null)
                    {
                        var parts = outsInfo.Split('|');
                        if (parts.Length >= 2 && parts[1] != "OFF")
                            sb.AppendLine($"OUTS,port={parts[1]},baud=9600");
                    }

                    // UDP output
                    var outuInfo = _portManager.GetAllInfo().FirstOrDefault(i => i.StartsWith("outu"));
                    if (outuInfo != null)
                    {
                        var parts = outuInfo.Split('|');
                        if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]) && parts[1] != "OFF")
                            sb.AppendLine($"OUTU,addr={parts[1]}");
                    }
                }
                sb.AppendLine();

                // LBL coordinates (если заданы)
                sb.AppendLine("# LBL (uncomment if needed)");
                sb.AppendLine($"# SRC3,mode=1,c0=0,c1=0,c2=0,c3=0,c4=0,c5=0");
                sb.AppendLine();

                File.WriteAllText(filePath, sb.ToString());
                _logger?.Write($"[SAVE] Settings saved to {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Write($"[SAVE] Error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region IDisposable / IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try { _webServer?.Stop(); } catch { }
            try { _webServer?.Dispose(); } catch { }

            _cts.Cancel();

            foreach (var input in _inputs)
            {
                try { await input.StopAsync(); } catch { }
            }

            try { _calibrationManager?.Stop(); } catch { }

            try { StopRctrlInput(); } catch { }
            try { _rctrlUdpOut?.Close(); } catch { }
            try { _rctrlUdpOut?.Dispose(); } catch { }            

            try { _azmManager?.Disconnect(); } catch { }
            try { _azmManager?.Dispose(); } catch { }

            

            try { _logger?.Flush(); } catch { }
            try { _logger?.FinishLog(); } catch { }
        }

        #endregion
    }
}