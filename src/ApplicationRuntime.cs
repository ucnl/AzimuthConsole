using AzimuthConsole.Web;
using AZMLib;
using System.Text;
using UCNLDrivers;
using static Program;

namespace AzimuthConsole;

public class ApplicationRuntime : IDisposable
{
    private bool _disposed;
    private bool _applicationTerminate;
    private ConsoleLogOptions _consoleLogOption = ConsoleLogOptions.Enabled;

    private TSLogProvider? _logger;
    private AZMSettingsContainer? _azmSettings;
    private ApplicationSettings? _appSettings;
    private UDPListener? _rctrlUdpListener;
    private UDPTranslator? _rctrlUdpTranslator;
    private AZMCombiner? _azmCombiner;
    private ConsoleInputProcessor? _inputProcessor;
    private WebServer? _webServer;
    private WebDataConverter? _webDataConverter;
    private ILogPlayer? _logPlayer;
    private DateTime _lastWebUpdate = DateTime.Now;

    private uRadantPort? _antennaRotator;
    private CalibrationManager? _calibrationManager;

    private CmdRegistrar? _cmdRegistrar;

    private readonly CmdProcessor _cmdLineArgProcessor = new();
    private readonly CmdProcessor _terminalCmdProcessor = new();
    private readonly CmdProcessor _rctrlCmdProcessor = new();
    private readonly CmdProcessor _webCmdProcessor = new();

    #region Logging helpers

    private void Log(LogLineType eventType, string message)
    {
        if (_logger == null) return;

        switch (_consoleLogOption)
        {
            case ConsoleLogOptions.Disabled:
                break;

            case ConsoleLogOptions.Errors_only:
                if (eventType == LogLineType.INFO)
                    _logger.WriteSilent($"{eventType}: {message}");
                else
                    _logger.Write($"{eventType}: {message}");
                break;

            case ConsoleLogOptions.Enabled:
                _logger.Write($"{eventType}: {message}");
                break;
        }
    }
    private void Log(string message) => Log(LogLineType.INFO, message);
    private void LogInfo(string message) => Log(LogLineType.INFO, message);
    private void LogError(string message) => Log(LogLineType.ERROR, message);

    #endregion

    public void Initialize(string[] args)
    {
        var logFileName = StrUtils.GetTimeDirTreeFileName(
            DateTime.Now, AppContext.BaseDirectory, "log", "log", true);

        _logger = new TSLogProvider(logFileName);
        _logger.TextAddedEvent += (o, e) =>
        {
            if (_consoleLogOption != ConsoleLogOptions.Disabled)
                Console.Write(e.Text);
        };
        _logger.WriteStart();
        _logger.Write($"{AppUtils.GetFullVersionInfo()} (C) UC&NL, unavlab.com");

        _logger.Write("Checking log size...");
        var logRoot = Path.Combine(AppContext.BaseDirectory, "log");
        int filesDeleted = 0;
        long bytesFreed = 0;
        try
        {
            _logger.CleanOldLogs(logRoot, 500L * 1024 * 1024, "*.log", out filesDeleted, out bytesFreed);
        }
        catch (Exception ex)
        {
            LogError($"ERROR during cleaning old log files: {ex.Message}");
        }

        if (filesDeleted > 0)
            _logger.Write($"Cleaned up {filesDeleted} old log files ({bytesFreed / 1024 / 1024} MB freed)");

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            LogError($"FATAL: {e.ExceptionObject}");

        _cmdLineArgProcessor.OutputMessageHandler += (line) => Log(line);
        _terminalCmdProcessor.OutputMessageHandler += (line) => Log(line);

        _logPlayer = new LogPlayerWrapper();
        _logPlayer.NewLineHandler += (o, e) => _azmCombiner?.Emulate(e.Line);
        _logPlayer.PlaybackFinishedHandler += (o, e) => _azmCombiner?.Disconnect();

        _azmSettings = new AZMSettingsContainer();
        _appSettings = new ApplicationSettings();

        _cmdRegistrar = new CmdRegistrar(
            _cmdLineArgProcessor,
            _terminalCmdProcessor,
            _rctrlCmdProcessor,
            _webCmdProcessor,
            _azmSettings,
            _appSettings,
            combiner: null,
            antennaRotator: null,
            calibrationManager: null,
            _inputProcessor,
            _logPlayer,
            () => { _applicationTerminate = true; return true; }
        );

        _cmdRegistrar.RegisterCommandLineOnlyCommands();

        foreach (var arg in args)
        {
            _cmdLineArgProcessor.Process(arg);
        }

        Log($"\r\n- AZM core settings:{_azmSettings}\r\n- Application settings:{_appSettings}");

        InitializeComponents();

        _cmdRegistrar = new CmdRegistrar(
            _cmdLineArgProcessor,
            _terminalCmdProcessor,
            _rctrlCmdProcessor,
            _webCmdProcessor,
            _azmSettings,
            _appSettings,
            _azmCombiner!,
            _antennaRotator!,
            _calibrationManager!,
            _inputProcessor,
            _logPlayer,
            () => { _applicationTerminate = true; return true; }
        );
        _cmdRegistrar.RegisterAllCommands();
        _cmdRegistrar.RegisterWebCommands();
    }

    private void InitializeComponents()
    {
        _inputProcessor = new ConsoleInputProcessor();
        SetupHotkeys();

        Console.WriteLine();
        Console.WriteLine(_inputProcessor.GetHotkeysDescription());

        StartWebServer();
        SetupUdpRemoteControl();

        _azmCombiner = new AZMCombiner(_azmSettings);

        if (_appSettings!.antennaRotatorEnabled)
        {
            _antennaRotator = new uRadantPort(_appSettings.antennaRotatorPortBaudrate)
            {
                IsLogIncoming = true,
                IsTryAlways = true,
                IsRawModeOnly = true
            };

            _antennaRotator.ProposedPortName = _appSettings.antennaRotatorPortName;
            _antennaRotator.LogEventHandler += (o, e) => Log(e.EventType, e.LogString);

            _antennaRotator.CurrentAngleChangedEventHandler += (o, e) => Log($"[RDT] Current angle: {_antennaRotator.CurrentAngle:F1}°");
            _antennaRotator.WaitingToFinishRotationChangedEventHandler += (o, e) => Log($"[RDT] Waiting to finish: {_antennaRotator.WaitingToFinishRotation}");
        }

        if (_antennaRotator != null)        
            _calibrationManager = new CalibrationManager(_antennaRotator, _azmCombiner, LogInfo, AppContext.BaseDirectory);

        if (_appSettings.webServerEnabled)
        {
            _webDataConverter = new WebDataConverter(_azmCombiner);
            _webDataConverter.SetCalibrationManager(_calibrationManager);
            _webServer?.SetWebDataConverter(_webDataConverter);
        }

        SetupAzmCombinerHandlers();

        if (!string.IsNullOrEmpty(_appSettings.antennaCalibrationTableFile))
        {
            try
            {
                var (angles, errors) = AZMAntennaCorrector.LoadFromFile(_appSettings.antennaCalibrationTableFile);
                _azmCombiner.ApplyCalibrationTable(angles, errors);
                LogInfo("Calibration table loaded successfully");
            }
            catch (Exception ex)
            {
                LogError($"Failed to load calibration table: {ex.Message}");
            }
        }

        _webCmdProcessor.OutputMessageHandler += (line) => Log($"[WEB] {line}");
    }

    private void SetupHotkeys()
    {
        if (_inputProcessor == null) return;

        _inputProcessor.RegisterHotkey(
            new ConsoleKeyInfo('\0', ConsoleKey.F1, false, false, false),
            () => _inputProcessor.EmulateCommand("help"),
            "Show help.");

        _inputProcessor.RegisterHotkey(
            new ConsoleKeyInfo('\0', ConsoleKey.F12, false, false, false),
            () =>
            {
                int nvalue = ((int)_consoleLogOption + 1) % (int)ConsoleLogOptions.Invalid;
                _consoleLogOption = (ConsoleLogOptions)nvalue;
                Console.WriteLine($"Logging to console: {_consoleLogOption}");
            },
            "Switch console output.");

        _inputProcessor.RegisterHotkey(
            new ConsoleKeyInfo('\0', ConsoleKey.N, false, false, true),
            () =>
            {
                if (_azmCombiner != null)
                {
                    if (_azmCombiner.ConnectionActive)
                        _terminalCmdProcessor.Process("ccon");
                    else
                        _terminalCmdProcessor.Process("ocon");
                }
            },
            "Network: Open/Close connections (OCON/CCON).");

        _inputProcessor.RegisterHotkey(
            new ConsoleKeyInfo('\0', ConsoleKey.I, false, false, true),
            () =>
            {
                if (_azmCombiner != null)
                {
                    if (_azmCombiner.InterrogationActive)
                        _terminalCmdProcessor.Process("pitg");
                    else
                        _terminalCmdProcessor.Process("ritg");
                }
            },
            "Interrogation: Pause/Resume responders interrogation.");

        _inputProcessor.RegisterHotkey(
            new ConsoleKeyInfo('\0', ConsoleKey.L, false, false, true),
            () => _inputProcessor.EmulateCommand("cls"),
            "Clear screen.");

        _inputProcessor.RegisterHotkey(
            new ConsoleKeyInfo('\0', ConsoleKey.E, false, false, true),
            () => _inputProcessor.EmulateCommand("exit"),
            "Exit application.");
    }

    private void StartWebServer()
    {
        if (_appSettings == null || !_appSettings.webServerEnabled) return;

        try
        {
            _webServer = new WebServer(8080);
            _webServer.LogEventHandler += (o, e) => Log(e.EventType, e.LogString);

            _webServer.WebCommandReceived += (o, e) =>
            {
                _webCmdProcessor.Process(e.Command);
            };

            _webServer.Start();
            LogInfo("Web server started on port 8080");
        }
        catch (Exception ex)
        {
            LogError($"Failed to start web server: {ex.Message}");
        }
    }

    private void SetupUdpRemoteControl()
    {
        if (_appSettings == null || !_appSettings.rctrl_enabled) return;

        LogInfo($"Initializing UDP remote control output RCTRL_OUT on {_appSettings.rctrl_out_endpoint}...");
        _rctrlUdpTranslator = new UDPTranslator(_appSettings.rctrl_out_endpoint.Port, _appSettings.rctrl_out_endpoint.Address);
        _rctrlCmdProcessor.OutputMessageHandler += (line) =>
        {
            try
            {
                _rctrlUdpTranslator.Send(line);
                LogInfo($"{_appSettings.rctrl_out_endpoint} (RCTRL_OUT) << {line}");
            }
            catch (Exception ex)
            {
                LogError($"RCTRL_OUT send failed: {ex.Message}");
            }
        };

        LogInfo($"Initializing UDP remote control input RCTRL_IN on {_appSettings.rctrl_in_endpoint}...");
        _rctrlUdpListener = new UDPListener(_appSettings.rctrl_in_endpoint.Port);
        _rctrlUdpListener.StartListen();
        _rctrlUdpListener.DataReceivedHandler += (o, e) =>
        {
            var rctrl_cmd = Encoding.ASCII.GetString(e.Data);
            _rctrlCmdProcessor.Process(rctrl_cmd);
            LogInfo($"{_appSettings.rctrl_in_endpoint} (RCTRL_IN) >> {rctrl_cmd}");
        };
    }

    private void SetupAzmCombinerHandlers()
    {
        if (_azmCombiner == null) return;

        _azmCombiner.AZMPreferredPortName = _azmSettings?.azmPrefPortName ?? "";

        _azmCombiner.OutputHandler += (o, e) =>
        {
            _logger?.WriteSilent($"<< {e.Line}");

            if (_appSettings?.webServerEnabled == true && _webDataConverter != null && _webServer != null)
            {
                if ((DateTime.Now - _lastWebUpdate).TotalMilliseconds > 100)
                {
                    var webData = _webDataConverter.ConvertToWebData(e.Line);
                    _webServer.UpdateData(webData);
                    _lastWebUpdate = DateTime.Now;
                }
            }
        };

        _azmCombiner.LogEventHandler += (o, e) => Log(e.EventType, e.LogString);

        _azmCombiner.CREQResultHandler += (o, e) => _azmCombiner.OutputHandler(_azmCombiner,
            new StringEventArgs($"CREQR,{e.RemoteAddress},{e.ReqCode},{e.ResCode}"));

        _azmCombiner.RSTSReceivedHandler += (o, e) => _azmCombiner.OutputHandler(_azmCombiner,
            new StringEventArgs($"RRA,{e.Addr}"));

        if (_antennaRotator != null)
        {
            _azmCombiner.DetectingChainCompletedHandler += (o, e) =>
            {
                if (!_antennaRotator.IsActive)
                {
                    try
                    {
                        _antennaRotator.Start();
                        LogInfo("[RDT] Started (chain completed)");
                    }
                    catch (Exception ex)
                    {
                        LogError($"[RDT] Start failed: {ex.Message}");
                    }
                }
            };
        }
    }

    public async Task RunInteractiveMode()
    {
        LogInfo("Starting interactive mode...");

        while (!_applicationTerminate)
        {
            var cmd = await _inputProcessor?.ReadCommandAsync()!;
            if (cmd != null)
                _terminalCmdProcessor.Process(cmd);
        }
    }

    public async Task RunDaemonMode()
    {
        LogInfo("Starting daemon mode...");

        var tcs = new TaskCompletionSource<bool>();

        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            LogInfo("Received SIGTERM, shutting down...");
            tcs.TrySetResult(true);
        };

        await tcs.Task;
        _applicationTerminate = true;
    }

    public void Dispose()
    {
        if (_disposed) return;

        LogInfo("Shutting down...");

        if (_appSettings?.rctrl_enabled == true)
        {
            _rctrlCmdProcessor.OnOutput("Application terminating...");
            _rctrlUdpListener?.StopListen();
        }

        _azmCombiner?.Disconnect();
        _azmCombiner?.Dispose();

        _webServer?.Stop();
        _webServer?.Dispose();

        _logger?.Flush();
        _logger?.FinishLog();
        _inputProcessor?.Dispose();

        _disposed = true;
    }
}