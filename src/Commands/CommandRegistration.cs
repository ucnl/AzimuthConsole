// AzimuthConsole/Commands/CommandRegistration.cs
using System.Globalization;

namespace AzimuthConsole.Commands
{
    public static class CommandRegistration
    {
        public static void RegisterAll(CommandRouter router, ApplicationRuntime runtime)
        {
            RegisterConnectionCommands(router, runtime);
            RegisterInterrogationCommands(router, runtime);
            RegisterPortCommands(router, runtime);
            RegisterTransceiverCommands(router, runtime);
            RegisterPositionCommands(router, runtime);
            RegisterBeaconCommands(router, runtime);
            RegisterCalibrationCommands(router, runtime);
            RegisterOutputCommands(router, runtime);
            RegisterLogCommands(router, runtime);
            RegisterServiceCommands(router, runtime);
        }

        private static void RegisterInterrogationCommands(CommandRouter router, ApplicationRuntime runtime)
        {
            router.Register(new CommandMeta
            {
                Id = "ITG?",
                Category = "Interrogation",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "ITG?,OK,active=TRUE/FALSE",
                Description = "Check interrogation status"
            }, async (args, ctx) =>
            {
                return CommandResult.Ok("active", runtime.InterrogationActive ? "TRUE" : "FALSE");
            });

            router.Register(new CommandMeta
            {
                Id = "RITG",
                Category = "Interrogation",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "RITG,OK",
                Description = "Resume responders interrogation"
            }, async (args, ctx) =>
            {
                runtime.ResumeInterrogation();
                return CommandResult.Ok();
            });

            router.Register(new CommandMeta
            {
                Id = "PITG",
                Category = "Interrogation",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "PITG,OK",
                Description = "Pause responders interrogation"
            }, async (args, ctx) =>
            {
                runtime.PauseInterrogation();
                return CommandResult.Ok();
            });
        }

        private static void RegisterPortCommands(CommandRouter router, ApplicationRuntime runtime)
        {
            router.Register(new CommandMeta
            {
                Id = "AZM",
                Category = "Ports",
                Sources = "T,R,W",
                Parameters = "port=COMx/AUTO,baud=N",
                Response = "AZM,OK",
                Description = "Configure AZM transceiver port"
            }, async (args, ctx) =>
                await runtime.ConfigurePortAsync("azm", args));

            router.Register(new CommandMeta
            {
                Id = "AUX1",
                Category = "Ports",
                Sources = "T,R,W",
                Parameters = "proto=NMEA/BP,port=COMx/AUTO/OFF,baud=N",
                Response = "AUX1,OK",
                Description = "Configure AUX1 port (GNSS or BP)"
            }, async (args, ctx) =>
                await runtime.ConfigurePortAsync("aux1", args));

            router.Register(new CommandMeta
            {
                Id = "AUX2",
                Category = "Ports",
                Sources = "T,R,W",
                Parameters = "port=COMx/AUTO/OFF,baud=N",
                Response = "AUX2,OK",
                Description = "Configure AUX2 magnetic compass port"
            }, async (args, ctx) =>
                await runtime.ConfigurePortAsync("aux2", args));

            router.Register(new CommandMeta
            {
                Id = "RDT",
                Category = "Ports",
                Sources = "T,R,W",
                Parameters = "port=COMx/AUTO/OFF,baud=N",
                Response = "RDT,OK",
                Description = "Configure antenna rotator port"
            }, async (args, ctx) =>
                await runtime.ConfigurePortAsync("rdt", args));

            router.Register(new CommandMeta
            {
                Id = "OUTS",
                Category = "Ports",
                Sources = "T,R,W",
                Parameters = "port=COMx/OFF,baud=N",
                Response = "OUTS,OK",
                Description = "Configure serial data output"
            }, async (args, ctx) =>
                await runtime.ConfigurePortAsync("outs", args));

            router.Register(new CommandMeta
            {
                Id = "OUTU",
                Category = "Ports",
                Sources = "T,R,W",
                Parameters = "addr=ip:port/OFF",
                Response = "OUTU,OK",
                Description = "Configure UDP broadcast data output"
            }, async (args, ctx) =>
                await runtime.ConfigurePortAsync("outu", args));

            router.Register(new CommandMeta
            {
                Id = "SIOC",
                Category = "Ports",
                Sources = "T,R,W",
                Parameters = "addr=N,ep=ip:port/OFF",
                Response = "SIOC,OK",
                Description = "Set individual UDP output channel for beacon"
            }, async (args, ctx) =>
                await runtime.ConfigurePortAsync("sioc", args));

            router.Register(new CommandMeta
            {
                Id = "RCTRL",
                Category = "Ports",
                Sources = "T,R,W",
                Parameters = "in=port,out=ip:port",
                Response = "RCTRL,OK",
                Description = "Configure remote control UDP channels"
            }, async (args, ctx) =>
                await runtime.ConfigurePortAsync("rctrl", args));

            router.Register(new CommandMeta
            {
                Id = "PORTS",
                Category = "Ports",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "PORTS,OK,port0=id|port|status,...",
                Description = "Show all ports status"
            }, async (args, ctx) =>
            {
                var info = runtime.GetAllPortsInfo().ToList();
                var data = new Dictionary<string, string>();
                for (int i = 0; i < info.Count; i++)
                    data[$"port{i}"] = info[i];
                return CommandResult.Ok(data);
            });
        }

        private static void RegisterTransceiverCommands(CommandRouter router, ApplicationRuntime runtime)
        {
            router.Register(new CommandMeta
            {
                Id = "MSK",
                Category = "Transceiver",
                Sources = "T,R,W",
                Parameters = "mask=N",
                Response = "MSK,OK,mask=N",
                Description = "Get/set address mask (restarts interrogation on change)"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("mask", out var val))
                {
                    runtime.UpdateAddressMask(ushort.Parse(val));
                    return CommandResult.Ok();
                }
                return CommandResult.Ok("mask", runtime.AddressMask.ToString());
            });

            router.Register(new CommandMeta
            {
                Id = "SLN",
                Category = "Transceiver",
                Sources = "T,R,W",
                Parameters = "val=N",
                Response = "SLN,OK,val=N",
                Description = "Get/set salinity (PSU)"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("val", out var val))
                {
                    runtime.UpdateSalinity(double.Parse(val, CultureInfo.InvariantCulture));
                    return CommandResult.Ok();
                }
                return CommandResult.Ok("val", runtime.Salinity.ToString("F1"));
            });

            router.Register(new CommandMeta
            {
                Id = "MDST",
                Category = "Transceiver",
                Sources = "T,R,W",
                Parameters = "val=N",
                Response = "MDST,OK,val=N",
                Description = "Get/set max distance in meters (restarts interrogation on change)"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("val", out var val))
                {
                    runtime.UpdateMaxDistance(double.Parse(val, CultureInfo.InvariantCulture));
                    return CommandResult.Ok();
                }
                return CommandResult.Ok("val", runtime.MaxDistance.ToString("F0"));
            });

            router.Register(new CommandMeta
            {
                Id = "SOS",
                Category = "Transceiver",
                Sources = "T,R,W",
                Parameters = "val=N",
                Response = "SOS,OK,val=N",
                Description = "Get/set speed of sound (m/s). Empty/NaN = auto"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("val", out var val) && !string.IsNullOrEmpty(val))
                {
                    runtime.UpdateSoundSpeed(double.Parse(val, CultureInfo.InvariantCulture));
                    return CommandResult.Ok();
                }
                else if (args.TryGetValue("val", out _))
                {
                    // val= с пустым значением — устанавливаем авто (NaN)
                    runtime.UpdateSoundSpeed(double.NaN);
                    return CommandResult.Ok();
                }
                return CommandResult.Ok("val", runtime.SoundSpeed.ToString("F1"));
            });

            router.Register(new CommandMeta
            {
                Id = "CREQ",
                Category = "Transceiver",
                Sources = "T,R,W",
                Parameters = "addr=N,code=N",
                Response = "CREQ,OK",
                Description = "Send custom data request to beacon (addr=1-16, code=3-30)"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("addr", out var addr) && args.TryGetValue("code", out var code))
                {
                    runtime.RequestBeaconData(int.Parse(addr), int.Parse(code));
                    return CommandResult.Ok();
                }
                return CommandResult.Error("usage: CREQ,addr=N,code=N");
            });
        }

        private static void RegisterPositionCommands(CommandRouter router, ApplicationRuntime runtime)
        {
            router.Register(new CommandMeta
            {
                Id = "LHOV",
                Category = "Position",
                Sources = "T,R,W",
                Parameters = "lat=N,lon=N,hdg=N",
                Response = "LHOV,OK",
                Description = "Override location and heading (empty params = disable override)"
            }, async (args, ctx) =>
            {
                if (args.Count == 0 || (!args.ContainsKey("lat") && !args.ContainsKey("lon") && !args.ContainsKey("hdg")))
                {
                    runtime.DisableLocationOverride();
                    return CommandResult.Ok();
                }
                var lat = double.Parse(args.GetValueOrDefault("lat", "NaN"), CultureInfo.InvariantCulture);
                var lon = double.Parse(args.GetValueOrDefault("lon", "NaN"), CultureInfo.InvariantCulture);
                var hdg = double.Parse(args.GetValueOrDefault("hdg", "NaN"), CultureInfo.InvariantCulture);
                if (double.IsNaN(lat) || double.IsNaN(lon) || double.IsNaN(hdg))
                    return CommandResult.Error("lat, lon, hdg required");
                runtime.OverrideLocation(lat, lon, hdg);
                return CommandResult.Ok();
            });

            router.Register(new CommandMeta
            {
                Id = "LHO?",
                Category = "Position",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "LHO?,OK,active=TRUE/FALSE",
                Description = "Check location override status"
            }, async (args, ctx) =>
            {
                return CommandResult.Ok("active", runtime.LocationOverrideActive ? "TRUE" : "FALSE");
            });

            router.Register(new CommandMeta
            {
                Id = "SRC3",
                Category = "Position",
                Sources = "T,R,W",
                Parameters = "mode=0/1/2,c0..c5=N",
                Response = "SRC3,OK",
                Description = "Set 3 LBL responder coordinates (0=discard,1=cartesian,2=geographic)"
            }, async (args, ctx) =>
            {
                var mode = args.GetValueOrDefault("mode", "0");
                var coords = new double[6];
                for (int i = 0; i < 6; i++)
                    coords[i] = double.TryParse(args.GetValueOrDefault($"c{i}", "NaN"), out var v) ? v : double.NaN;
                runtime.SetLBLResponders(mode, coords);
                return CommandResult.Ok();
            });

            router.Register(new CommandMeta
            {
                Id = "OFS",
                Category = "Position",
                Sources = "T,R,W",
                Parameters = "x=N,y=N,phi=N",
                Response = "OFS,OK,x=N,y=N,phi=N",
                Description = "Get/set antenna offsets (X,Y in meters, Phi in degrees)"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("x", out var x) &&
                    args.TryGetValue("y", out var y) &&
                    args.TryGetValue("phi", out var phi))
                {
                    runtime.UpdateAntennaOffsets(
                        double.Parse(x, CultureInfo.InvariantCulture),
                        double.Parse(y, CultureInfo.InvariantCulture),
                        double.Parse(phi, CultureInfo.InvariantCulture));
                    return CommandResult.Ok();
                }
                return CommandResult.Ok(new Dictionary<string, string>
                {
                    ["x"] = runtime.AntennaXOffset.ToString("F2"),
                    ["y"] = runtime.AntennaYOffset.ToString("F2"),
                    ["phi"] = runtime.AntennaPhi.ToString("F1")
                });
            });
        }

        private static void RegisterBeaconCommands(CommandRouter router, ApplicationRuntime runtime)
        {
            router.Register(new CommandMeta
            {
                Id = "RRA?",
                Category = "Beacons",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "RRA?,OK",
                Description = "Request current responder local address"
            }, async (args, ctx) =>
            {
                runtime.QueryLocalAddress();
                return CommandResult.Ok();
            });

            router.Register(new CommandMeta
            {
                Id = "SRRA",
                Category = "Beacons",
                Sources = "T,R,W",
                Parameters = "addr=N",
                Response = "SRRA,OK",
                Description = "Set responder local address"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("addr", out var addr))
                {
                    runtime.SetLocalAddress(int.Parse(addr));
                    return CommandResult.Ok();
                }
                return CommandResult.Error("usage: SRRA,addr=N");
            });
        }

        private static void RegisterCalibrationCommands(CommandRouter router, ApplicationRuntime runtime)
        {
            router.Register(new CommandMeta
            {
                Id = "SCAL",
                Category = "Calibration",
                Sources = "T,R,W",
                Parameters = "start=N,step=N,n=N",
                Response = "SCAL,OK",
                Description = "Start antenna calibration with rotator (default: start=0,step=15,n=20)"
            }, async (args, ctx) =>
            {
                var start = args.TryGetValue("start", out var s) ? double.Parse(s, CultureInfo.InvariantCulture) : 0.0;
                var step = args.TryGetValue("step", out var st) ? double.Parse(st, CultureInfo.InvariantCulture) : 15.0;
                var n = args.TryGetValue("n", out var sn) ? int.Parse(sn) : 20;
                runtime.StartCalibration(start, step, n);
                return CommandResult.Ok();
            });

            router.Register(new CommandMeta
            {
                Id = "FCAL",
                Category = "Calibration",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "FCAL,OK",
                Description = "Stop/abort antenna calibration"
            }, async (args, ctx) =>
            {
                runtime.StopCalibration();
                return CommandResult.Ok();
            });

            router.Register(new CommandMeta
            {
                Id = "CAL?",
                Category = "Calibration",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "CAL?,OK,state=...,points=N,total=N,angle=N,acal_state=...,acal_collected=N,acal_total=N[,acal_phi=N]",
                Description = "Get calibration status (rotator + angular)"
            }, async (args, ctx) =>
            {
                return CommandResult.Ok(runtime.GetCalibrationStatus());
            });

            router.Register(new CommandMeta
            {
                Id = "ACAL",
                Category = "Calibration",
                Sources = "T,R,W",
                Parameters = "start=N,end=N,step=N,n=N,addr=N",
                Response = "ACAL,OK",
                Description = "Start angular calibration (compass/antenna zero alignment)"
            }, async (args, ctx) =>
            {
                var st = double.Parse(args["start"], CultureInfo.InvariantCulture);
                var nd = double.Parse(args["end"], CultureInfo.InvariantCulture);
                var step = double.Parse(args["step"], CultureInfo.InvariantCulture);
                var n = int.Parse(args["n"]);
                var addr = int.Parse(args["addr"]);
                runtime.StartAngularCalibration(st, nd, step, n, addr);
                return CommandResult.Ok();
            });
        }

        private static void RegisterOutputCommands(CommandRouter router, ApplicationRuntime runtime)
        {
            router.Register(new CommandMeta
            {
                Id = "OFMT?",
                Category = "Output",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "OFMT?,OK,format=...",
                Description = "Get output messages format description"
            }, async (args, ctx) =>
            {
                return CommandResult.Ok("format", runtime.GetOutputFormat());
            });

            router.Register(new CommandMeta
            {
                Id = "PSIMSSB",
                Category = "Output",
                Sources = "T,R,W",
                Parameters = "on=TRUE/FALSE",
                Response = "PSIMSSB,OK",
                Description = "Enable/disable PSIMSSB (Simrad/HiPAP) output format"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("on", out var val))
                {
                    runtime.SetPSIMSSBOutput(val.ToUpper() == "TRUE" || val == "1" || val.ToUpper() == "ON");
                    return CommandResult.Ok("on", val.ToUpper());
                }
                return CommandResult.Ok("on", runtime.GetPSIMSSBOutput() ? "TRUE" : "FALSE");
            });
        }

        private static void RegisterLogCommands(CommandRouter router, ApplicationRuntime runtime)
        {
            router.Register(new CommandMeta
            {
                Id = "WEBLOG",
                Category = "Log",
                Sources = "T,R,W",
                Parameters = "on=TRUE/FALSE",
                Response = "WEBLOG,OK",
                Description = "Toggle web command logging to file (default OFF)"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("on", out var val))
                {
                    var enable = val.ToUpper() == "TRUE" || val == "1" || val.ToUpper() == "ON";
                    runtime.SetWebLogging(enable);
                    return CommandResult.Ok("on", enable ? "TRUE" : "FALSE");
                }
                return CommandResult.Ok("on", runtime.GetWebLogging() ? "TRUE" : "FALSE");
            });

            router.Register(new CommandMeta
            {
                Id = "DELGS",
                Category = "Log",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "DELGS,OK",
                Description = "Delete all old log files (current log preserved)"
            }, async (args, ctx) =>
            {
                var result = runtime.CleanOldLogs();
                return result ? CommandResult.Ok() : CommandResult.Error("failed to clean logs");
            });
        }

        private static void RegisterServiceCommands(CommandRouter router, ApplicationRuntime runtime)
        {
            router.Register(new CommandMeta
            {
                Id = "HELP",
                Category = "Service",
                Sources = "T,R,W",
                Parameters = "cmd=?",
                Response = "HELP,OK,commands=...",
                Description = "Show help for all commands or specific command"
            }, async (args, ctx) =>
            {
                var cmd = args.GetValueOrDefault("cmd", null);
                var help = router.GetHelp(cmd);
                return CommandResult.Ok("commands", help);
            });

            router.Register(new CommandMeta
            {
                Id = "HKEYS",
                Category = "Service",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "HKEYS,OK,hotkeys=...",
                Description = "Show hotkeys hint"
            }, async (args, ctx) =>
            {
                var keys = "F1 - Help\nF12 - Switch log mode (Normal/Errors/Silent)\n" +
                           "Ctrl+L - Clear screen\nCtrl+N - Open connection (OCON)\n" +
                           "Ctrl+Shift+N - Close connection (CCON)\n" +
                           "Ctrl+I - Resume interrogation (RITG)\n" +
                           "Ctrl+Shift+I - Pause interrogation (PITG)\nCtrl+E - Exit";
                return CommandResult.Ok("hotkeys", keys);
            });

            router.Register(new CommandMeta
            {
                Id = "EXIT",
                Category = "Service",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "EXIT,OK",
                Description = "Terminate application"
            }, async (args, ctx) =>
            {
                runtime.RequestShutdown();
                return CommandResult.Ok();
            });

            router.Register(new CommandMeta
            {
                Id = "VER",
                Category = "Service",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "VER,OK,version=...",
                Description = "Show version info"
            }, async (args, ctx) =>
            {
                return CommandResult.Ok("version", AppUtils.GetFullVersionInfo());
            });

            router.Register(new CommandMeta
            {
                Id = "STAT",
                Category = "Service",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "STAT,OK,azm_status=...,interrogation=...,...",
                Description = "Show system status summary"
            }, async (args, ctx) =>
            {
                return CommandResult.Ok(runtime.GetSystemState());
            });
            
            router.Register(new CommandMeta
            {
                Id = "EXPCR",
                Category = "Service",
                Sources = "T,R,W",
                Parameters = "file=path",
                Response = "EXPCR,OK",
                Description = "Export command reference to Markdown file"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("file", out var file))
                {
                    var result = runtime.ExportCommandReference(file);
                    return result ? CommandResult.Ok() : CommandResult.Error("failed to write file");
                }
                return CommandResult.Error("usage: EXPCC,file=path.md");
            });

            router.Register(new CommandMeta
            {
                Id = "PLAY",
                Category = "Service",
                Sources = "T,R,W",
                Parameters = "speed=0|1,file=path",
                Response = "PLAY,OK",
                Description = "Playback log file (0=instant, 1=realtime, no params=stop)"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("file", out var file))
                {
                    var isInstant = !args.TryGetValue("speed", out var speed) || speed == "0";
                    var result = runtime.StartLogPlayback(isInstant, file);
                    return result ? CommandResult.Ok() : CommandResult.Error("log player not available");
                }
                var stopped = runtime.StopLogPlayback();
                return stopped ? CommandResult.Ok() : CommandResult.Error("not playing");
            });

            router.Register(new CommandMeta
            {
                Id = "SCRIPT",
                Category = "Service",
                Sources = "T",
                Parameters = "file=path",
                Response = "SCRIPT,OK",
                Description = "Execute script from file"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("file", out var file))
                {
                    await runtime.ExecuteScript(file);
                    return CommandResult.Ok();
                }
                return CommandResult.Error("usage: SCRIPT,file=path.ext");
            });

            router.Register(new CommandMeta
            {
                Id = "WAIT",
                Category = "Service",
                Sources = "T",
                Parameters = "for=ACAL|CAL|OCON|DETECTED|N,timeout=N,port=id",
                Response = "WAIT,OK",
                Description = "Wait for event: ACAL=angular calibration complete, CAL=rotator calibration complete, OCON=connection, DETECTED=port detected, N=ms"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("for", out var condition))
                {
                    switch (condition.ToUpper())
                    {
                        case "ACAL":
                            return await runtime.WaitForAngularCalibration(args);
                        case "CAL":
                            return await runtime.WaitForCalibration(args);
                        case "OCON":
                            return await runtime.WaitForConnection(args);
                        case "DETECTED":
                            return await runtime.WaitForDetected(args);
                        default:
                            if (int.TryParse(condition, out var ms))
                            {
                                await Task.Delay(ms);
                                return CommandResult.Ok();
                            }
                            return CommandResult.Error($"unknown condition: {condition}");
                    }
                }
                return CommandResult.Error("usage: WAIT,for=ACAL|CAL|OCON|DETECTED|N,timeout=N,port=id");
            });

            router.Register(new CommandMeta
            {
                Id = "SAVE",
                Category = "Service",
                Sources = "T,R,W",
                Parameters = "file=path",
                Response = "SAVE,OK",
                Description = "Save current settings as init script"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("file", out var file))
                {
                    var result = runtime.SaveSettings(file);
                    return result ? CommandResult.Ok() : CommandResult.Error("failed to save");
                }
                return CommandResult.Error("usage: SAVE,file=settings.cmd");
            });

            router.Register(new CommandMeta
            {
                Id = "SAVEINIT",
                Category = "Service",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "SAVEINIT,OK",
                Description = "Save current settings as default init script (init.cmd)"
            }, async (args, ctx) =>
            {
                var initFile = Path.Combine(AppContext.BaseDirectory, "init.cmd");
                var result = runtime.SaveSettings(initFile);
                return result ? CommandResult.Ok() : CommandResult.Error("failed to save init.cmd");
            });

            router.Register(new CommandMeta
            {
                Id = "RESETINIT",
                Category = "Service",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "RESETINIT,OK",
                Description = "Remove default init script, revert to factory defaults on next start"
            }, async (args, ctx) =>
            {
                var initFile = Path.Combine(AppContext.BaseDirectory, "init.cmd");
                try
                {
                    if (File.Exists(initFile))
                    {
                        File.Delete(initFile);                        
                    }
                    return CommandResult.Ok();
                }
                catch (Exception ex)
                {
                    return CommandResult.Error($"failed to remove init.cmd: {ex.Message}");
                }
            });
        }



        private static void RegisterConnectionCommands(CommandRouter router, ApplicationRuntime runtime)
        {
            router.Register(new CommandMeta
            {
                Id = "OCON",
                Category = "Connection",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "OCON,OK",
                Description = "Open connections (AZM >> AUX1 >> AUX2 chain)"
            }, async (args, ctx) =>
            {
                await runtime.ConnectAsync();
                return CommandResult.Ok();
            });

            router.Register(new CommandMeta
            {
                Id = "CCON",
                Category = "Connection",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "CCON,OK",
                Description = "Close all connections"
            }, async (args, ctx) =>
            {
                await runtime.DisconnectAsync();
                return CommandResult.Ok();
            });

            router.Register(new CommandMeta
            {
                Id = "CNA?",
                Category = "Connection",
                Sources = "T,R,W",
                Parameters = "-",
                Response = "CNA?,OK,active=TRUE/FALSE",
                Description = "Check connection status"
            }, async (args, ctx) =>
            {
                return CommandResult.Ok("active", runtime.AzmStatus != "Inactive" ? "TRUE" : "FALSE");
            });

            router.Register(new CommandMeta
            {
                Id = "DET?",
                Category = "Connection",
                Sources = "T,R,W",
                Parameters = "id=AZM/AUX1/AUX2/RDT",
                Response = "DET?,OK,detected=TRUE/FALSE",
                Description = "Check device detection status"
            }, async (args, ctx) =>
            {
                if (args.TryGetValue("id", out var id))
                    return CommandResult.Ok("detected", runtime.IsDeviceDetected(id) ? "TRUE" : "FALSE");
                return CommandResult.Error("usage: DET?,id=AZM|AUX1|AUX2|RDT");
            });
        }
    }
}