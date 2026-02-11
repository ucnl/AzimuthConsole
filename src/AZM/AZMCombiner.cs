using System.Globalization;
using System.Net;
using System.Text;
using UCNLDrivers;
using UCNLMan;
using UCNLNav;
using UCNLNav.TrackFilters;
using UCNLNMEA;
using UCNLPhysics;

namespace AzimuthConsole.AZM
{
    public class CREQResultEventArgs : EventArgs
    {
        public REMOTE_ADDR_Enum RemoteAddress { get; private set; }

        public CDS_REQ_CODES_Enum ReqCode { get; private set; }
        public int ResCode { get; private set; }

        public CREQResultEventArgs(REMOTE_ADDR_Enum addr, CDS_REQ_CODES_Enum req_code, int res_code)
        {
            RemoteAddress = addr;
            ReqCode = req_code;
            ResCode = res_code;
        }
    }   

    public class AZMCombiner
    {
        public bool ConnectionActive { get => azmPort != null && azmPort.IsActive; }

        bool interrogationActive = false;
        public bool InterrogationActive
        {
            get => interrogationActive;
            private set
            {
                interrogationActive = value;
                InterrogationActiveChangedHandler?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool AZMDetected { get => azmPort != null && azmPort.Detected; }
        public string AZMPreferredPortName
        {
            get => azmPort == null ? string.Empty : azmPort.ProposedPortName;
            set
            { 
                if (azmPort != null) 
                    azmPort.ProposedPortName = value; 
            }
        }

        public bool IsUseAUX1 { get; private set; }
        public bool AUX1Detected { get { return (aux1Port != null) && (aux1Port.Detected); } }

        string aux1PreferredPortName = string.Empty;
        public string AUX1PreferredPortName
        {
            get { return (aux1Port != null) ? aux1Port.ProposedPortName : string.Empty; }
            set
            {
                aux1PreferredPortName = value;
            }
        }

        public bool IsUseAUX2 { get; private set; }
        public bool AUX2Detected { get { return (aux2Port != null) && (aux2Port.Detected); } }

        string aux2PreferredPortName = string.Empty;
        public string AUX2PreferredPortName
        {
            get { return (aux2Port != null) ? aux2Port.ProposedPortName : string.Empty; }
            set
            {
                aux2PreferredPortName = value;
            }
        }

        public bool IsUseSerialOutput { get; private set; }

        public bool IsUseUDPOutput { get; private set; }

        public bool IsStationNMEAOutputEnabled { get; set; }

        public ushort AddressMask { get; private set; }
        public double MaxDist_m { get; private set; }
        public double Salinity_PSU { get; private set; }

        public AZM_DEVICE_TYPE_Enum DeviceType { get; private set; }
        public string DeviceSerialNumber { get; private set; }
        public string DeviceVersionInfo { get; private set; }

        public double LatitudeOverride { get; private set; }
        public double LongitudeOverride { get; private set; }
        public double HeadingOverride { get; private set; }
        public bool LocationOverrideEnabled { get => pTimer.Enabled; }


        public bool IsRecalculateRange { get; set; } = true;
        public bool IsPTimeAdjustment { get; set; } = true;
        double PTimePerAddrAdjustment_s = 0.0006; // 
        double PTimePerAddrAdjustment2_s = 0.0004;

        readonly WPManager wpManager = new();


        double phi_deg;
        double x_offset_m;
        double y_offset_m;

        AgingValue<double> stPressure_mBar;
        AgingValue<double> stDepth_m;
        AgingValue<double> waterTemp_C;
        AgingValue<double> stPitch_deg;
        AgingValue<double> stRoll_deg;

        AgingValue<double> lat_deg;
        AgingValue<double> lon_deg;
        AgingValue<double> course_deg;
        AgingValue<double> speed_mps;
        AgingValue<double> heading_deg;

        AgingValue<double> x_m;
        AgingValue<double> y_m;
        AgingValue<double> z_m;
        AgingValue<double> rerr_m;

        List<IAging> stationParams;

        readonly AZMPort? azmPort;
        uGNSSSerialPort? aux1Port;
        uMagneticCompassPort? aux2Port;
        NMEASerialPort? serialOutput;
        UDPTranslator? udpOutput;

        Dictionary<REMOTE_ADDR_Enum, ResponderBeacon> remotes;

        bool polling_started_received = false;
        DateTime prevRemAckTS = DateTime.Now;

        readonly System.Timers.Timer pTimer;
        readonly string[] llSeparators = [">>", " "];

        readonly LBLProcessor lblProcessor = new();

        public AZMCombiner(ushort addrMask, double salinity_PSU, double maxDist_m, double hdn_adj_deg, double gnss_x_offset_m, double gnss_y_offset_m)
        {
            DeviceSerialNumber = string.Empty;
            DeviceVersionInfo = string.Empty;

            wpManager.IsAutoSoundSpeed = true;
            wpManager.IsAutoSalinity = false;
            wpManager.Salinity = salinity_PSU;

            AddressMask = addrMask;

            if ((salinity_PSU >= PHX.PHX_SALINITY_PSU_MIN) && (salinity_PSU <= PHX.PHX_SALINITY_PSU_MAX))
                Salinity_PSU = salinity_PSU;
            else
                throw new ArgumentOutOfRangeException(nameof(salinity_PSU));

            if ((hdn_adj_deg >= 0) && (hdn_adj_deg <= 360))
                phi_deg = hdn_adj_deg;
            else
                throw new ArgumentOutOfRangeException(nameof(hdn_adj_deg));

            if ((maxDist_m >= AZM.ABS_MIN_RANGE_M) && (maxDist_m <= AZM.ABS_MAX_RANGE_M))
                MaxDist_m = maxDist_m;
            else
                throw new ArgumentOutOfRangeException(nameof(maxDist_m));

            x_offset_m = gnss_x_offset_m;
            y_offset_m = gnss_y_offset_m;

            remotes = [];

            stPressure_mBar = new AgingValue<double>(int.MaxValue, 10, AZM.mBar_fmtr);
            stPressure_mBar.IgnoreAge = true;
            stPressure_mBar.Name = nameof(stPressure_mBar);

            stDepth_m = new AgingValue<double>(int.MaxValue, 10, AZM.meters1dec_fmtr);
            stDepth_m.IgnoreAge = true;
            stDepth_m.Name = nameof(stDepth_m);

            waterTemp_C = new AgingValue<double>(int.MaxValue, 10, AZM.degC_fmtr);
            waterTemp_C.IgnoreAge = true;
            waterTemp_C.Name = nameof(waterTemp_C);

            stPitch_deg = new AgingValue<double>(3, 10, AZM.degrees1dec_fmtr);
            stPitch_deg.IgnoreAge = true;
            stPitch_deg.Name = nameof(stPitch_deg);

            stRoll_deg = new AgingValue<double>(int.MaxValue, 10, AZM.degrees1dec_fmtr);
            stRoll_deg.Name = nameof(stRoll_deg);

            lat_deg = new AgingValue<double>(int.MaxValue, 10, AZM.latlon_fmtr);
            lat_deg.IgnoreAge = true;
            lat_deg.Name = nameof(lat_deg);
            lon_deg = new AgingValue<double>(int.MaxValue, 10, AZM.latlon_fmtr);
            lon_deg.IgnoreAge = true;
            lon_deg.Name = nameof(lon_deg);
            course_deg = new AgingValue<double>(int.MaxValue, 10, AZM.degrees1dec_fmtr);
            course_deg.IgnoreAge = true;
            course_deg.Name = nameof(course_deg);
            speed_mps = new AgingValue<double>(int.MaxValue, 10, x => string.Format(CultureInfo.InvariantCulture, "{0:F01}", x / 3.6));
            speed_mps.Name = nameof(speed_mps);

            heading_deg = new AgingValue<double>(int.MaxValue, 10, AZM.degrees1dec_fmtr);
            heading_deg.Name = nameof(heading_deg);

            x_m = new AgingValue<double>(int.MaxValue, 10, AZM.meters3dec_fmtr);
            x_m.IgnoreAge = true;
            x_m.Name = nameof(x_m);
            y_m = new AgingValue<double>(int.MaxValue, 10, AZM.meters3dec_fmtr);
            y_m.IgnoreAge = true;
            y_m.Name = nameof(y_m);
            z_m = new AgingValue<double>(int.MaxValue, 10, AZM.meters3dec_fmtr);
            z_m.IgnoreAge = true;
            z_m.Name = nameof(z_m);
            rerr_m = new AgingValue<double>(int.MaxValue, 10, AZM.meters3dec_fmtr);
            rerr_m.Name = nameof(rerr_m);

            stationParams = [ stPressure_mBar, stDepth_m, waterTemp_C, 
                              stPitch_deg, stRoll_deg, lat_deg, lon_deg, course_deg, speed_mps, heading_deg,
                              x_m, y_m, z_m, rerr_m ];

            azmPort = new AZMPort(BaudRate.baudRate9600)
            {
                IsTryAlways = true,
                IsLogIncoming = true
            };

            azmPort.DetectedChanged += (o, e) =>
            {
                LogEventHandler?.Invoke(o, new LogEventArgs(LogLineType.INFO, string.Format("AZM Detected={0}", azmPort.Detected)));

                if (azmPort.Detected)
                {
                    if (IsUseAUX1 && (aux1Port != null) && !aux1Port.IsActive)
                    {
                        aux1Port.ProposedPortName = aux1PreferredPortName;
                        aux1Port.Start();
                    }
                }

            };
            azmPort.DeviceInfoValidChanged += (o, e) =>
            {
                if (azmPort.IsDeviceInfoValid && 
                    ((azmPort.DeviceType == AZM_DEVICE_TYPE_Enum.DT_USBL_TSV) ||
                     (azmPort.DeviceType == AZM_DEVICE_TYPE_Enum.DT_LBL_TSV)))
                {
                    LogEventHandler?.Invoke(o, new LogEventArgs(LogLineType.INFO,
                        string.Format(CultureInfo.InvariantCulture,
                        "Querying to start polling (AddrMask={0}, Salinity={1:F01} PSU, MaxDist={2:F01} m)",
                        addrMask, salinity_PSU, maxDist_m)));

                    azmPort.Query_BaseStart(addrMask, salinity_PSU, maxDist_m);
                    prevRemAckTS = DateTime.Now;
                    polling_started_received = false;
                }

                if (azmPort.IsDeviceInfoValid)
                {
                    DeviceType = azmPort.DeviceType;
                    DeviceSerialNumber = azmPort.SerialNumber;
                    DeviceVersionInfo = string.Format("{0} v{1}", azmPort.SystemInfo, azmPort.SystemVersion);
                }
                else
                {
                    DeviceType = AZM_DEVICE_TYPE_Enum.DT_INVALID;
                    DeviceSerialNumber = string.Empty;
                    DeviceVersionInfo = string.Empty;
                }
            };
            azmPort.ACKReceived += (o, e) =>
            {
                if ((e.SentenceID == ICs.IC_D2D_STRSTP) && (e.ResultID != IC_RESULT_Enum.IC_RES_OK))
                {
                    LogEventHandler?.Invoke(o, new LogEventArgs(LogLineType.ERROR, string.Format("IC_D2D_STRSTP caused a \"{0}\" error, retrying...", e.ResultID)));
                    LogEventHandler?.Invoke(o, new LogEventArgs(LogLineType.INFO,
                        string.Format("Querying to start polling (AddrMask={0} ({1}), Salinity={2:F01} PSU, MaxDist={3:F01} m)", 
                        addrMask,
                        Convert.ToString(addrMask, 2).PadLeft(16, '0'),
                        salinity_PSU, 
                        maxDist_m)));

                    azmPort.Query_BaseStart(addrMask, salinity_PSU, maxDist_m);
                    prevRemAckTS = DateTime.Now;
                    polling_started_received = false;
                }
            };
            azmPort.IsActiveChanged += (o, e) => LogEventHandler?.Invoke(o, new LogEventArgs(LogLineType.INFO, string.Format("Active={0}", azmPort.IsActive)));
            azmPort.LogEventHandler += (o, e) => LogEventHandler?.Invoke(o, e);
            azmPort.NDTAReceived += (o, e) =>
            {
                ProcessStationLocalParameters(e);

                if (e.Status == NDTA_Status_Enum.NDTA_REMT) // Remote timeout
                {
                    SetRemoteTimeoutStatus(e.Address);
                    prevRemAckTS = DateTime.Now;
                }
                else if (e.Status == NDTA_Status_Enum.NDTA_REMR) // Remote response
                {
                    if (DeviceType == AZM_DEVICE_TYPE_Enum.DT_USBL_TSV)
                    {
                        ProcessRemoteUSBL(e);
                    }
                    else if (DeviceType == AZM_DEVICE_TYPE_Enum.DT_LBL_TSV)
                    {
                        ProcessRemoteLBL(e);
                    }                    
                    prevRemAckTS = DateTime.Now;
                }

                if (e.Status == NDTA_Status_Enum.NDTA_REMT || e.Status == NDTA_Status_Enum.NDTA_REMR)
                {
                    ProcessIUDP(e.Address);
                }

                if (azmPort.IsActive &&
                    polling_started_received &&
                    (DateTime.Now.Subtract(prevRemAckTS).Seconds > 5))
                {
                    polling_started_received = false;
                    LogEventHandler?.Invoke(this,
                        new LogEventArgs(LogLineType.ERROR, "Remote action timeout (Short-term power shutdown?) , restarting polling..."));
                    azmPort.Query_BaseStart(addrMask, salinity_PSU, maxDist_m);
                }

            };
            azmPort.STRSTPReceived += (o, e) =>
            {
                InterrogationActive = e.AddrMask != 0;

                if (e.AddrMask == 0)
                {
                    LogEventHandler?.Invoke(o,
                        new LogEventArgs(LogLineType.INFO, "Interrogation paused..."));

                    polling_started_received = false;
                }
                else
                {
                    LogEventHandler?.Invoke(o,
                        new LogEventArgs(LogLineType.INFO,
                            string.Format(CultureInfo.InvariantCulture,
                            "Polling started (AddrMask={0} ({1}), Salinity={2:F01} PSU, SoundSpeed={3}, MaxDist={4:F01} m)",
                            e.AddrMask,
                            Convert.ToString(e.AddrMask, 2).PadLeft(16, '0'),
                            e.Sty_PSU,
                            double.IsNaN(e.SoundSpeed_mps) ? "Auto" : string.Format(CultureInfo.InvariantCulture, "{0:F01} m/s", e.SoundSpeed_mps),
                            e.MaxDist_m)));

                    polling_started_received = true;
                    prevRemAckTS = DateTime.Now;
                }
            };
            azmPort.RSTSReceived += (o, e) => RSTSReceivedHandler?.Invoke(o, e);           

            pTimer = new System.Timers.Timer
            {
                Interval = 1000,
                AutoReset = true
            };
            pTimer.Elapsed += (o, e) =>
            {
                lat_deg.Value = LatitudeOverride;
                lon_deg.Value = LongitudeOverride;
                heading_deg.Value = HeadingOverride;
            };
        }

        public bool Connect()
        {
            bool result = true;

            if (IsUseAUX1 && ((aux1Port != null) && aux1Port.IsActive))
                aux1Port.Stop();

            if (IsUseAUX2 && ((aux2Port != null) && aux2Port.IsActive))
                aux2Port.Stop();

            if (IsUseSerialOutput && serialOutput != null && !serialOutput.IsOpen)
            {
                try
                {
                    serialOutput.Open();
                }
                catch (Exception ex)
                {
                    LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, ex));
                }
            }

            if ((azmPort != null) && !azmPort.IsActive)
            {
                azmPort.Start();
                polling_started_received = false;
            }

            return result;
        }

        public bool Disconnect()
        {
            bool result = true;

            if ((azmPort != null) && azmPort.IsActive)
            {
                azmPort.Query_BaseStop();
                azmPort.Stop();

                if (IsUseAUX1 && ((aux1Port != null) && aux1Port.IsActive))
                    aux1Port.Stop();

                if (IsUseAUX2 && ((aux2Port != null) && aux2Port.IsActive))
                    aux2Port.Stop();

                if (IsUseSerialOutput && serialOutput != null && serialOutput.IsOpen)
                {
                    try
                    {
                        serialOutput.Close();
                    }
                    catch (Exception ex)
                    {
                        LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, ex));
                    }
                }

            }

            return result;
        }


        public void AUX1Init(BaudRate baudrate)
        {
            if (!IsUseAUX1)
            {
                IsUseAUX1 = true;

                aux1Port = new uGNSSSerialPort(baudrate)
                {
                    IsLogIncoming = true,
                    IsTryAlways = true,
                    MagneticOnly = false
                };

                aux1Port.DetectedChanged += (o, e) =>
                {
                    if (aux1Port.Detected)
                    {
                        if (IsUseAUX2 && ((aux2Port != null) && !aux2Port.IsActive))
                        {
                            aux2Port.ProposedPortName = aux2PreferredPortName;
                            aux2Port.Start();
                        }
                    }

                };

                aux1Port.HeadingUpdated += (o, e) => heading_deg.Value = aux1Port.Heading;

                aux1Port.LocationUpdated += (o, e) =>
                {
                    lat_deg.Value = aux1Port.Latitude;
                    lon_deg.Value = aux1Port.Longitude;

                    if (!double.IsNaN(aux1Port.CourseOverGround))
                        course_deg.Value = aux1Port.CourseOverGround;

                    if (!double.IsNaN(aux1Port.GroundSpeed))
                        speed_mps.Value = aux1Port.GroundSpeed;
                };

                aux1Port.LogEventHandler += (o, e) => LogEventHandler?.Rise(o, e);
            }
        }

        public void AUX2Init(BaudRate baudrate)
        {
            if (!IsUseAUX2)
            {
                IsUseAUX2 = true;

                aux2Port = new uMagneticCompassPort(baudrate)
                {
                    IsLogIncoming = true,
                    IsTryAlways = true
                };

                aux2Port.HeadingUpdated += (o, e) => heading_deg.Value = aux2Port.Heading;
                aux2Port.LogEventHandler += (o, e) => LogEventHandler?.Rise(o, e);
            }
        }

        public void SerialOutputInit(string portName, BaudRate baudRate)
        {
            try
            {
                serialOutput = new NMEASerialPort(
                    new SerialPortSettings(portName, baudRate,
                    System.IO.Ports.Parity.None, DataBits.dataBits8, System.IO.Ports.StopBits.One, System.IO.Ports.Handshake.None));
            }
            catch (Exception ex)
            {
                LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, ex));
            }
        }

        public void UDPOutputInit(IPEndPoint udpoutputEndpoint)
        {
            try
            {
                udpOutput = new UDPTranslator(udpoutputEndpoint.Port, udpoutputEndpoint.Address);
            }
            catch (Exception ex)
            {
                LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, ex));
            }
        }


        public bool SetResponderIndividualUDPChannel(REMOTE_ADDR_Enum raddr, IPEndPoint rEndPoint)
        {
            bool result = false;

            try
            {
                remotes[raddr].InitIUDPOutput(rEndPoint);
                result = true;
            }
            catch (Exception)
            {

            }

            return result;
        }

        public bool DiscardResponderInvidualUPDChannel(REMOTE_ADDR_Enum raddr)
        {
            if (remotes.TryGetValue(raddr, out ResponderBeacon? value))
                value.DeInitUDPOutput();

            return true;
        }


        public void ToUDPOutput(string line)
        {
            try
            {
                udpOutput?.Send(line);
                LogEventHandler?.Rise(this,
                    new LogEventArgs(LogLineType.INFO,
                    string.Format("{0}:{1} ({2}) << {3}", udpOutput?.Address, udpOutput?.Port, "UDP_OUT", line)));
            }
            catch (Exception ex)
            {
                LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, ex));
            }
        }

        public void ToSerialOutput(string line)
        {
            try
            {
                serialOutput?.SendData(line);
                LogEventHandler?.Rise(this,
                    new LogEventArgs(LogLineType.INFO,
                    string.Format("{0} ({1}) << {2}", serialOutput?.PortName, "SERIAL_OUT", line)));
            }
            catch (Exception ex)
            {
                LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, ex));
            }
        }


        public bool PauseInterrogation()
        {
            bool result = false;

            if (azmPort != null)
            {
                try
                {
                    result = azmPort.Query_BaseStart(0, Salinity_PSU, MaxDist_m);
                }
                catch (Exception e)
                {
                    LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, e));
                }
            }

            return result;
        }

        public bool ResumeInterrogation()
        {
            bool result = false;

            if (azmPort != null)
            {
                try
                {
                    prevRemAckTS = DateTime.Now;
                    result = azmPort.Query_BaseStart(AddressMask, Salinity_PSU, MaxDist_m);
                }
                catch (Exception e)
                {
                    LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, e));
                }
            }

            return result;
        }


        public bool LocationOverrideEnable(double lt_deg, double ln_deg, double hdn_deg)
        {
            if (pTimer.Enabled)
                pTimer.Stop();

            LatitudeOverride = lt_deg;
            LongitudeOverride = ln_deg;
            HeadingOverride = hdn_deg;

            pTimer.Start();

            return true;
        }

        public bool LocationOverrideDisable()
        {
            pTimer.Stop();
            return true;
        }

        public bool IsDeviceDetected(string deviceID)
        {
            bool result = false;

            if (deviceID == "AZM")
                result = AZMDetected;
            else if (deviceID == "AUX1")
                result = AUX1Detected;
            else if (deviceID == "AUX2")
                result = AUX2Detected;

            return result;
        }


        public bool CREQ(REMOTE_ADDR_Enum remoteAddr, CDS_REQ_CODES_Enum dataID)
        {
            if (azmPort == null)
                return false;
            else
                return azmPort.Query_CREQ(remoteAddr, dataID);
        }

        public bool QueryLocalAddress()
        {
            if (azmPort == null)
                return false;
            else
                return azmPort.Query_RSTS(0, double.NaN);
        }

        public bool QueryLocalAddressSet(REMOTE_ADDR_Enum address)
        {
            if (azmPort == null)
                return false;
            else
                return azmPort.Query_RSTS(address, double.NaN);
        }

        private void SetRemoteTimeoutStatus(REMOTE_ADDR_Enum address)
        {
            if (!remotes.ContainsKey(address))
                remotes.Add(address, new ResponderBeacon(address));

            remotes[address].IsTimeout = true;
            remotes[address].Timeouts++;

            if (remotes[address].Azimuth_deg.IsInitialized &&
                remotes[address].SRangeProjection_m.IsInitialized)
            {
                OutputHandler?.Rise(this, new StringEventArgs(remotes[address].ToString()));
            }
        }

        private static void CalcAbsLocation(double olat_rad, double olon_rad,
            double azm_rad, double dst_m,
            out double rlat_rad, out double rlon_rad, out double razm_rad)
        {
            if (!Algorithms.VincentyDirect(olat_rad, olon_rad, azm_rad, dst_m,
                Algorithms.WGS84Ellipsoid,
                Algorithms.VNC_DEF_EPSILON, Algorithms.VNC_DEF_IT_LIMIT,
                out rlat_rad, out rlon_rad, out razm_rad, out _))
            {
                Algorithms.HaversineDirect(olat_rad, olon_rad, dst_m, azm_rad,
                    Algorithms.WGS84Ellipsoid.MajorSemiAxis_m,
                    out rlat_rad, out rlon_rad);

                razm_rad = Algorithms.Wrap2PI(azm_rad + Math.PI);
            }
        }

        private void ProcessIUDP(REMOTE_ADDR_Enum raddr)
        {
            if (remotes.TryGetValue(raddr, out ResponderBeacon? value) && value.IsIUDPInitialized)
            {
                var nline = value.ToNMEAStrings();
                bool sent = false;

                try
                {
                    value.SendToIUDP(nline);
                    sent = true;
                }
                catch (Exception ex)
                {
                    LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, ex));
                }

                if (sent)
                    LogEventHandler?.Rise(this,
                        new LogEventArgs(LogLineType.INFO,
                        string.Format("{0} (IUDP_{1}) << {2}",
                        value.UDPEndpointDescription, raddr, nline)));
            }
        }

        /// <summary>
        /// The method processes the following properties of the specified (by address) ResponderBeacon:
        /// - IsTimeout
        /// - SuccededRequests
        /// - VCC_V
        /// - MSR_dB
        /// - WaterTemp_C
        /// - PTime_s
        /// - SRange_m
        /// - SRangeProjection_m
        /// - Depth_m
        /// 
        /// Handles:
        /// - custom user requests
        /// - error codes
        /// 
        /// </summary>
        /// <param name="e"></param>
        /// <param name="is_srp"></param>
        private void ProcessCommonItems(NDTAReceivedEventArgs e, out bool is_srp)
        {
            is_srp = false;

            if (!remotes.ContainsKey(e.Address))
                remotes.Add(e.Address, new ResponderBeacon(e.Address));

            remotes[e.Address].IsTimeout = false;
            remotes[e.Address].SuccededRequests++;

            if (AZM.IsUserDataReqCode(e.RequestCode))
            {
                CREQResultHandler?.Invoke(this, new CREQResultEventArgs(e.Address, e.RequestCode, e.ResponseCode));
            }

            if (AZM.IsErrorCode(e.ResponseCode))
            {
                CDS_RESP_CODES_Enum rError = (CDS_RESP_CODES_Enum)Enum.ToObject(typeof(CDS_RESP_CODES_Enum), e.ResponseCode);

                if ((rError == CDS_RESP_CODES_Enum.CDS_ERR_BAT_LOW) ||
                    (rError == CDS_RESP_CODES_Enum.CDS_RSYS_STRT))
                    remotes[e.Address].Message.Value = rError.ToString().Replace("CDS", "").Replace('_', ' ');
                else
                    remotes[e.Address].Message.Value = string.Format("{0} caused {1}", e.RequestCode, rError);

            }
            else if (e.RequestCode == CDS_REQ_CODES_Enum.CDS_REQ_VCC)
            {
                remotes[e.Address].VCC_V.Value = (double)(e.ResponseCode) * (AZM.ABS_MAX_VCC_V - AZM.ABS_MIN_VCC_V) / AZM.CRANGE + AZM.ABS_MIN_VCC_V;
            }
            else if (e.RequestCode == CDS_REQ_CODES_Enum.CDS_REQ_TMP)
            {
                remotes[e.Address].WaterTemp_C.Value = (double)(e.ResponseCode) * (AZM.ABS_MAX_TEMP_C - AZM.ABS_MIN_TEMP_C) / AZM.CRANGE + AZM.ABS_MIN_TEMP_C;
            }


            if (!double.IsNaN(e.MSR_dB))
                remotes[e.Address].MSR_dB.Value = e.MSR_dB;

            if (!double.IsNaN(e.RemotesDepth_m))
            {
                remotes[e.Address].Depth_m.Value = e.RemotesDepth_m;
                remotes[e.Address].Z_m.Value = e.RemotesDepth_m;
            }

            bool ignoreRange = false;

            if (!double.IsNaN(e.PropTime_s))
            {
                remotes[e.Address].PTime_s.Value = e.PropTime_s;

                if (IsRecalculateRange)
                {
                    ignoreRange = true;
                    is_srp = true;

                    if (DeviceType == AZM_DEVICE_TYPE_Enum.DT_LBL_TSV)
                    {
                        if (IsPTimeAdjustment)
                        {
                            remotes[e.Address].SRange_m.Value = 
                                (e.PropTime_s - PTimePerAddrAdjustment_s  - PTimePerAddrAdjustment2_s * (int)e.Address) * wpManager.SoundSpeed;
                        }
                        else
                            remotes[e.Address].SRange_m.Value = e.PropTime_s * wpManager.SoundSpeed;
                    }
                    else
                    {
                        remotes[e.Address].SRange_m.Value = e.PropTime_s * wpManager.SoundSpeed;
                    }

                    if (stDepth_m.IsInitialized && remotes[e.Address].Depth_m.IsInitialized)
                    {
                        double delta_d_m = Math.Abs(stDepth_m.Value - remotes[e.Address].Depth_m.Value);
                        if (delta_d_m <= remotes[e.Address].SRange_m.Value)
                            remotes[e.Address].SRangeProjection_m.Value = 
                                Math.Sqrt(remotes[e.Address].SRange_m.Value * remotes[e.Address].SRange_m.Value - delta_d_m * delta_d_m);
                        else
                            remotes[e.Address].SRangeProjection_m.Value = remotes[e.Address].SRange_m.Value;
                    }
                    else
                    {
                        remotes[e.Address].SRangeProjection_m.Value = remotes[e.Address].SRange_m.Value;
                    }
                }
            }

            if (!ignoreRange && !double.IsNaN(e.SlantRange_m))
                remotes[e.Address].SRange_m.Value = e.SlantRange_m;

            if (!ignoreRange)
            {
                if (!double.IsNaN(e.SlantRangeProjection_m))
                {
                    remotes[e.Address].SRangeProjection_m.Value = e.SlantRangeProjection_m;
                    is_srp = true;
                }
                else
                {
                    if (!double.IsNaN(e.SlantRange_m))
                    {
                        remotes[e.Address].SRangeProjection_m.Value = e.SlantRange_m;
                        is_srp = true;
                    }
                }
            }            
        }


        private void ProcessRemoteUSBL(NDTAReceivedEventArgs e)
        {
            if (!remotes.ContainsKey(e.Address))
                remotes.Add(e.Address, new ResponderBeacon(e.Address));

            bool is_a = false;

            ProcessCommonItems(e, out bool is_srp);

            if (!double.IsNaN(e.HAngle_deg))
            {
                remotes[e.Address].Azimuth_deg.Value = e.HAngle_deg;
                is_a = true;
            }

            if (!double.IsNaN(e.VAngle_deg))
                remotes[e.Address].Elevation_deg.Value = e.VAngle_deg;

            if (is_a && is_srp)
            {
                if (lat_deg.IsInitializedAndNotObsolete &&
                    lon_deg.IsInitializedAndNotObsolete &&
                    heading_deg.IsInitializedAndNotObsolete)
                {
                    PolarCS_ShiftRotate(heading_deg.Value, phi_deg,
                        remotes[e.Address].Azimuth_deg.Value,
                        remotes[e.Address].SRangeProjection_m.Value,
                        x_offset_m, y_offset_m,
                        out double a_azm, out double a_rng);

                    CalcAbsLocation(
                        Algorithms.Deg2Rad(lat_deg.Value),
                        Algorithms.Deg2Rad(lon_deg.Value),
                        Algorithms.Deg2Rad(a_azm), a_rng,
                        out double rlat_rad,
                        out double rlon_rad,
                        out double _);

                    DateTime ts = DateTime.Now;

                    if (remotes[e.Address].DHFilterState == null)
                        remotes[e.Address].DHFilterState = new DHTrackFilter(8, 1, 5);

                    if (remotes[e.Address].DHFilterState.Process(rlat_rad, rlon_rad, 0, ts,
                            out rlat_rad, out rlon_rad, out _, out _))
                    {

                        if (remotes[e.Address].TFilterState == null)
                            remotes[e.Address].TFilterState = new TrackMovingAverageSmoother(4, 20);

                        double rdpt_m = remotes[e.Address].Depth_m.IsInitialized ? remotes[e.Address].Depth_m.Value : 0;

                        remotes[e.Address].TFilterState?.Process(
                            rlat_rad, rlon_rad, rdpt_m, DateTime.Now,
                            out rlat_rad, out rlon_rad, out _, out _);


                        remotes[e.Address].AAzimuth_deg.Value = a_azm;
                        remotes[e.Address].RAzimuth_deg.Value = Algorithms.Wrap360(a_azm + 180);
                        remotes[e.Address].ADistance_m.Value = a_rng;

                        double rlat_deg = Algorithms.Rad2Deg(rlat_rad);
                        double rlon_deg = Algorithms.Rad2Deg(rlon_rad);

                        remotes[e.Address].Lat_deg.Value = rlat_deg;
                        remotes[e.Address].Lon_deg.Value = rlon_deg;
                    }
                }
                else
                {
                    remotes[e.Address].RAzimuth_deg.Value = Algorithms.Wrap360(remotes[e.Address].Azimuth_deg.Value + 180);
                }
            }

            OutputHandler?.Rise(this, new StringEventArgs(remotes[e.Address].ToString()));
        }       

        public bool Set3RespondersGeographicCoordinates(double r1x, double r1y, double r2x, double r2y, double r3x, double r3y)
        {
            throw new NotImplementedException();
        }

        public bool Discard3RespondersCoordinates()
        {
            throw new NotImplementedException();
        }

        public bool Set3RespondersLocalCoordinates(double r1x, double r1y, double r2x, double r2y, double r3x, double r3y)
        {
            if (double.IsNaN(r1x) || double.IsNaN(r1y) || 
                double.IsNaN(r2x) || double.IsNaN(r2y) || 
                double.IsNaN(r3x) || double.IsNaN(r3y))
                return false;

            if (!remotes.ContainsKey(REMOTE_ADDR_Enum.REM_ADDR_1))
                remotes.Add(REMOTE_ADDR_Enum.REM_ADDR_1, new ResponderBeacon(REMOTE_ADDR_Enum.REM_ADDR_1));

            remotes[REMOTE_ADDR_Enum.REM_ADDR_1].X_m.Value = r1x;
            remotes[REMOTE_ADDR_Enum.REM_ADDR_1].Y_m.Value = r1y;

            if (!remotes.ContainsKey(REMOTE_ADDR_Enum.REM_ADDR_2))
                remotes.Add(REMOTE_ADDR_Enum.REM_ADDR_2, new ResponderBeacon(REMOTE_ADDR_Enum.REM_ADDR_2));

            remotes[REMOTE_ADDR_Enum.REM_ADDR_2].X_m.Value = r2x;
            remotes[REMOTE_ADDR_Enum.REM_ADDR_2].Y_m.Value = r2y;

            if (!remotes.ContainsKey(REMOTE_ADDR_Enum.REM_ADDR_3))
                remotes.Add(REMOTE_ADDR_Enum.REM_ADDR_3, new ResponderBeacon(REMOTE_ADDR_Enum.REM_ADDR_3));

            remotes[REMOTE_ADDR_Enum.REM_ADDR_3].X_m.Value = r3x;
            remotes[REMOTE_ADDR_Enum.REM_ADDR_3].Y_m.Value = r3y;

            return true;
        }        

        private void ProcessRemoteLBL(NDTAReceivedEventArgs e)
        {
            if (!remotes.ContainsKey(e.Address))
                remotes.Add(e.Address, new ResponderBeacon(e.Address));

            ProcessCommonItems(e, out bool is_srp);

            if (is_srp)
            {
                if (remotes[e.Address].X_m.IsInitialized &&
                    remotes[e.Address].Y_m.IsInitialized &&
                    remotes[e.Address].Z_m.IsInitialized)
                {
                    lblProcessor.UpdatePoint(e.Address,
                        remotes[e.Address].X_m.Value,
                        remotes[e.Address].Y_m.Value,
                        remotes[e.Address].Z_m.Value,
                        remotes[e.Address].SRangeProjection_m.Value);

                    if (lblProcessor.CanFormNavigationBase())
                    {                        
                        var basepoints = lblProcessor.GetValidPointsForSolver();

                        double x_prev = x_m.IsInitialized ? x_m.Value : double.NaN;
                        double y_prev = y_m.IsInitialized ? y_m.Value : double.NaN;

                        if (double.IsNaN(x_prev) || double.IsNaN(y_prev))
                        {
                            x_prev = 0;
                            y_prev = 0;

                            foreach (var point in basepoints)
                            {
                                x_prev += point.X;
                                y_prev += point.Y;
                            }

                            x_prev /= basepoints.Count();
                            y_prev /= basepoints.Count();
                        }

                        Algorithms.TOA_NLM2D_Solve(basepoints.ToArray(), x_prev, y_prev, z_m.Value,
                            Algorithms.NLM_DEF_IT_LIMIT, Algorithms.NLM_DEF_PREC_THRLD, 1.0,
                            out double x_curr, out double y_curr, out double rerr, out int itcnt);

                        x_m.Value = x_curr;
                        y_m.Value = y_curr;
                        rerr_m.Value = rerr;

                    }
                }
            }
            
            OutputHandler?.Rise(this, new StringEventArgs(remotes[e.Address].ToString()));
        }


        /// <summary>
        /// All angles clockwise from the North direction
        /// </summary>
        /// <param name="heading_deg">Compass reading, 0-360° clockwise from North direction</param>
        /// <param name="phi_deg">Antenna - comрass zero directions difference, °</param>
        /// <param name="bearing_deg">Bearing to a responder, 0-360° clockwise from North direction</param>
        /// <param name="r_m">slant range projection, m</param>
        /// <param name="xt">transversal GNSS/antenna offset</param>
        /// <param name="yt">longitudal GNSS/antenna offset</param>
        /// <param name="a_deg">Absolute azimuth to the responder</param>
        /// <param name="r_a">Range to the responder (from the GNSS position)</param>
        private static void PolarCS_ShiftRotate(double heading_deg, double phi_deg, double bearing_deg,
            double r_m, double xt, double yt,
            out double a_deg, out double r_a)
        {
            double teta = Algorithms.Wrap2PI(Algorithms.Deg2Rad(bearing_deg + phi_deg));

            double xr = xt + r_m * Math.Sin(teta);
            double yr = yt + r_m * Math.Cos(teta);

            r_a = Math.Sqrt(xr * xr + yr * yr);

            double a_r = Math.Atan2(xr, yr);
            if (a_r < 0)
                a_r += 2 * Math.PI;

            a_r += Algorithms.Deg2Rad(heading_deg);
            a_r = Algorithms.Wrap2PI(a_r);

            a_deg = Algorithms.Rad2Deg(a_r);
        }


        public string GetStationParametersToStringFormat()
        {
            StringBuilder sb = new();

            sb.Append("@AZMLOC,");

            foreach (IAging avalue in stationParams)
            {
                Utils.AppendAgingValueDesciption(sb, avalue);
            }

            return sb.ToString();
        }

        private string StationParametersToString()
        {
            StringBuilder sb = new();

            sb.Append("@AZMLOC,");

            foreach (IAging avalue in stationParams)
            {
                Utils.AppendAgingValue(sb, avalue);
            }

            return sb.ToString();
        }

        private string BuildNMEAOutput()
        {
            StringBuilder sb = new();

            var ltCardinal = lat_deg.IsInitializedAndNotObsolete ? (lat_deg.Value > 0 ? "N" : "S") : string.Empty;
            var lnCardinal = lon_deg.IsInitializedAndNotObsolete ? (lon_deg.Value > 0 ? "E" : "W") : string.Empty;

            bool location_valid = lat_deg.IsInitializedAndNotObsolete && lon_deg.IsInitializedAndNotObsolete;

            sb.Append(
                NMEAParser.BuildSentence(
                    TalkerIdentifiers.GN,
                    SentenceIdentifiers.RMC,
                    [
                        DateTime.UtcNow,
                        location_valid ? "Valid" : "Invalid",
                        location_valid ? lat_deg.Value : null, ltCardinal,
                        location_valid ? lon_deg.Value : null, lnCardinal,
                        null,
                        null,
                        DateTime.UtcNow,
                        null,
                        null,
                        location_valid ? "A" : "V",
                    ]));

            sb.Append(
                NMEAParser.BuildSentence(
                    TalkerIdentifiers.GN,
                    SentenceIdentifiers.GGA,
                    [
                        DateTime.UtcNow,
                        location_valid ? lat_deg.Value : null, ltCardinal,
                        location_valid ? lon_deg.Value : null, lnCardinal,
                        "GPS fix",
                        4,
                        null,
                        stDepth_m.IsInitializedAndNotObsolete ? -stDepth_m.Value : null,
                        "M",
                        null,
                        "M",
                        null,
                        null,
                    ]));

            sb.Append(
                NMEAParser.BuildSentence(
                    TalkerIdentifiers.GN,
                    SentenceIdentifiers.MTW,
                    [
                        waterTemp_C.IsInitializedAndNotObsolete ? waterTemp_C.Value : null,
                    ]));

            return sb.ToString();
        }

        private void ProcessStationLocalParameters(NDTAReceivedEventArgs e)
        {
            if (!double.IsNaN(e.LocTemp_C))
            {
                waterTemp_C.Value = e.LocTemp_C;
                wpManager.Temperature = e.LocTemp_C;
            }

            if (!double.IsNaN(e.LocPrs_mBar))
            {
                stPressure_mBar.Value = e.LocPrs_mBar;
                wpManager.Pressure = e.LocPrs_mBar;

                if (waterTemp_C.IsInitialized)
                {
                    double waterDensity_kgm3 =
                        PHX.Water_density_calc(waterTemp_C.Value,
                                               e.LocPrs_mBar,
                                               Salinity_PSU);

                    stDepth_m.Value = PHX.Depth_by_pressure_calc(e.LocPrs_mBar,
                        PHX.PHX_ATM_PRESSURE_MBAR, waterDensity_kgm3, PHX.PHX_GRAVITY_ACC_MPS2);                    

                    z_m.Value = stDepth_m.Value;
                }
            }

            if (!double.IsNaN(e.LocPitch_deg))
                stPitch_deg.Value = e.LocPitch_deg;
            if (!double.IsNaN(e.LocRoll_deg))
                stRoll_deg.Value = e.LocRoll_deg;

            OutputHandler?.Rise(this, new StringEventArgs(StationParametersToString()));

            if (IsStationNMEAOutputEnabled)
                OutputHandler?.Rise(this, new StringEventArgs(BuildNMEAOutput()));
        }

        public void Emulate(string eString)
        {
            string str = eString.Trim() + NMEAParser.SentenceEndDelimiter;

            var splits = str.Split(llSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (splits.Length == 3)
            {
                if (splits[1] == "(GNSS)")
                {
                    if (aux1Port == null)
                        AUX1Init(BaudRate.baudRate9600);

                    aux1Port?.EmulateInput(splits[2]);
                }
                else if (splits[1] == "(AZM)")
                {
                    azmPort?.EmulateInput(splits[2]);
                }
            }
        }

        public EventHandler<LogEventArgs>? LogEventHandler;
        public EventHandler? InterrogationActiveChangedHandler;
        public EventHandler<StringEventArgs>? OutputHandler;
        public EventHandler<CREQResultEventArgs>? CREQResultHandler;
        public EventHandler<RSTSReceivedEventArgs>? RSTSReceivedHandler;
        public EventHandler? DetectedChangedHandler;
        public EventHandler? ActiveChangedHandler;
    }
}
