using AZMLib;
using System.Net;
using System.Text;
using UCNLDrivers;

namespace AzimuthConsole;

public class CmdRegistrar
{
    private readonly CmdProcessor _cmdLineProcessor;
    private readonly CmdProcessor _terminalProcessor;
    private readonly CmdProcessor _rctrlProcessor;
    private readonly CmdProcessor _webProcessor;
    private readonly AZMSettingsContainer _azmSettings;
    private readonly ApplicationSettings _appSettings;
    private readonly AZMCombiner? _combiner;
    private readonly uRadantPort? _antennaRotator;
    private readonly CalibrationManager? _calibrationManager;
    private readonly ConsoleInputProcessor? _inputProcessor;
    private readonly ILogPlayer? _logPlayer;
    private readonly Func<bool>? _exitHandler;

    public CmdRegistrar(
        CmdProcessor cmdLineProcessor,
        CmdProcessor terminalProcessor,
        CmdProcessor rctrlProcessor,
        CmdProcessor webProcessor,
        AZMSettingsContainer azmSettings,
        ApplicationSettings appSettings,
        AZMCombiner? combiner = null,
        uRadantPort? antennaRotator = null,
        CalibrationManager calibrationManager = null,
        ConsoleInputProcessor? inputProcessor = null,
        ILogPlayer? logPlayer = null,
        Func<bool>? exitHandler = null)
    {
        _cmdLineProcessor = cmdLineProcessor;
        _terminalProcessor = terminalProcessor;
        _rctrlProcessor = rctrlProcessor;
        _webProcessor = webProcessor;
        _azmSettings = azmSettings;
        _appSettings = appSettings;
        _combiner = combiner;
        _antennaRotator = antennaRotator;
        _calibrationManager = calibrationManager;
        _inputProcessor = inputProcessor;
        _logPlayer = logPlayer;
        _exitHandler = exitHandler;
    }

    /// <summary>
    /// Регистрирует команды, которые не требуют AZMCombiner (только для парсинга аргументов командной строки)
    /// </summary>
    public void RegisterCommandLineOnlyCommands()
    {
        _cmdLineProcessor.AddCommand("HELP", "", _ =>
        {
            Console.WriteLine("--- Supported commands ---");
            Console.WriteLine("- Command line arguments -");
            Console.WriteLine(_cmdLineProcessor.GetCommandsDescriptions());
            return true;
        }, "Show help");

        _cmdLineProcessor.AddCommand("DAEMON", "", _ => true,
            "Run application in daemon mode.");

        _cmdLineProcessor.AddCommand("SETM", "c--c,x,c--c,c--c,x,x.x,x", args => SetMainParameters(args),
            "Set main parameters. SETM,azmPort|AUTO,[azmBaudrate],[rctrl_in_ip_addr:port],[rctrl_out_ip_addr:port],[addr_mask],[salinity_PSU],[max_dist_m]");

        _cmdLineProcessor.AddCommand("SET2", "x.x,", args => SetMainParameters2(args),
            "Set additional parameters. SET2,[speedOfSound_mps]");

        _cmdLineProcessor.AddCommand("AUX1A", "", _ => { _azmSettings.aux1Alternative = true; return true; },
            "Use this command to turn the alternative mode for AUX1 source.");

        _cmdLineProcessor.AddCommand("SETA", "c--c,x,c--c,x", args => SetAuxParameters(args),
            "Set aux ports parameters. SETA,[aux1Port|AUTO],[aux1Baudrate],[aux2Port|AUTO],[aux2Baudrate]");

        _cmdLineProcessor.AddCommand("SETO", "c--c,x,c--c", args => SetOutputParameters(args),
            "Set output parameters. SETO,[outPort],[outBaudrate],[out_ip_addr:port]");

        _cmdLineProcessor.AddCommand("SEAR", "c--c,x", args => SetAntennaRotatorParameters(args),
            "Set antenna rotator port parameters. SEAR,[port],[baudrate]"); 

        _cmdLineProcessor.AddCommand("SARP", "x.x,x.x,x.x", args => SetAntennaPosition(args),
            "Set antenna's relative position. SARP,x_offset_m,y_offset_m,angular_offset_deg");

        _cmdLineProcessor.AddCommand("FLTS", "x,x.x,x,x.x,x.x,x.x,x,x.x,x,x.x,x.x", args => ConfigureFilters(args),
            "Configure track filters. Usage: FLTS,usbl_sf_fifo,usbl_sf_thld,usbl_df_fifo,usbl_df_mspeed,usbl_df_thld,lbl_rer_thld,lbl_sf_fifo,lbl_sf_thld,lbl_df_fifo,lbl_df_mspeed,lbl_df_thld");

        _cmdLineProcessor.AddCommand("NWEB", "", _ => { _appSettings.webServerEnabled = false; return true; },
            "Disables built-in web-server.");

        _cmdLineProcessor.AddCommand("LCAL", "c--c", args => { _appSettings.antennaCalibrationTableFile = (string)args[0]; return true; },
            "Load antenna calibration table. LCAL,caltable.csv");

        _cmdLineProcessor.AddCommand("SIOC", "x,c--c", args => SetIndividualUDPOutputConfig(args),
            "Set responder's individual UDP output channel. Usage: SIOC,responderAddress,address:port");

        _cmdLineProcessor.AddCommand("SRC3", "x,x.x,x.x,x.x,x.x,x.x,x.x", args => SetRespondersCoordinatesConfig(args),
            "Set responders coordinates. Usage: SRC3,0-discard|1-cartesian|2-geographic,r1x,r1y,...,r3x,r3y");

        _cmdLineProcessor.AddCommand("SCLHOV", "x.x,x.x,x.x", args => SetCalibrationLHOV(args),
            "Set default LHOV for calibration. SCLHOV,lat_deg,lon_deg,hdg_deg. Empty values disable auto-LHOV.");

        _cmdLineProcessor.AddCommand("PSIMSSB", "c--c", args => SetHiPAPMode(args),
            "Enable/disable $PSIMSSB (HiPAP) output. Option is disabled by default. PSIMSSB,[ON|OFF]");
    }

    /// <summary>
    /// Регистрирует все команды (требует наличия AZMCombiner)
    /// </summary>
    public void RegisterAllCommands()
    {
        if (_combiner == null)
            throw new InvalidOperationException("AZMCombiner is required for RegisterAllCommands");
        
        RegisterTerminalCommands();
        RegisterRctrlCommands();
    }

    private void RegisterTerminalCommands()
    {
        _terminalProcessor.AddCommand("HELP", "", _ =>
        {
            Console.WriteLine("--- Supported commands ---");
            Console.WriteLine("- Command line arguments -");
            Console.WriteLine(_cmdLineProcessor.GetCommandsDescriptions());
            Console.WriteLine("- Terminal commands -");
            Console.WriteLine(_terminalProcessor.GetCommandsDescriptions());
            Console.WriteLine("- UDP remote control commands -");
            Console.WriteLine(_rctrlProcessor.GetCommandsDescriptions());
            return true;
        }, "Show help");

        _terminalProcessor.AddCommand("CLS", "", _ => { Console.Clear(); return true; }, "Clear screen");

        _terminalProcessor.AddCommand("EXIT", "", _ => _exitHandler?.Invoke() ?? false, "Exit AzimuthConsole");

        _terminalProcessor.AddCommand("HKEYS", "", _ =>
        {
            _terminalProcessor.OnOutput(_inputProcessor?.GetHotkeysDescription() ?? "No hotkeys available");
            return true;
        }, "Show hotkeys hint");

        _terminalProcessor.AddCommand("PLAY", "x,c--c", args => PlayLog(args),
            "Playback a log file. PLAY,[reserved],[logFile].");

        _terminalProcessor.AddCommand("OCON", "", _ => Connect(), "Open CONnections.");
        _terminalProcessor.AddCommand("CCON", "", _ => Disconnect(), "Close CONnections.");

        _terminalProcessor.AddCommand("CNA?", "", _ =>
        {
            _terminalProcessor.OnOutput($"CNA,{_combiner!.ConnectionActive}");
            return true;
        }, "Check if CoNnection is Active. Returns as CNA,{true|false}.");

        _terminalProcessor.AddCommand("ITG?", "", _ =>
        {
            _terminalProcessor.OnOutput($"ITG,{_combiner!.InterrogationActive}");
            return true;
        }, "Get responders InTerroGation state. Returns as ITG,{true|false}.");

        _terminalProcessor.AddCommand("RITG", "", _ => _combiner!.ResumeInterrogation(), "Resume responders interrogation.");
        _terminalProcessor.AddCommand("PITG", "", _ => _combiner!.PauseInterrogation(), "Pause responders interrogation.");

        _terminalProcessor.AddCommand("DET?", "c--c", args => GetDetectedState(args),
            "Check if specified device is DETected. DET?,AZM|AUX1|AUX2. Returns as DET,DeviceID,true|false.");

        _terminalProcessor.AddCommand("CREQ", "x,x", args => SendCREQ(args),
            "Request custom user data value. Usage: CREQ,remAddr=1..16,reqCode=3..30.");

        _terminalProcessor.AddCommand("RRA?", "", _ => _combiner!.QueryLocalAddress(),
            "Request current responder address. Usage: RRA?");

        _terminalProcessor.AddCommand("SRRA", "x", args => _combiner!.QueryLocalAddressSet((REMOTE_ADDR_Enum)args[0]),
            "Request to set responder address. Usage: SRRA,address");

        _terminalProcessor.AddCommand("SIOC", "x,c--c", args => SetIndividualUDPOutput(args),
            "Set responder's individual UDP output channel. Usage: SIOC,responderAddress,address:port");

        _terminalProcessor.AddCommand("SRC3", "x,x.x,x.x,x.x,x.x,x.x,x.x", args => SetRespondersCoordinates(args),
            "Set responders coordinates. Usage: SRC3,0-discard|1-cartesian|2-geographic,r1x,r1y,...,r3x,r3y");

        _terminalProcessor.AddCommand("LHOV", "x.x,x.x,x.x", args => LocationAndHeadingOverride(args),
            "Antenna's Location and Heading OVerride. LHOV,lat_deg,lon_deg,hdn_deg. All empty fields disable the feature.");

        _terminalProcessor.AddCommand("LHO?", "", _ =>
        {
            _terminalProcessor.OnOutput($"LHO,{_combiner!.LocationOverrideEnabled}");
            return true;
        }, "Get Location and Heading Override feature status. Returns as LHO,true|false");

        _terminalProcessor.AddCommand("OFMT?", "", _ => GetOutputFormat(),
            "Get output messages format description.");

        _terminalProcessor.AddCommand("SETS?", "", _ =>
        {
            _terminalProcessor.OnOutput($"\r\n- AZM core settings:{_azmSettings}\r\n- Application settings:{_appSettings}");
            return true;
        }, "Shows application settings.");



        _terminalProcessor.AddCommand("SCAL", "x.x,x.x,x", args => StartCalibration(args), 
            "Start calibration. SCAL,[startAngle_deg],[stepAngle_deg],[measurementsPerPoint]. Defaults: 0,15,20");

        _terminalProcessor.AddCommand("FCAL", "", _ => FinishCalibration(),
            "Finish/abort calibration.");

        _terminalProcessor.AddCommand("CAL?", "", _ => GetCalibrationStatus(),
            "Get calibration status. Returns CAL,state,currentAngle,totalPoints");
    }

    private void RegisterRctrlCommands()
    {
        _rctrlProcessor.AddCommand("HELP", "", args =>
        {
            var sb = new StringBuilder();
            sb.AppendLine("- UDP remote control commands -");
            sb.AppendLine(_rctrlProcessor.GetCommandsDescriptions());
            _rctrlProcessor.OnOutput(sb.ToString());
            return true;
        }, "Show help");

        _rctrlProcessor.AddCommand("OCON", "", _ => Connect(), "Open CONnections.");
        _rctrlProcessor.AddCommand("CCON", "", _ => Disconnect(), "Close CONnections.");

        _rctrlProcessor.AddCommand("CNA?", "", _ =>
        {
            _rctrlProcessor.OnOutput($"CNA,{_combiner!.ConnectionActive}");
            return true;
        }, "Check if CoNnection is Active. Returns as CNA,{true|false}.");

        _rctrlProcessor.AddCommand("ITG?", "", _ =>
        {
            _rctrlProcessor.OnOutput($"ITG,{_combiner!.InterrogationActive}");
            return true;
        }, "Get responders InTerroGation state. Returns as ITG,{true|false}.");

        _rctrlProcessor.AddCommand("RITG", "", _ => _combiner!.ResumeInterrogation(), "Resume responders interrogation.");
        _rctrlProcessor.AddCommand("PITG", "", _ => _combiner!.PauseInterrogation(), "Pause responders interrogation.");

        _rctrlProcessor.AddCommand("DET?", "c--c", args => GetDetectedStateForRctrl(args),
            "Check if specified device is DETected. DET?,AZM|AUX1|AUX2. Returns as DET,DeviceID,true|false.");

        _rctrlProcessor.AddCommand("CREQ", "x,x", args => SendCREQForRctrl(args),
            "Request custom user data value. Usage: CREQ,remAddr=1..16,reqCode=3..30.");

        _rctrlProcessor.AddCommand("RRA?", "", _ => _combiner!.QueryLocalAddress(),
            "Request current responder address. Usage: RRA?");

        _rctrlProcessor.AddCommand("SRRA", "x", args => _combiner!.QueryLocalAddressSet((REMOTE_ADDR_Enum)args[0]),
            "Request to set responder address. Usage: SRRA,address");

        _rctrlProcessor.AddCommand("SIOC", "x,c--c", args => SetIndividualUDPOutput(args),
            "Set responder's individual UDP output channel. Usage: SIOC,responderAddress,address:port");

        _rctrlProcessor.AddCommand("SRC3", "x,x.x,x.x,x.x,x.x,x.x,x.x", args => SetRespondersCoordinates(args),
            "Set responders coordinates. Usage: SRC3,0-discard|1-cartesian|2-geographic,r1x,r1y,...,r3x,r3y");

        _rctrlProcessor.AddCommand("LHOV", "x.x,x.x,x.x", args => LocationAndHeadingOverride(args),
            "Antenna's Location and Heading OVerride. LHOV,lat_deg,lon_deg,hdn_deg. All empty fields disable the feature.");

        _rctrlProcessor.AddCommand("LHO?", "", _ =>
        {
            _rctrlProcessor.OnOutput($"LHO,{_combiner!.LocationOverrideEnabled}");
            return true;
        }, "Get Location and Heading Override feature status. Returns as LHO,true|false");

        _rctrlProcessor.AddCommand("OFMT?", "", _ => GetOutputFormatForRctrl(),
            "Get output messages format description.");


        _rctrlProcessor.AddCommand("SCAL", "x.x,x.x,x", args => StartCalibration(args),
            "Start calibration. SCAL,[startAngle_deg],[stepAngle_deg],[measurementsPerPoint]");

        _rctrlProcessor.AddCommand("FCAL", "", _ => FinishCalibration(),
            "Finish/abort calibration.");

        _rctrlProcessor.AddCommand("CAL?", "", _ => GetCalibrationStatusForRctrl(),
            "Get calibration status.");

    }

    public void RegisterWebCommands()
    {
        // Только базовые команды управления
        _webProcessor.AddCommand("OCON", "", _ => Connect(),
            "Open connection");

        _webProcessor.AddCommand("CCON", "", _ => Disconnect(),
            "Close connection");

        _webProcessor.AddCommand("RITG", "", _ => _combiner!.ResumeInterrogation(),
            "Resume interrogation");

        _webProcessor.AddCommand("PITG", "", _ => _combiner!.PauseInterrogation(),
            "Pause interrogation");

        _webProcessor.AddCommand("CNA?", "", _ =>
        {
            _webProcessor.OnOutput($"CNA,{_combiner!.ConnectionActive}");
            return true;
        }, "Connection status");

        _webProcessor.AddCommand("ITG?", "", _ =>
        {
            _webProcessor.OnOutput($"ITG,{_combiner!.InterrogationActive}");
            return true;
        }, "Interrogation status");


        if (_antennaRotator != null)
        {
            _webProcessor.AddCommand("SCAL", "", _ => StartCalibration([null, null, null]),
                "Start calibration");
            _webProcessor.AddCommand("FCAL", "", _ => FinishCalibration(),
                "Stop calibration");
            _webProcessor.AddCommand("CAL?", "", _ => GetCalibrationStatus(),
                "Get calibration status");
        }
    }

    // ===== HELPER METHODS (без combiner, для конфигурации) =====

    private bool SetIndividualUDPOutputConfig(object[] args)
    {
        var radd = AZM.O2_REMOTE_ADDR_Enum(args[0]);
        var discard = args[1] == null;
        if (radd == REMOTE_ADDR_Enum.REM_ADDR_INVALID) return false;
        if (discard) return _azmSettings.InvidvidualEndpoints.Remove(radd);
        if (!IPEndPoint.TryParse(CConv.O2S(args[1]), out var newEP)) return false;
        return _azmSettings.InvidvidualEndpoints.TryAdd(radd, newEP);
    }

    private bool SetRespondersCoordinatesConfig(object[] args)
    {
        var mode = args[0] == null ? LBLResponderCoordinatesModeEnum.None : (LBLResponderCoordinatesModeEnum)(int)args[0];
        var coords = new[]
        {
            (X: AZM.O2D(args[1]), Y: AZM.O2D(args[2])),
            (X: AZM.O2D(args[3]), Y: AZM.O2D(args[4])),
            (X: AZM.O2D(args[5]), Y: AZM.O2D(args[6]))
        };

        _azmSettings.LBLResponderCoordinatesMode = mode;
        if (mode != LBLResponderCoordinatesModeEnum.None)
        {
            _azmSettings.LBLModeR1Coordinates = (coords[0].X, coords[0].Y);
            _azmSettings.LBLModeR2Coordinates = (coords[1].X, coords[1].Y);
            _azmSettings.LBLModeR3Coordinates = (coords[2].X, coords[2].Y);
        }
        return true;
    }

    // ===== HELPER METHODS (с combiner) =====

    private bool Connect()
    {
        try { return _combiner!.Connect(); }
        catch (Exception ex) { _terminalProcessor.OnOutput(ex.ToString()); return false; }
    }

    private bool Disconnect()
    {
        try 
        {
            _antennaRotator?.Stop();
            return _combiner!.Disconnect();
        }
        catch (Exception ex) { _terminalProcessor.OnOutput(ex.ToString()); return false; }
    }

    private bool SetMainParameters2(object[] args)
    {
        _azmSettings.speedOfSound = CConv.O2D_D(args[0], _azmSettings.speedOfSound);
        return true;
    }

    private bool SetMainParameters(object[] args)
    {
        _azmSettings.azmPrefPortName = CConv.O2S_D(args[0], string.Empty);
        if (_azmSettings.azmPrefPortName.ToUpper() == "AUTO")
            _azmSettings.azmPrefPortName = string.Empty;
        _azmSettings.azmPortBaudrate = CConv.O2Baudrate_D(args[1], _azmSettings.azmPortBaudrate);

        _appSettings.rctrl_enabled = args[2] != null &&
            IPEndPoint.TryParse(CConv.O2S_D(args[2], _appSettings.rctrl_in_endpoint.ToString()), out _appSettings.rctrl_in_endpoint) &&
            args[3] != null && IPEndPoint.TryParse(CConv.O2S_D(args[3], _appSettings.rctrl_out_endpoint.ToString()), out _appSettings.rctrl_out_endpoint);

        _azmSettings.address_mask = CConv.O2U16_D(args[4], _azmSettings.address_mask);
        _azmSettings.sty_PSU = CConv.O2D_D(args[5], _azmSettings.sty_PSU);
        _azmSettings.max_dist_m = CConv.O2S32_D(args[6], _azmSettings.max_dist_m);

        return true;
    }

    private bool SetAuxParameters(object[] args)
    {
        _azmSettings.aux1Enabled = args[0] != null;
        if (_azmSettings.aux1Enabled)
        {
            _azmSettings.aux1PrefPortName = CConv.O2S_D(args[0], _azmSettings.aux1PrefPortName);
            if (_azmSettings.aux1PrefPortName.ToUpper() == "AUTO")
                _azmSettings.aux1PrefPortName = string.Empty;
            _azmSettings.aux1PortBaudrate = CConv.O2Baudrate_D(args[1], _azmSettings.aux1PortBaudrate);
        }
        _azmSettings.aux2Enabled = args[2] != null;
        if (_azmSettings.aux2Enabled)
        {
            _azmSettings.aux2PrefPortName = CConv.O2S_D(args[2], _azmSettings.aux2PrefPortName);
            if (_azmSettings.aux2PrefPortName.ToUpper() == "AUTO")
                _azmSettings.aux2PrefPortName = string.Empty;
            _azmSettings.aux2PortBaudrate = CConv.O2Baudrate_D(args[3], _azmSettings.aux2PortBaudrate);
        }
        return true;
    }

    private bool SetOutputParameters(object[] args)
    {
        _azmSettings.outputSerialEnabled = !string.IsNullOrEmpty((string)args[0]);
        if (_azmSettings.outputSerialEnabled)
        {
            _azmSettings.outputSerialPortName = CConv.O2S_D(args[0], _azmSettings.outputSerialPortName);
            _azmSettings.outputSerialPortBaudrate = CConv.O2Baudrate_D(args[1], _azmSettings.outputSerialPortBaudrate);
        }
        _azmSettings.output_udp_enabled = args[2] != null &&
            IPEndPoint.TryParse(CConv.O2S_D(args[2], _azmSettings.output_endpoint.ToString()), out _azmSettings.output_endpoint);
        return true;
    }

    private bool SetAntennaRotatorParameters(object[] args)
    {
        _appSettings.antennaRotatorEnabled = args[0] != null;
        if (_appSettings.antennaRotatorEnabled)
        {
            _appSettings.antennaRotatorPortName = CConv.O2S_D(args[0], _appSettings.antennaRotatorPortName).ToUpper();

            if (_appSettings.antennaRotatorPortName == "AUTO")
                _appSettings.antennaRotatorPortName = string.Empty;
            
                _appSettings.antennaRotatorPortBaudrate = CConv.O2Baudrate_D(args[1], _appSettings.antennaRotatorPortBaudrate);
        }
        return true;
    }

    private bool SetAntennaPosition(object[] args)
    {
        _azmSettings.antenna_x_offset_m = CConv.O2D_D(args[0], _azmSettings.antenna_x_offset_m);
        _azmSettings.antenna_y_offset_m = CConv.O2D_D(args[1], _azmSettings.antenna_y_offset_m);
        _azmSettings.antenna_angular_offset_deg = CConv.O2D_D(args[2], _azmSettings.antenna_angular_offset_deg);
        return true;
    }

    private bool ConfigureFilters(object[] args)
    {
        var usbl_sf_fifo = args[0] != null ? AZM.O2S32(args[0]) : _azmSettings.USBLMode_SFilter_FIFO_Size;
        var usbl_sf_thld = args[1] != null ? AZM.O2D(args[1]) : _azmSettings.USBLMode_SFilter_Threshold;
        var usbl_df_fifo = args[2] != null ? AZM.O2S32(args[2]) : _azmSettings.USBLMode_DHFilter_FIFO_Size;
        var usbl_df_mspeed = args[3] != null ? AZM.O2D(args[3]) : _azmSettings.USBLMode_DHFilter_MaxSpeed_mps;
        var usbl_df_thld = args[4] != null ? AZM.O2D(args[4]) : _azmSettings.USBLMode_DHFilter_Threshold_m;
        var lbl_rer_thld = args[5] != null ? AZM.O2D(args[5]) : _azmSettings.LBLMode_RErr_Threshold_m;

        if (args[6] != null && args[7] != null)
        {
            _azmSettings.LBLMode_SFilter_FIFO_Size = AZM.O2S32(args[6]);
            _azmSettings.LBLMode_SFilter_Threshold_m = AZM.O2D(args[7]);
            _azmSettings.LBLMode_Use_SFilter = true;
        }
        else _azmSettings.LBLMode_Use_SFilter = false;

        if (args[8] != null && args[9] != null && args[10] != null)
        {
            _azmSettings.LBLMode_DHFilter_FIFO_Size = AZM.O2S32(args[8]);
            _azmSettings.LBLMode_DHFilter_MaxSpeed_mps = AZM.O2D(args[9]);
            _azmSettings.LBLMode_DHFilter_Threshold_m = AZM.O2D(args[10]);
            _azmSettings.LBLMode_Use_DHFilter = true;
        }
        else _azmSettings.LBLMode_Use_DHFilter = false;

        _azmSettings.USBLMode_SFilter_FIFO_Size = usbl_sf_fifo;
        _azmSettings.USBLMode_SFilter_Threshold = usbl_sf_thld;
        _azmSettings.USBLMode_DHFilter_FIFO_Size = usbl_df_fifo;
        _azmSettings.USBLMode_DHFilter_MaxSpeed_mps = usbl_df_mspeed;
        _azmSettings.USBLMode_DHFilter_Threshold_m = usbl_df_thld;
        _azmSettings.LBLMode_RErr_Threshold_m = lbl_rer_thld;
        return true;
    }

    private bool SetIndividualUDPOutput(object[] args)
    {
        var radd = AZM.O2_REMOTE_ADDR_Enum(args[0]);
        var discard = args[1] == null;
        if (radd == REMOTE_ADDR_Enum.REM_ADDR_INVALID) return false;
        if (discard) return _combiner!.DiscardResponderInvidualUPDChannel(radd);
        if (!IPEndPoint.TryParse(CConv.O2S(args[1]), out var newEP)) return false;
        return _combiner!.SetResponderIndividualUDPChannel(radd, newEP);
    }

    private bool SetRespondersCoordinates(object[] args)
    {
        var mode = args[0] == null ? LBLResponderCoordinatesModeEnum.None : (LBLResponderCoordinatesModeEnum)(int)args[0];
        var coords = new[]
        {
            (X: AZM.O2D(args[1]), Y: AZM.O2D(args[2])),
            (X: AZM.O2D(args[3]), Y: AZM.O2D(args[4])),
            (X: AZM.O2D(args[5]), Y: AZM.O2D(args[6]))
        };

        if (mode == LBLResponderCoordinatesModeEnum.Cartesian)
        {
            return _combiner!.Set3RespondersLocalCoordinates(coords[0].X, coords[0].Y, coords[1].X, coords[1].Y, coords[2].X, coords[2].Y);
        }
        if (mode == LBLResponderCoordinatesModeEnum.Geographic)
        {
            return _combiner!.Set3RespondersGeographicCoordinates(coords[0].X, coords[0].Y, coords[1].X, coords[1].Y, coords[2].X, coords[2].Y);
        }
        return _combiner!.Discard3RespondersCoordinates();
    }

    private bool LocationAndHeadingOverride(object[] args)
    {
        if (args[0] == null && args[1] == null && args[2] == null)
            return _combiner!.LocationOverrideDisable();
        var lat = AZM.O2D(args[0]);
        var lon = AZM.O2D(args[1]);
        var hdg = AZM.O2D(args[2]);
        if (!double.IsNaN(lat) && !double.IsNaN(lon) && !double.IsNaN(hdg) &&
            AZM.IsLatDeg(lat) && AZM.IsLonDeg(lon) && AZM.IsLatDeg(hdg))
            return _combiner!.LocationOverrideEnable(lat, lon, hdg);
        return false;
    }

    private bool GetOutputFormat()
    {
        _terminalProcessor.OnOutput(string.Format("Station local parameters:\r\n{0}\r\nRemote parameters:\r\n{1}",
            _combiner!.GetStationParametersToStringFormat(),
            new ResponderBeacon(REMOTE_ADDR_Enum.REM_ADDR_1).GetToStringFormat()));
        return true;
    }

    private bool GetOutputFormatForRctrl()
    {
        _rctrlProcessor.OnOutput(string.Format("Station local parameters:\r\n{0}\r\nRemote parameters:\r\n{1}",
            _combiner!.GetStationParametersToStringFormat(),
            new ResponderBeacon(REMOTE_ADDR_Enum.REM_ADDR_1).GetToStringFormat()));
        return true;
    }

    private bool GetDetectedState(object[] args)
    {
        var deviceID = CConv.O2S(args[0]);
        bool isDetected;

        if (deviceID == "RDT")
            isDetected = _antennaRotator?.Detected == true;
        else
            isDetected = _combiner!.IsDeviceDetected(deviceID);

        _terminalProcessor.OnOutput($"DET,{deviceID},{isDetected}");
        return true;
    }

    private bool GetDetectedStateForRctrl(object[] args)
    {
        var deviceID = CConv.O2S(args[0]);
        bool isDetected;

        if (deviceID == "RDT")
            isDetected = _antennaRotator?.Detected == true;
        else
            isDetected = _combiner!.IsDeviceDetected(deviceID);

        _rctrlProcessor.OnOutput($"DET,{deviceID},{isDetected}");
        return true;
    }

    private bool SendCREQ(object[] args)
    {
        var remAddr = (REMOTE_ADDR_Enum)args[0];
        var reqCode = (CDS_REQ_CODES_Enum)args[1];
        if (remAddr != REMOTE_ADDR_Enum.REM_ADDR_INVALID && AZM.IsUserDataReqCode(reqCode))
            return _combiner!.CREQ(remAddr, reqCode);
        return false;
    }

    private bool SendCREQForRctrl(object[] args)
    {
        var remAddr = (REMOTE_ADDR_Enum)args[0];
        var reqCode = (CDS_REQ_CODES_Enum)args[1];
        if (remAddr != REMOTE_ADDR_Enum.REM_ADDR_INVALID && AZM.IsUserDataReqCode(reqCode))
            return _combiner!.CREQ(remAddr, reqCode);
        return false;
    }

    private bool PlayLog(object[] args)
    {
        var fileName = AZM.O2S(args[1]);
        if (string.IsNullOrEmpty(fileName))
            return _logPlayer?.StopLogPlayBack() ?? false;
        return _logPlayer?.StartLogPlayBack(args[0] != null, fileName) ?? false;
    }


    private bool StartCalibration(object[] args)
    {
        if (_calibrationManager == null)
        {
            _terminalProcessor.OnOutput("SCAL,ERROR,Antenna rotator not configured");
            return false;
        }

        double startAngle = args[0] != null ? (double)args[0] : 0.0;
        double stepAngle = args[1] != null ? (double)args[1] : 15.0;
        int measurements = args[2] != null ? (int)args[2] : 20;

        if (stepAngle <= 0 || measurements <= 0)
        {
            _terminalProcessor.OnOutput("SCAL,ERROR,Invalid parameters");
            return false;
        }

        if (!_combiner.LocationOverrideEnabled)
        {
            if (!double.IsNaN(_appSettings.calibrationDefaultLat) &&
                !double.IsNaN(_appSettings.calibrationDefaultLon) &&
                !double.IsNaN(_appSettings.calibrationDefaultHdg))
            {
                _combiner.LocationOverrideEnable(
                    _appSettings.calibrationDefaultLat,
                    _appSettings.calibrationDefaultLon,
                    _appSettings.calibrationDefaultHdg);
            }
            else
            {
                _terminalProcessor.OnOutput("SCAL,ERROR,LHOV required. Use SCLHOV or LHOV command.");
                return false;
            }
        }

        try
        {
            _calibrationManager.Start(startAngle, stepAngle, measurements);
            _terminalProcessor.OnOutput($"SCAL,OK,{startAngle:F1},{stepAngle:F1},{measurements}");
            return true;
        }
        catch (Exception ex)
        {
            _terminalProcessor.OnOutput($"SCAL,ERROR,{ex.Message}");
            return false;
        }
    }

    private bool FinishCalibration()
    {
        if (_calibrationManager == null)
        {
            _terminalProcessor.OnOutput("FCAL,ERROR,Antenna rotator not configured");
            return false;
        }

        try
        {
            _calibrationManager.Stop();
            _terminalProcessor.OnOutput("FCAL,OK");
            return true;
        }
        catch (Exception ex)
        {
            _terminalProcessor.OnOutput($"FCAL,ERROR,{ex.Message}");
            return false;
        }
    }

    private bool GetCalibrationStatus()
    {
        if (_calibrationManager == null)
        {
            _terminalProcessor.OnOutput("CAL,not_configured");
            return true;
        }

        _terminalProcessor.OnOutput($"CAL,{_calibrationManager.State},{_calibrationManager.CalibrationPairs.Count}");
        return true;
    }

    private bool GetCalibrationStatusForRctrl()
    {
        if (_calibrationManager == null)
        {
            _rctrlProcessor.OnOutput("CAL,not_configured");
            return true;
        }

        _rctrlProcessor.OnOutput($"CAL,{_calibrationManager.State},{_calibrationManager.CalibrationPairs.Count}");
        return true;
    }

    private bool SetCalibrationLHOV(object[] args)
    {
        if (args[0] == null && args[1] == null && args[2] == null)
        {            
            _appSettings.calibrationDefaultLat = double.NaN;
            _appSettings.calibrationDefaultLon = double.NaN;
            _appSettings.calibrationDefaultHdg = double.NaN;
            return true;
        }

        double lat = AZM.O2D(args[0]);
        double lon = AZM.O2D(args[1]);
        double hdg = AZM.O2D(args[2]);

        if (!double.IsNaN(lat) && AZM.IsLatDeg(lat))
            _appSettings.calibrationDefaultLat = lat;
        if (!double.IsNaN(lon) && AZM.IsLonDeg(lon))
            _appSettings.calibrationDefaultLon = lon;
        if (!double.IsNaN(hdg) && AZM.IsHdnDeg(hdg))
            _appSettings.calibrationDefaultHdg = hdg;

        return true;
    }

    private bool SetHiPAPMode(object[] args)
    {
        string val = CConv.O2S_D(args[0], "OFF").Trim().ToUpperInvariant();
        _azmSettings.IsPSIMSSBOutputEnabled = val == "ON" || val == "1" || val == "TRUE";
        return true;
    }

}