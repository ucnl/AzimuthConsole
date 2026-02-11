
using AzimuthConsole;
using AzimuthConsole.AZM;
using System.Net;
using System.Reflection;
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

    private static void Main(string[] args)
    {
        bool application_terminate = false;

        ConsoleLogOptions consoleLogOption = ConsoleLogOptions.Enabled;

        LogPlayer? lPlayer = null;
        TSLogProvider? logger;
        SettingsContainer? settings = new();
        UDPListener? rctrl_udp_listener = null;
        UDPTranslator? rctrl_udp_translator = null;
        AZMCombiner? azmCombiner = null;
        ConsoleInputProcessor inputProcessor = new();
        CmdRegistrar registrar = new CmdRegistrar(azmCombiner, settings);        

        var logFileName = StrUtils.GetTimeDirTreeFileName(DateTime.Now, AppContext.BaseDirectory, "log", "log", true);

        logger = new TSLogProvider(logFileName);
        logger.TextAddedEvent += (o, e) => 
        { 
            if (consoleLogOption != ConsoleLogOptions.Disabled)
                Console.Write(e.Text); 
        };
        logger.WriteStart();
        logger.Write($"{Assembly.GetExecutingAssembly().GetName().Name} v{Assembly.GetExecutingAssembly().GetName().Version} (C) UC&NL, unavlab.com");
                
        CmdProcessor cmdLineArgProcessor = new();
        cmdLineArgProcessor.OutputMessageHandler += (line) => logger.Write(line);
        CmdProcessor terminalCmdProcessor = new();
        terminalCmdProcessor.OutputMessageHandler += (line) => logger.Write(line);
        CmdProcessor rctrlCmdProcessor = new();               

        lPlayer = new LogPlayer();
        lPlayer.NewLogLineHandler += (o, e) =>
        {
            /*
            if (e.TS != TimeSpan.Zero)
            {
                var now = DateTime.Now;
                emu_TS = new DateTime(now.Year, now.Month, now.Day, e.TS.Hours, e.TS.Minutes, e.TS.Seconds);
                is_emu_TS = true;
            }
            */

            if (e.Line.StartsWith("INFO"))
            {
                int idx = e.Line.IndexOf(' ');
                if (idx >= 0)
                {
                    azmCombiner.Emulate(e.Line.Substring(idx).Trim());
                }
            }
        };
        lPlayer.LogPlaybackFinishedHandler += (o, e) => azmCombiner.Disconnect();
        bool StartLogPlayBack(bool isInstant, string fileName)
        {
            if (lPlayer.IsRunning || azmCombiner.ConnectionActive ||
                !File.Exists(fileName))
                return false;
            else
            {
                bool ok = false;

                try
                {
                    lPlayer.Playback(fileName);
                    ok = true;
                }
                catch (Exception)
                {
                    
                }

                return ok;
            }  
        }
        bool StopLogPlayBack()
        {
            if (lPlayer.IsRunning)
            {
                lPlayer.RequestToStop();
                return true;
            }
            else
                return false;
        }

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
                result = StopLogPlayBack();
            }
            else
            {
                result = StartLogPlayBack(args[0] != null, lFileName);
            }
            
            return result;
        },
        "Playback a log file. PLAY,[reserved],[logFile].");

        registrar.RegisterCommandLineCommands(cmdLineArgProcessor);
        registrar.RegisterTerminalCommands(terminalCmdProcessor);
        registrar.RegisterRCTRLCommands(rctrlCmdProcessor);

        foreach (var arg in args)
        {
            cmdLineArgProcessor.Process(arg);
        }

        logger.Write($"\r\n- Application settings:{settings}");

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
            new ConsoleKeyInfo('\0', ConsoleKey.L, false, false, true),
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
            "Link: Open/Close connections (OCON/CCON)."
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
            new ConsoleKeyInfo('\0', ConsoleKey.E, false, false, true),
            () => { inputProcessor.EmulateCommand("exit"); },
            "Exit application."
        );

        Console.WriteLine();
        Console.WriteLine(inputProcessor.GetHotkeysDescription());

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

        azmCombiner = new AZMCombiner(settings.address_mask,
            settings.sty_PSU, settings.max_dist_m,
            settings.antenna_angular_offset_deg, settings.antenna_x_offset_m, settings.antenna_y_offset_m)
        {
            AZMPreferredPortName = settings.azmPrefPortName
        };
        azmCombiner.OutputHandler += (o, e) => logger.WriteSilent($"<< {e.Line}");
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

        if (settings.aux1Enabled)
        {
            azmCombiner.AUX1PreferredPortName = settings.aux1PrefPortName;
            azmCombiner.AUX1Init(settings.aux1PortBaudrate);
        }

        if (settings.aux2Enabled)
        {
            azmCombiner.AUX2PreferredPortName = settings.aux2PrefPortName;
            azmCombiner.AUX2Init(settings.aux2PortBaudrate);
        }

        if (settings.outputSerialEnabled)
        {
            logger.Write($"Initializing SERIAL_OUT on {settings.outputSerialPortName}...");
            azmCombiner.SerialOutputInit(settings.outputSerialPortName, settings.outputSerialPortBaudrate);
            azmCombiner.OutputHandler += (o, e) => azmCombiner.ToSerialOutput(e.Line);
        }

        if (settings.output_udp_enabled)
        {
            logger.Write($"Initializing UDP output UDP_OUT on {settings.output_endpoint}...");
            azmCombiner.UDPOutputInit(settings.output_endpoint);
            azmCombiner.OutputHandler += (o, e) => azmCombiner.ToUDPOutput(e.Line);
        }

        if (settings.LBLResponderCoordinatesMode == LBLResponderCoordinatesModeEnum.Cartesian)
        {
            azmCombiner.Set3RespondersLocalCoordinates(
                settings.LBLModeR1X, settings.LBLModeR1Y,
                settings.LBLModeR2X, settings.LBLModeR2Y,
                settings.LBLModeR3X, settings.LBLModeR3Y);
        }

        if ((settings.InvidvidualEndpoints != null) && (settings.InvidvidualEndpoints.Count > 0))
        {
            foreach (var item in settings.InvidvidualEndpoints)
            {
                logger.Write($"Initializing responder {item.Key} UDP output on {item.Value}...");
                azmCombiner.SetResponderIndividualUDPChannel(item.Key, item.Value);
            }
        }

        logger.Write("Ready");

        while (!application_terminate)
        {           
            var cmd = inputProcessor.ReadCommand();
            if (cmd != null)
                terminalCmdProcessor.Process(cmd);
        }

        if (settings.rctrl_enabled)
        {
            rctrlCmdProcessor.OnOutput("Application terminating...");
            rctrl_udp_listener?.StopListen();
        }

        azmCombiner.Disconnect();
        
        logger.Flush();
        logger.FinishLog();
        inputProcessor.Dispose();
    }
}