using AzimuthConsole.AZM;
using System.Net;
using UCNLDrivers;

namespace AzimuthConsole
{
    public class CmdRegistrar
    {
      
        public void RegisterTerminalCommands(SettingsContainer settings, CmdProcessor proc, AZMCombiner combiner)
        {
            RegisterConnectCommand(proc, combiner);
            RegisterDisconnectCommand(proc, combiner);
            RegisterGetConnectionStateCommand(proc, combiner);
            RegisterGetInterrogationStateCommand(proc, combiner);
            RegisterGetDetectedStateCommand(proc, combiner);
            RegisterCREQCommand(proc, combiner);
            RegisterGetLocationAndHeadingOverrideCommand(proc, combiner);
            RegisterGetOutputFormatCommand(proc, combiner);
            RegisterSet3RespondersCoordinatesCommand(settings, proc, combiner);
            RegisterSetResponderIndividualUDPOutput(settings, proc, combiner);
            RegisterResponderRemoteAddressQueryCommand(proc, combiner);
            RegisterSetResponderRemoteAddressQueryCommand(proc, combiner);
            
            RegisterPauseInterrogationCommand(proc, combiner);
            RegisterResumeInterrogationCommand(proc, combiner);
            RegisterLocationAndHeadingOverrideCommand(proc, combiner);
        }

        public void RegisterCommandLineCommands(SettingsContainer settings, CmdProcessor proc, AZMCombiner combiner) 
        {
            RegisterSettingsMainCommand(settings, proc);
            RegisterSettingsAuxilary(settings, proc);
            RegisterSettingsOutput(settings, proc);
            RegisterSetAntennaRelativePosition(settings, proc);
            RegisterSet3RespondersCoordinatesCommand(settings, proc, combiner);
            RegisterSetResponderIndividualUDPOutput(settings, proc, combiner);
        }

        public void RegisterRCTRLCommands(SettingsContainer settings, CmdProcessor proc, AZMCombiner combiner)
        {
            RegisterConnectCommand(proc, combiner);
            RegisterDisconnectCommand(proc, combiner);
            RegisterGetConnectionStateCommand(proc, combiner);
            RegisterGetInterrogationStateCommand(proc, combiner);
            RegisterGetDetectedStateCommand(proc, combiner);
            RegisterCREQCommand(proc, combiner);
            RegisterGetLocationAndHeadingOverrideCommand(proc, combiner);
            RegisterGetOutputFormatCommand(proc, combiner);
            
            RegisterSetResponderIndividualUDPOutput(settings, proc, combiner);
            RegisterResponderRemoteAddressQueryCommand(proc, combiner);
            RegisterSetResponderRemoteAddressQueryCommand(proc, combiner);

            RegisterPauseInterrogationCommand(proc, combiner);
            RegisterResumeInterrogationCommand(proc, combiner);
            RegisterLocationAndHeadingOverrideCommand(proc, combiner);

            RegisterSet3RespondersCoordinatesCommand(settings, proc, combiner);
        }



        private void RegisterLocationAndHeadingOverrideCommand(CmdProcessor proc, AZMCombiner combiner)
        {
            proc.AddCommand("LHOV", "x.x,x.x,x.x", args =>
            {
                bool result = false;

                if (combiner != null)
                {
                    if ((args[0] == null) && (args[1] == null) && (args[2] == null))
                    {
                        result = combiner.LocationOverrideDisable();
                    }
                    else
                    {
                        double lt = AZM.AZM.O2D(args[0]);
                        double ln = AZM.AZM.O2D(args[1]);
                        double hd = AZM.AZM.O2D(args[2]);

                        if (!double.IsNaN(lt) && !double.IsNaN(ln) && !double.IsNaN(hd) &&
                            AZM.AZM.IsLatDeg(lt) && AZM.AZM.IsLonDeg(ln) && AZM.AZM.IsLatDeg(hd))
                        {
                            result = combiner.LocationOverrideEnable(AZM.AZM.O2D(args[0]), AZM.AZM.O2D(args[1]), AZM.AZM.O2D(args[2]));
                        }
                    }
                }

                return result;
            },
        "Antenna's Location and Heading OVerride. LHOV,lat_deg,lon_deg,hdn_deg. All empty fields disable the feature.");
        }

        private void RegisterResumeInterrogationCommand(CmdProcessor proc, AZMCombiner combiner)
        {
            proc.AddCommand("RITG", "", args => combiner?.ResumeInterrogation() ?? false, "Resume responders interrogation.");
        }

        private void RegisterPauseInterrogationCommand(CmdProcessor proc, AZMCombiner combiner)
        {
            proc.AddCommand("PITG", "", args => combiner?.PauseInterrogation() ?? false, "Pause responders interrogation.");
        }

        private void RegisterSettingsMainCommand(SettingsContainer settings, CmdProcessor proc)
        {
            proc.AddCommand("SETM", "c--c,x,c--c,c--c,x,x.x,x", args =>
            {
                settings.azmPrefPortName = CConv.O2S_D(args[0], string.Empty);
                if (settings.azmPrefPortName.ToUpper() == "AUTO")
                    settings.azmPrefPortName = string.Empty;

                settings.azmPortBaudrate = CConv.O2Baudrate_D(args[1], settings.azmPortBaudrate);

                settings.rctrl_enabled = (args[2] != null) && 
                (IPEndPoint.TryParse(CConv.O2S_D(args[2], settings.rctrl_in_endpoint.ToString()), out settings.rctrl_in_endpoint)) &&
                (args[3] != null) && IPEndPoint.TryParse(CConv.O2S_D(args[3], settings.rctrl_out_endpoint.ToString()), out settings.rctrl_out_endpoint);

                settings.address_mask = CConv.O2U16_D(args[4], settings.address_mask);
                settings.sty_PSU = CConv.O2D_D(args[5], settings.sty_PSU);
                settings.max_dist_m = CConv.O2S32_D(args[6], settings.max_dist_m);

                return true;
            },
            "Set main parameters. SETM,azmPort|AUTO,[azmBaudrate],[rctrl_in_ip_addr:port],[rctrl_out_ip_addr:port],[addr_mask],[salinity_PSU],[max_dist_m]");
        }

        private void RegisterSettingsAuxilary(SettingsContainer settings, CmdProcessor proc)
        {
            proc.AddCommand("SETA", "c--c,x,c--c,x", args =>
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
        }

        private void RegisterSettingsOutput(SettingsContainer settings, CmdProcessor proc)
        {
            proc.AddCommand("SETO", "c--c,x,c--c", args =>
            {
                settings.outputSerialEnabled = !string.IsNullOrEmpty((string)args[0]);
                if (settings.outputSerialEnabled)
                {
                    settings.outputSerialPortName = CConv.O2S_D(args[0], settings.outputSerialPortName);
                    settings.outputSerialPortBaudrate = CConv.O2Baudrate_D(args[1], settings.outputSerialPortBaudrate);
                }

                settings.output_udp_enabled = (args[2] != null) && 
                IPEndPoint.TryParse(CConv.O2S_D(args[2], settings.output_endpoint.ToString()), out settings.output_endpoint);

                return true;
            },
            "Set output parameters. SETO,[outPort],[outBaudrate],[out_ip_addr:port]");
        }

        private void RegisterSetAntennaRelativePosition(SettingsContainer settings, CmdProcessor proc)
        {
            proc.AddCommand("SARP", "x.x,x.x,x.x", args =>
            {
                settings.antenna_x_offset_m = CConv.O2D_D(args[0], settings.antenna_x_offset_m);
                settings.antenna_y_offset_m = CConv.O2D_D(args[1], settings.antenna_y_offset_m);
                settings.antenna_angular_offset_deg = CConv.O2D_D(args[2], settings.antenna_angular_offset_deg);

                return true;
            },
            "Set antenna's relative position. SARP,x_offset_m,y_offset_m,angular_offset_deg");
        }


        public void RegisterResponderRemoteAddressQueryCommand(CmdProcessor proc, AZMCombiner combiner)
        {
            proc.AddCommand("RRA?", "", args =>
            {
                if ((combiner != null))
                {
                    return combiner.QueryLocalAddress();
                }
                else
                    return false;

            }, "Request current responder address. Usage: RRA?");
        }

        public void RegisterSetResponderRemoteAddressQueryCommand(CmdProcessor proc, AZMCombiner combiner)
        {
            proc.AddCommand("SRRA", "x", args =>
            {
                if ((combiner != null))
                {
                    return combiner.QueryLocalAddressSet((REMOTE_ADDR_Enum)args[0]);
                }
                else
                    return false;

            }, "Request to set responder address. Usage: SRRA,address");
        }

        public void RegisterGetInterrogationStateCommand(CmdProcessor proc, AZMCombiner combiner)
        {
            proc.AddCommand("ITG?", "", args =>
            {
                proc.OnOutput($"ITG,{combiner?.InterrogationActive ?? false}");
                return true;
            }, "Get responders InTerroGation state. Returns as ITG,{true|false}.");
        }

        public void RegisterGetConnectionStateCommand(CmdProcessor proc, AZMCombiner combiner)
        {
            proc.AddCommand("CNA?", "", args =>
            {
                proc.OnOutput($"CNA,{combiner?.ConnectionActive ?? false}");
                return true;
            }, "Check if CoNnection is Active. Returns as CNA,{true|false}.");
        }

        public void RegisterGetDetectedStateCommand(CmdProcessor proc, AZMCombiner combiner)
        {
            proc.AddCommand("DET?", "c--c", args =>
            {
                var deviceID = CConv.O2S(args[0]);
                bool isDetected = combiner?.IsDeviceDetected(deviceID) ?? false;
                proc.OnOutput($"DET,{deviceID},{isDetected}");
                return true;

            }, "Check if specified device is DETected. DET?,AZM|AUX1|AUX2. Returns as DET,DeviceID,true|false.");
        }

        public void RegisterConnectCommand(CmdProcessor proc, AZMCombiner combiner)
        {
            proc.AddCommand("OCON", "", args =>
            {
                bool result = false;
                try
                {
                    result = combiner.Connect();
                }
                catch (Exception ex)
                {
                    proc.OnOutput(ex.ToString());
                }

                return result;
            }, "Open CONnections.");
        }

        public void RegisterDisconnectCommand(CmdProcessor proc, AZMCombiner combiner)
        {
            proc.AddCommand("CCON", "", args =>
            {
                bool result = false;
                try
                {
                    result = combiner.Disconnect();
                }
                catch (Exception ex)
                {
                    proc.OnOutput(ex.ToString());
                }

                return result;
            }, "Close CONnections.");
        }

        public void RegisterCREQCommand(CmdProcessor proc, AZMCombiner combiner)
        {
            proc.AddCommand("CREQ", "x,x", args =>
            {
                REMOTE_ADDR_Enum remAddr = (REMOTE_ADDR_Enum)args[0];
                CDS_REQ_CODES_Enum reqCode = (CDS_REQ_CODES_Enum)args[1];

                if ((combiner != null) && (remAddr != REMOTE_ADDR_Enum.REM_ADDR_INVALID) && AZM.AZM.IsUserDataReqCode(reqCode))
                {
                    return combiner.CREQ(remAddr, reqCode);
                }
                else
                    return false;
            }, "Request custom user data value. Usage: CREQ,remAddr=1..16,reqCode=3..30.");
        }


        public void RegisterGetLocationAndHeadingOverrideCommand(CmdProcessor proc, AZMCombiner combiner)
        {
            proc.AddCommand("LHO?", "", args =>
            {
                proc.OnOutput($"LHO,{combiner?.LocationOverrideEnabled ?? false}");
                return true;

            }, "Get Location and Heading Override feature status. Returns as LHO,true|false");
        }

        public void RegisterGetOutputFormatCommand(CmdProcessor proc, AZMCombiner combiner)
        {
            proc.AddCommand("OFMT?", "", args =>
            {
                proc.OnOutput(
                    string.Format("Station local parameters:\r\n{0}\r\nRemote parameters:\r\n{1}",
                    combiner.GetStationParametersToStringFormat(),
                    (new ResponderBeacon(REMOTE_ADDR_Enum.REM_ADDR_1).GetToStringFormat())));

                return true;

            }, "Get output messages format description.");
        }

        bool CheckLBL3ModeParameters(LBLResponderCoordinatesModeEnum lblRCMode,
            double r1x, double r1y, double r2x, double r2y, double r3x, double r3y)
        {
            bool result = false;

            if (lblRCMode == LBLResponderCoordinatesModeEnum.Geographic)
            {
                result = r1x.IsValidLonDeg() && r1y.IsValidLatDeg() &&
                         r2x.IsValidLonDeg() && r2y.IsValidLatDeg() &&
                         r3x.IsValidLonDeg() && r3y.IsValidLatDeg();
            }
            else if (lblRCMode == LBLResponderCoordinatesModeEnum.Cartesian)
            {
                result = !double.IsNaN(r1x) && !double.IsNaN(r1y) &&
                         !double.IsNaN(r2x) && !double.IsNaN(r2y) &&
                         !double.IsNaN(r3x) && !double.IsNaN(r3y);
            }
            else
            {
                result = lblRCMode != LBLResponderCoordinatesModeEnum.Invalid;
            }

            return result;
        }

        public bool SetLBL3RespondersModeOnTheFly(AZMCombiner combiner, LBLResponderCoordinatesModeEnum lblRCMode,
            double r1x, double r1y, double r2x, double r2y, double r3x, double r3y)
        {
            if (CheckLBL3ModeParameters(lblRCMode, r1x, r1y, r2x, r2y, r3x, r3y))
            {
                if (lblRCMode == LBLResponderCoordinatesModeEnum.Cartesian)
                    return combiner.Set3RespondersLocalCoordinates(r1x, r1y, r2x, r2y, r3x, r3y);
                else if (lblRCMode == LBLResponderCoordinatesModeEnum.Geographic)
                    return combiner.Set3RespondersGeographicCoordinates(r1x, r1y, r2x, r2y, r3x, r3y);
                else
                    return combiner.Discard3RespondersCoordinates();
            }
            else
                return false;
        }

        public bool SetLBL3ResponderModeOnStartup(SettingsContainer settings, LBLResponderCoordinatesModeEnum lblRCMode,
            double r1x, double r1y, double r2x, double r2y, double r3x, double r3y)
        {
            if (CheckLBL3ModeParameters(lblRCMode, r1x, r1y, r2x, r2y, r3x, r3y))
            {
                settings.LBLResponderCoordinatesMode = lblRCMode;

                if (lblRCMode != LBLResponderCoordinatesModeEnum.None)
                {
                    settings.LBLModeR1X = r1x;
                    settings.LBLModeR1Y = r1y;
                    settings.LBLModeR2X = r2x;
                    settings.LBLModeR2Y = r2y;
                    settings.LBLModeR3X = r3x;
                    settings.LBLModeR3Y = r3y;
                }

                return true;
            }
            else
                return false;
        }


        public void RegisterSet3RespondersCoordinatesCommand(SettingsContainer settings, CmdProcessor proc, AZMCombiner combiner) =>
            proc.AddCommand("SRC3", "x,x.x,x.x,x.x,x.x,x.x,x.x", args =>
            {
                LBLResponderCoordinatesModeEnum lblRCMode =
                (args[0] == null) ? LBLResponderCoordinatesModeEnum.None : (LBLResponderCoordinatesModeEnum)(int)args[0];

                double r1x = AZM.AZM.O2D(args[1]);
                double r1y = AZM.AZM.O2D(args[2]);
                double r2x = AZM.AZM.O2D(args[3]);
                double r2y = AZM.AZM.O2D(args[4]);
                double r3x = AZM.AZM.O2D(args[5]);
                double r3y = AZM.AZM.O2D(args[6]);

                if (combiner != null)
                    return SetLBL3RespondersModeOnTheFly(combiner, lblRCMode, r1x, r1y, r2x, r2y, r3x, r3y);
                else
                    return SetLBL3ResponderModeOnStartup(settings, lblRCMode, r1x, r1y, r2x, r2y, r3x, r3y);

            }, "Set responders coordinates. Usage: SRC3,0-discard|1-cartesian|2-geographic,r1x,r1y,...,r3x,r3y");

        public void RegisterSetResponderIndividualUDPOutput(SettingsContainer settings, CmdProcessor proc, AZMCombiner combiner) =>
            proc.AddCommand("SIOC", "x,c--c", args =>
            {
                REMOTE_ADDR_Enum radd = (REMOTE_ADDR_Enum)AZM.AZM.O2_REMOTE_ADDR_Enum(args[0]);
                bool discard = args[1] == null;

                if (radd != REMOTE_ADDR_Enum.REM_ADDR_INVALID)
                {
                    if (discard)
                    {
                        if (combiner == null)
                        {
                            settings.InvidvidualEndpoints.Remove(radd);
                            return true;
                        }
                        else
                        {
                            return combiner.DiscardResponderInvidualUPDChannel(radd);
                        }
                    }
                    else
                    {
                        bool result = IPEndPoint.TryParse(CConv.O2S(args[1]), out IPEndPoint newEP);
                        if (result)
                        {
                            if (combiner == null)
                            {
                                settings.InvidvidualEndpoints[radd] = newEP;
                                return true;
                            }
                            else
                            {
                                return combiner.SetResponderIndividualUDPChannel(radd, newEP);
                            }
                        }
                        else
                            return false;
                    }
                }
                else
                    return false;

            }, "Set responder's individual UDP output channel. Usage: SIOC,responderAddress,address:port");

    }
}
