
using AzimuthConsole;
using AzimuthConsole.AZM;
using AzimuthConsole.Web;
using System.Text;
using UCNLDrivers;

internal class Program
{
    public enum ConsoleLogOptions
    {
        Enabled,
        Errors_only,
        Disabled,
        Invalid
    }

    private static WebServer? webServer;
    private static DateTime lastWebUpdate = DateTime.Now;

    private static async Task Main(string[] args)
    {
        bool application_terminate = false;

        ConsoleLogOptions consoleLogOption = ConsoleLogOptions.Enabled;

        LogPlayerWrapper? logPlayerWrapper = null;
        TSLogProvider? logger;
        SettingsContainer? settings = new();
        UDPListener? rctrl_udp_listener = null;
        UDPTranslator? rctrl_udp_translator = null;
        AZMCombiner? azmCombiner = null;
        ConsoleInputProcessor inputProcessor = new();
        CmdRegistrar registrar = new();
        WebDataConverter? webDataConverter = null;
        
        var logFileName = StrUtils.GetTimeDirTreeFileName(DateTime.Now, AppContext.BaseDirectory, "log", "log", true);

        logger = new TSLogProvider(logFileName);
        logger.TextAddedEvent += (o, e) => 
        { 
            if (consoleLogOption != ConsoleLogOptions.Disabled)
                Console.Write(e.Text); 
        };
        logger.WriteStart();

        logger.Write($"{AppUtils.GetFullVersionInfo()} (C) UC&NL, unavlab.com");

        AppDomain.CurrentDomain.UnhandledException += (s, e) => logger.Write($"FATAL: {e.ExceptionObject}");

        CmdProcessor cmdLineArgProcessor = new();
        cmdLineArgProcessor.OutputMessageHandler += (line) => logger.Write(line);
        CmdProcessor terminalCmdProcessor = new();
        terminalCmdProcessor.OutputMessageHandler += (line) => logger.Write(line);
        CmdProcessor rctrlCmdProcessor = new();

        logPlayerWrapper = new();
        logPlayerWrapper.NewLineHandler = (o, e) => azmCombiner?.Emulate(e.Line);
        logPlayerWrapper.PlaybackFinishedHandler += (o, e) => azmCombiner?.Disconnect();
        

        var hCmd = cmdLineArgProcessor.AddCommand("HELP", "", args =>
        {
            Console.WriteLine("--- Supported commands ---");
            Console.WriteLine("- Command line arguments -");
            Console.WriteLine(cmdLineArgProcessor.GetCommandsDescriptions());
            Console.WriteLine("- Terminal commands -");
            Console.WriteLine(terminalCmdProcessor.GetCommandsDescriptions());
            Console.WriteLine("- UDP remote control commands -");
            Console.WriteLine(rctrlCmdProcessor.GetCommandsDescriptions());

            return true;
        },
        "Show help");
        terminalCmdProcessor.AddCommand(hCmd);
        rctrlCmdProcessor.AddCommand("HELP", "", args =>
        {
            StringBuilder sb = new();
            sb.AppendLine("- UDP remote control commands -");
            sb.AppendLine(rctrlCmdProcessor.GetCommandsDescriptions());

            rctrlCmdProcessor.OnOutput(sb.ToString());
            return true;
        },
        "Show help");
        terminalCmdProcessor.AddCommand("CLS", "", args => { Console.Clear(); return true; }, "Clear screen");
        terminalCmdProcessor.AddCommand("EXIT", "", args => { application_terminate = true; return true; }, "Exit AzimuthConsole");
        terminalCmdProcessor.AddCommand("HKEYS", "", args => { terminalCmdProcessor.OnOutput(inputProcessor.GetHotkeysDescription()); return true; }, "Show hotkeys hint");
        terminalCmdProcessor.AddCommand("PLAY", "x,c--c", args =>
        {
            bool result = true;

            var lFileName = AZM.O2S(args[1]);
            if (string.IsNullOrEmpty(lFileName))
            {
                result = logPlayerWrapper.StopLogPlayBack();
            }
            else
            {
                result = logPlayerWrapper.StartLogPlayBack(args[0] != null, lFileName);
            }
            
            return result;
        },
        "Playback a log file. PLAY,[reserved],[logFile].");

        registrar.RegisterCommandLineCommands(settings, cmdLineArgProcessor, azmCombiner);
        
        foreach (var arg in args)
        {
            cmdLineArgProcessor.Process(arg);
        }

        logger.Write($"\r\n- Application settings:{settings}");

        inputProcessor.RegisterHotkey(
            new ConsoleKeyInfo('\0', ConsoleKey.F1, false, false, false),
            () => { inputProcessor.EmulateCommand("help"); },
            "Show help."
        );
        inputProcessor.RegisterHotkey(
            new ConsoleKeyInfo('\0', ConsoleKey.F12, false, false, false),
            () => {

                int nvalue = ((int)consoleLogOption + 1) % (int)ConsoleLogOptions.Invalid;
                consoleLogOption = (ConsoleLogOptions)nvalue;
                Console.WriteLine($"Logging to console: {consoleLogOption}");
            },
            "Switch console output."
        );
        inputProcessor.RegisterHotkey(
            new ConsoleKeyInfo('\0', ConsoleKey.N, false, false, true),
            () => 
            {
                if (azmCombiner != null)
                {
                    if (azmCombiner.ConnectionActive)
                        terminalCmdProcessor.Process("ccon");
                    else
                        terminalCmdProcessor.Process("ocon");
                }
            },
            "Network: Open/Close connections (OCON/CCON)."
        );
        inputProcessor.RegisterHotkey(
            new ConsoleKeyInfo('\0', ConsoleKey.I, false, false, true),
            () => 
            {
                if (azmCombiner != null)
                {
                    if (azmCombiner.InterrogationActive)
                        terminalCmdProcessor.Process("pitg");
                    else
                        terminalCmdProcessor.Process("ritg");
                }
            },
            "Interrogation: Pause/Resume responders interrogation."
        );        
        inputProcessor.RegisterHotkey(
            new ConsoleKeyInfo('\0', ConsoleKey.L, false, false, true),
            () => { inputProcessor.EmulateCommand("cls"); },
            "Clear screen."
        );
        inputProcessor.RegisterHotkey(
            new ConsoleKeyInfo('\0', ConsoleKey.E, false, false, true),
            () => { inputProcessor.EmulateCommand("exit"); },
            "Exit application."
        );

        Console.WriteLine();
        Console.WriteLine(inputProcessor.GetHotkeysDescription());


        if (settings.webServerEnabled)
        {
            try
            {
                webServer = new WebServer(8080);
                webServer.LogEventHandler += (o, e) =>
                {
                    if (consoleLogOption == ConsoleLogOptions.Errors_only)
                    {
                        if (e.EventType == LogLineType.INFO)
                            logger.WriteSilent($"{e.EventType}: {e.LogString}");
                        else
                            logger.Write($"{e.EventType}: {e.LogString}");
                    }
                    else if (consoleLogOption == ConsoleLogOptions.Enabled)
                    {
                        logger.Write($"{e.EventType}: {e.LogString}");
                    }
                };

                webServer.Start();
                logger.Write("Web interface available at http://localhost:8080");
            }
            catch (Exception ex)
            {
                logger.Write($"Failed to start web server: {ex.Message}");
            }
        }

        if (settings.rctrl_enabled)
        {            
            logger.Write($"Initializing UDP remote control output RCTRL_OUT on {settings.rctrl_out_endpoint}...");
            rctrl_udp_translator = new UDPTranslator(settings.rctrl_out_endpoint.Port, settings.rctrl_out_endpoint.Address);
            rctrlCmdProcessor.OutputMessageHandler += (line) =>
            {
                try
                {
                    rctrl_udp_translator.Send(line);
                    logger.Write(string.Format("{0} ({1}) << {2}", settings.rctrl_out_endpoint, "RCTRL_OUT", line));
                }
                catch (Exception ex)
                {
                    logger.Write(ex.ToString());
                }
            };

            logger.Write($"Initializing UDP remote control input RCTRL_IN on {settings.rctrl_in_endpoint}...");
            rctrl_udp_listener = new UDPListener(settings.rctrl_in_endpoint.Port);
            rctrl_udp_listener.StartListen();
            rctrl_udp_listener.DataReceivedHandler += (o, e) =>
            {
                var rctrl_cmd = Encoding.ASCII.GetString(e.Data);
                rctrlCmdProcessor.Process(rctrl_cmd);
                logger.Write(string.Format("{0} ({1}) >> {2}", settings.rctrl_in_endpoint, "RCTRL_IN", rctrl_cmd));
            };
        }

        azmCombiner = new AZMCombiner(settings);

        if (settings.webServerEnabled)
            webDataConverter = new WebDataConverter(azmCombiner);

        azmCombiner.AZMPreferredPortName = settings.azmPrefPortName;
        azmCombiner.OutputHandler += (o, e) =>
        {
            logger.WriteSilent($"<< {e.Line}");

            if (settings.webServerEnabled)
            {
                if ((webDataConverter != null) && (webServer != null && (DateTime.Now - lastWebUpdate).TotalMilliseconds > 100))
                {
                    var webData = webDataConverter.ConvertToWebData(e.Line);
                    webServer.UpdateData(webData);
                    lastWebUpdate = DateTime.Now;
                }
            }
        };
        azmCombiner.LogEventHandler += (o, e) =>
        {
            if (consoleLogOption == ConsoleLogOptions.Errors_only)
            {
                if (e.EventType == LogLineType.INFO)
                    logger.WriteSilent($"{e.EventType}: {e.LogString}");
                else
                    logger.Write($"{e.EventType}: {e.LogString}");
            }
            else if (consoleLogOption == ConsoleLogOptions.Enabled)
            {
                logger.Write($"{e.EventType}: {e.LogString}");
            }
        };
        azmCombiner.CREQResultHandler += (o, e) => azmCombiner.OutputHandler(azmCombiner,
            new StringEventArgs($"CREQR,{e.RemoteAddress},{e.ReqCode},{e.ResCode}"));
        azmCombiner.RSTSReceivedHandler += (o, e) => azmCombiner.OutputHandler(azmCombiner,
            new StringEventArgs($"RRA,{e.Addr}"));

        registrar.RegisterTerminalCommands(settings, terminalCmdProcessor, azmCombiner);
        registrar.RegisterRCTRLCommands(settings, rctrlCmdProcessor, azmCombiner);        

        logger.Write("Ready");

        while (!application_terminate)
        {
            var cmd = await inputProcessor.ReadCommandAsync();
            if (cmd != null)
                terminalCmdProcessor.Process(cmd);
        }

        if (settings.rctrl_enabled)
        {
            rctrlCmdProcessor.OnOutput("Application terminating...");
            rctrl_udp_listener?.StopListen();
        }

        azmCombiner?.Disconnect();
        azmCombiner?.Dispose();

        webServer?.Stop();
        webServer?.Dispose();

        logger.Flush();
        logger.FinishLog();
        inputProcessor.Dispose();
    }
}