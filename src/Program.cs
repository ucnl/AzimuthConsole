
using AzimuthConsole;
using AzimuthConsole.AZM;
using System.Net;
using System.Reflection;
using System.Text;
using UCNLDrivers;

internal class Program
{
    private static void Main(string[] args)
    {
        bool application_terminate = false;

        TSLogProvider logger;
        SettingsContainer settings = new SettingsContainer();
        UDPListener rctrl_udp_listener = null;
        UDPTranslator rctrl_udp_translator = null;
        AZMCombiner azmCombiner = null;

        var logFileName = StrUtils.GetTimeDirTreeFileName(DateTime.Now, AppContext.BaseDirectory, "log", "log", true);

        logger = new TSLogProvider(logFileName);
        logger.TextAddedEvent += (o, e) => { Console.Write(e.Text); };
        logger.WriteStart();
        logger.Write(string.Format("{0} v{1} (C) UC&NL, unavlab.com", Assembly.GetExecutingAssembly().GetName().Name, Assembly.GetExecutingAssembly().GetName().Version));


        void RegisterGetInterrogationStateCommand(CmdProcessor proc, AZMCombiner combiner) =>
            proc.AddCommand("ITG?", "", args =>
            {
                proc.OnOutput($"ITG,{combiner?.InterrogationActive ?? false}");
                return true;
            }, "Get responders InTerroGation state. Returns as ITG,{true|false}.");

        void RegisterGetConnectionStateCommand(CmdProcessor proc, AZMCombiner combiner) =>
            proc.AddCommand("CNA?", "", args =>
            {
                proc.OnOutput($"CNA,{combiner?.ConnectionActive ?? false}");
                return true;
            }, "Check if CoNnection is Active. Returns as CNA,{true|false}.");

        void RegisterGetDetectedStateCommand(CmdProcessor proc, AZMCombiner combiner) =>
            proc.AddCommand("DET?", "c--c", args =>
            {
                var deviceID = CConv.O2S(args[0]);
                bool isDetected = combiner?.IsDeviceDetected(deviceID) ?? false;
                proc.OnOutput($"DET,{deviceID},{isDetected}");
                return true;

            }, "Check if specified device is DETected. DET?,AZM|AUX1|AUX2. Returns as DET,DeviceID,true|false.");

        void RegisterConnectCommand(CmdProcessor proc, AZMCombiner combiner) =>
            proc.AddCommand("OCON", "", args =>
            {
                bool result = false;
                try
                {
                    result = azmCombiner.Connect();
                }
                catch (Exception ex)
                {
                    proc.OnOutput(ex.ToString());
                }

                return result;
            }, "Open CONnections.");

        void RegisterDisconnectCommand(CmdProcessor proc, AZMCombiner combiner) =>
            proc.AddCommand("CCON", "", args =>
            {
                bool result = false;
                try
                {
                    result = azmCombiner.Disconnect();
                }
                catch (Exception ex)
                {
                    proc.OnOutput(ex.ToString());
                }

                return result;
            }, "Close CONnections.");

        void RegisterCREQCommand(CmdProcessor proc, AZMCombiner combiner) =>
            proc.AddCommand("CREQ", "x,x", args =>
            {
                REMOTE_ADDR_Enum remAddr = (REMOTE_ADDR_Enum)args[0];
                CDS_REQ_CODES_Enum reqCode = (CDS_REQ_CODES_Enum)args[1];

                if ((azmCombiner != null) && (remAddr != REMOTE_ADDR_Enum.REM_ADDR_INVALID) && (AZM.IsUserDataReqCode(reqCode)))
                {
                    return azmCombiner.CREQ(remAddr, reqCode);
                }
                else
                    return false;
            }, "Request custom user data value. Usage: CREQ,remAddr=1..16,reqCode=3..30.");

        void RegisterGetLocationAndHeadingOverrideCommand(CmdProcessor proc, AZMCombiner combiner) =>
            proc.AddCommand("LHO?", "", args =>
            {
                proc.OnOutput($"LHO,{combiner?.LocationOverrideEnabled ?? false}");
                return true;

            }, "Get Location and Heading Override feature status. Returns as LHO,true|false");

        void RegisterGetOutputFormatCommand(CmdProcessor proc, AZMCombiner combiner) =>
            proc.AddCommand("OFMT?", "", args =>
            {
                proc.OnOutput(
                    string.Format("Station local parameters:\r\n{0}\r\nRemote parameters:\r\n{1}",
                    combiner.GetStationParametersToStringFormat(),
                    (new ResponderBeacon(REMOTE_ADDR_Enum.REM_ADDR_1).GetToStringFormat())));

                return true;


            }, "Get output messages format description.");


        CmdProcessor cmdLineArgProcessor = new CmdProcessor();
        cmdLineArgProcessor.OutputMessageHandler += (line) => logger.Write(line);

        CmdProcessor terminalCmdProcessor = new CmdProcessor();
        terminalCmdProcessor.OutputMessageHandler += (line) => logger.Write(line);

        CmdProcessor rctrlCmdProcessor = new CmdProcessor();

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
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("- UDP remote control commands -");
            sb.AppendLine(rctrlCmdProcessor.GetCommandsDescriptions());

            rctrlCmdProcessor.OnOutput(sb.ToString());
            return true;
        },
        "Show help");

        cmdLineArgProcessor.AddCommand("SETM", "c--c,x,c--c,c--c,x,x.x,x", args =>
        {
            settings.azmPrefPortName = CConv.O2S_D(args[0], string.Empty);
            if (settings.azmPrefPortName.ToUpper() == "AUTO")
                settings.azmPrefPortName = string.Empty;

            settings.azmPortBaudrate = CConv.O2Baudrate_D(args[1], settings.azmPortBaudrate);

            settings.rctrl_enabled = (args[2] != null) && (IPEndPoint.TryParse(CConv.O2S_D(args[2], settings.rctrl_in_endpoint.ToString()), out settings.rctrl_in_endpoint)) &&
                            (args[3] != null) && (IPEndPoint.TryParse(CConv.O2S_D(args[3], settings.rctrl_out_endpoint.ToString()), out settings.rctrl_out_endpoint));

            settings.address_mask = CConv.O2U16_D(args[4], settings.address_mask);
            settings.sty_PSU = CConv.O2D_D(args[5], settings.sty_PSU);
            settings.max_dist_m = CConv.O2S32_D(args[6], settings.max_dist_m);

            return true;
        },
        "Set main parameters. SETM,azmPort|AUTO,[azmBaudrate],[rctrl_in_ip_addr:port],[rctrl_out_ip_addr:port],[addr_mask],[salinity_PSU],[max_dist_m]");

        cmdLineArgProcessor.AddCommand("SETA", "c--c,x,c--c,x", args =>
        {
            settings.aux1Enabled = args[0] != null;
            if (settings.aux1Enabled)
            {
                settings.aux1PrefPortName = CConv.O2S_D(args[0], settings.aux1PrefPortName);
                if (settings.aux1PrefPortName.ToUpper() == "AUTO")
                    settings.aux1PrefPortName = string.Empty;
                settings.aux1PortBaudrate = CConv.O2Baudrate_D(args[1], settings.aux1PortBaudrate);
            }

            settings.aux2Enabled = args[2] != null;
            if (settings.aux2Enabled)
            {
                settings.aux2PrefPortName = CConv.O2S_D(args[2], settings.aux2PrefPortName);
                if (settings.aux2PrefPortName.ToUpper() == "AUTO")
                    settings.aux2PrefPortName = string.Empty;
                settings.aux2PortBaudrate = CConv.O2Baudrate_D(args[3], settings.aux2PortBaudrate);
            }

            return true;
        },
        "Set aux ports parameters. SETA,[aux1Port|AUTO],[aux1Baudrate],[aux2Port|AUTO],[aux2Baudrate]");

        cmdLineArgProcessor.AddCommand("SETO", "c--c,x,c--c", args =>
        {
            settings.outputSerialEnabled = !string.IsNullOrEmpty((string)args[0]);
            if (settings.outputSerialEnabled)
            {
                settings.outputSerialPortName = CConv.O2S_D(args[0], settings.outputSerialPortName);
                settings.outputSerialPortBaudrate = CConv.O2Baudrate_D(args[1], settings.outputSerialPortBaudrate);
            }

            settings.output_udp_enabled = (args[2] != null) && (IPEndPoint.TryParse(CConv.O2S_D(args[2], settings.output_endpoint.ToString()), out settings.output_endpoint));

            return true;
        },
        "Set output parameters. SETO,[outPort],[outBaudrate],[out_ip_addr:port]");

        cmdLineArgProcessor.AddCommand("SARP", "x.x,x.x,x.x", args =>
        {
            settings.antenna_x_offset_m = CConv.O2D_D(args[0], settings.antenna_x_offset_m);
            settings.antenna_y_offset_m = CConv.O2D_D(args[1], settings.antenna_y_offset_m);
            settings.antenna_angular_offset_deg = CConv.O2D_D(args[2], settings.antenna_angular_offset_deg);

            return true;
        },
        "Set antenna's relative position. SARP,x_offset_m,y_offset_m,angular_offset_deg");

        terminalCmdProcessor.AddCommand("CLS", "", args => { Console.Clear(); return true; }, "Clear screen");
        terminalCmdProcessor.AddCommand("EXIT", "", args => { application_terminate = true; return true; }, "Exit AzimuthConsole");



        foreach (var arg in args)
        {
            cmdLineArgProcessor.Process(arg);
        }

        logger.Write("- Application settings:");
        logger.Write(settings.ToString());

        if (settings.rctrl_enabled)
        {
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
            settings.antenna_angular_offset_deg, settings.antenna_x_offset_m, settings.antenna_y_offset_m);
        azmCombiner.AZMPreferredPortName = settings.azmPrefPortName;

        azmCombiner.OutputHandler += (o, e) => logger.WriteSilent(string.Format("<< {0}", e.Line));
        azmCombiner.LogEventHandler += (o, e) =>
        {
            if (e.EventType == LogLineType.INFO)
                logger.WriteSilent(string.Format("{0}: {1}", e.EventType, e.LogString));
            else
                logger.Write(string.Format("{0}: {1}", e.EventType, e.LogString));
        };

        azmCombiner.CREQResultHandler += (o, e) => azmCombiner.OutputHandler(azmCombiner,
            new StringEventArgs(string.Format("CREQR,{0},{1},{2}", e.RemoteAddress, e.ReqCode, e.ResCode)));

        RegisterConnectCommand(terminalCmdProcessor, azmCombiner);
        RegisterConnectCommand(rctrlCmdProcessor, azmCombiner);
        RegisterDisconnectCommand(terminalCmdProcessor, azmCombiner);
        RegisterDisconnectCommand(rctrlCmdProcessor, azmCombiner);
        RegisterGetConnectionStateCommand(terminalCmdProcessor, azmCombiner);
        RegisterGetConnectionStateCommand(rctrlCmdProcessor, azmCombiner);
        RegisterGetInterrogationStateCommand(terminalCmdProcessor, azmCombiner);
        RegisterGetInterrogationStateCommand(rctrlCmdProcessor, azmCombiner);
        RegisterGetDetectedStateCommand(terminalCmdProcessor, azmCombiner);
        RegisterGetDetectedStateCommand(rctrlCmdProcessor, azmCombiner);
        RegisterCREQCommand(terminalCmdProcessor, azmCombiner);
        RegisterCREQCommand(rctrlCmdProcessor, azmCombiner);
        RegisterGetLocationAndHeadingOverrideCommand(terminalCmdProcessor, azmCombiner);
        RegisterGetLocationAndHeadingOverrideCommand(rctrlCmdProcessor, azmCombiner);
        RegisterGetOutputFormatCommand(terminalCmdProcessor, azmCombiner);
        RegisterGetOutputFormatCommand(rctrlCmdProcessor, azmCombiner);

        var tcmd = terminalCmdProcessor.AddCommand("PITG", "", args => azmCombiner?.PauseInterrogation() ?? false, "Pause responders interrogation.");
        rctrlCmdProcessor.AddCommand(tcmd);

        tcmd = terminalCmdProcessor.AddCommand("RITG", "", args => azmCombiner?.ResumeInterrogation() ?? false, "Resume responders interrogation.");
        rctrlCmdProcessor.AddCommand(tcmd);

        tcmd = terminalCmdProcessor.AddCommand("LHOV", "x.x,x.x,x.x", args =>
        {
            bool result = false;

            if (azmCombiner != null)
            {
                if ((args[0] == null) && (args[1] == null) && (args[2] == null))
                {
                    result = azmCombiner.LocationOverrideDisable();
                }
                else
                {
                    double lt = AZM.O2D(args[0]);
                    double ln = AZM.O2D(args[1]);
                    double hd = AZM.O2D(args[2]);

                    if (!double.IsNaN(lt) && !double.IsNaN(ln) && !double.IsNaN(hd) &&
                        AZM.IsLatDeg(lt) && AZM.IsLonDeg(ln) && AZM.IsLatDeg(hd))
                    {
                        result = azmCombiner.LocationOverrideEnable(AZM.O2D(args[0]), AZM.O2D(args[1]), AZM.O2D(args[2]));
                    }
                }
            }

            return result;
        },
        "Antenna's Location and Heading OVerride. LHOV,lat_deg,lon_deg,hdn_deg. All empty fields disable the feature.");

        rctrlCmdProcessor.AddCommand(tcmd);

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
            azmCombiner.SerialOutputInit(settings.outputSerialPortName, settings.outputSerialPortBaudrate);
            azmCombiner.OutputHandler += (o, e) => azmCombiner.ToSerialOutput(e.Line);
        }

        if (settings.output_udp_enabled)
        {
            azmCombiner.UDPOutputInit(settings.output_endpoint);
            azmCombiner.OutputHandler += (o, e) => azmCombiner.ToUDPOutput(e.Line);
        }

        while (!application_terminate)
        {
            var cmd = Console.ReadLine();
            if (cmd != null)
                terminalCmdProcessor.Process(cmd);
        }

        if (settings.rctrl_enabled)
        {
            if (rctrl_udp_listener != null)
                rctrl_udp_listener.StopListen();
        }

        logger.Flush();
        logger.FinishLog();
    }
}