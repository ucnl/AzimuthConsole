using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
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

    public class AZMCombiner : IDisposable
    {
        private bool _disposed = false;

        public bool ConnectionActive => azmPort?.IsActive == true;

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

        public bool AZMDetected => azmPort?.Detected == true;
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
        public bool AUX1Detected => aux1Port?.Detected == true;

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
        public bool AUX2Detected => aux2Port?.Detected == true;

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
        

        DHTrackFilterCartesian? lblDHFilter;
        TrackMovingAverageSmootherCartesian? lblSFilter;
        double lbl_rerr_threshold_m = 10.0;


        public bool IsRecalculateRange { get; set; } = true;
        public bool IsPTimeAdjustment { get; set; } = true;
        double PTimePerAddrAdjustment_s = 0.0006; // 
        double PTimePerAddrAdjustment2_s = 0.0004;

        readonly WPManager wpManager = new();

        double phi_deg;
        double x_offset_m;
        double y_offset_m;

        int usbl_dhfilter_fifo_size = 8;
        double usbl_dhfilter_maxspeed_mps = 1;
        double usbl_dhfilter_threshold_m = 5;
        int usbl_sfilter_fifo_size = 4;
        double usbl_sfilter_threshold_m = 100;

        public AZMTranscieverState state;



        readonly AZMPort? azmPort;
        GNSSWrapper? aux1Port;
        uMagneticCompassPort? aux2Port;
        NMEASerialPort? serialOutput;
        UDPTranslator? udpOutput;

        public ConcurrentDictionary<REMOTE_ADDR_Enum, ResponderBeacon> remotes;

        bool polling_started_received = false;
        DateTime prevRemAckTS = DateTime.Now;

        readonly System.Timers.Timer pTimer;
        readonly string[] llSeparators = [">>", " "];

        readonly LBLProcessor lblProcessor = new();

        public AZMCombiner(SettingsContainer settings)
        {
            DeviceSerialNumber = string.Empty;
            DeviceVersionInfo = string.Empty;

            ValidateMainParameters(settings);

            InitializeFilters(settings);

            wpManager.IsAutoSoundSpeed = true;
            wpManager.IsAutoSalinity = false;
            wpManager.Salinity = Salinity_PSU;

            AddressMask = settings.address_mask;

            x_offset_m = settings.antenna_x_offset_m;
            y_offset_m = settings.antenna_y_offset_m;

            remotes = new ConcurrentDictionary<REMOTE_ADDR_Enum, ResponderBeacon>();
            state = new AZMTranscieverState();            

            azmPort = new AZMPort(BaudRate.baudRate9600)
            {
                IsTryAlways = true,
                IsLogIncoming = true
            };
            SubscribeToEvents();
            InitializeAuxiliaryConnections(settings);

            if (settings.LBLResponderCoordinatesMode == LBLResponderCoordinatesModeEnum.Cartesian)
            {
                Set3RespondersLocalCoordinates(
                    settings.LBLModeR1Coordinates.X, settings.LBLModeR1Coordinates.Y,
                    settings.LBLModeR2Coordinates.X, settings.LBLModeR2Coordinates.Y,
                    settings.LBLModeR3Coordinates.X, settings.LBLModeR3Coordinates.Y);
            } else if (settings.LBLResponderCoordinatesMode == LBLResponderCoordinatesModeEnum.Geographic)
            {
                Set3RespondersGeographicCoordinates(
                    settings.LBLModeR1Coordinates.X, settings.LBLModeR1Coordinates.Y,
                    settings.LBLModeR2Coordinates.X, settings.LBLModeR2Coordinates.Y,
                    settings.LBLModeR3Coordinates.X, settings.LBLModeR3Coordinates.Y);
            }


            pTimer = new System.Timers.Timer
            {
                Interval = 1000,
                AutoReset = true
            };

            pTimer.Elapsed += (o, e) =>
            {
                if (_disposed) return;

                state.Lat_deg.Value = LatitudeOverride;
                state.Lon_deg.Value = LongitudeOverride;
                state.Heading_deg.Value = HeadingOverride;
            };
        }

        private void ValidateMainParameters(SettingsContainer settings)
        {
            if (AZM.IsStyPSU(settings.sty_PSU))
                Salinity_PSU = settings.sty_PSU;
            else
                throw new ArgumentOutOfRangeException(nameof(settings.sty_PSU));

            if (AZM.IsHdnDeg(settings.antenna_angular_offset_deg))
                phi_deg = settings.antenna_angular_offset_deg;
            else
                throw new ArgumentOutOfRangeException(nameof(settings.antenna_angular_offset_deg));

            if (AZM.IsMaxDst(settings.max_dist_m))
                MaxDist_m = settings.max_dist_m;
            else
                throw new ArgumentOutOfRangeException(nameof(settings.max_dist_m));
        }

        private void InitializeFilters(SettingsContainer settings)
        {
            if (settings.LBLMode_Use_DHFilter)
                lblDHFilter = new DHTrackFilterCartesian(
                    settings.LBLMode_DHFilter_FIFO_Size,
                    settings.LBLMode_DHFilter_MaxSpeed_mps,
                    settings.LBLMode_DHFilter_Threshold_m);

            if (settings.LBLMode_Use_SFilter)
                lblSFilter = new TrackMovingAverageSmootherCartesian(
                    settings.LBLMode_SFilter_FIFO_Size,
                    settings.LBLMode_SFilter_Threshold_m);

            usbl_dhfilter_fifo_size = settings.USBLMode_DHFilter_FIFO_Size;
            usbl_dhfilter_maxspeed_mps = settings.USBLMode_DHFilter_MaxSpeed_mps;
            usbl_dhfilter_threshold_m = settings.USBLMode_DHFilter_Threshold_m;

            usbl_sfilter_fifo_size = settings.USBLMode_SFilter_FIFO_Size;
            usbl_sfilter_threshold_m = settings.USBLMode_SFilter_Threshold;

            lbl_rerr_threshold_m = settings.LBLMode_RErr_Threshold_m;
        }

        private void InitializeAuxiliaryConnections(SettingsContainer settings)
        {
            if (settings.aux1Enabled)
            {
                AUX1PreferredPortName = settings.aux1PrefPortName;
                AUX1Init(settings.aux1PortBaudrate, settings.aux1Alternative);
            }

            if (settings.aux2Enabled)
            {
                AUX2PreferredPortName = settings.aux2PrefPortName;
                AUX2Init(settings.aux2PortBaudrate);
            }

            if (settings.outputSerialEnabled)
            {
                LogInfo($"Initializing SERIAL_OUT on {settings.outputSerialPortName}...");
                SerialOutputInit(settings.outputSerialPortName, settings.outputSerialPortBaudrate);
                OutputHandler += (o, e) => ToSerialOutput(e.Line);
            }

            if (settings.output_udp_enabled)
            {
                LogInfo($"Initializing UDP output UDP_OUT on {settings.output_endpoint}...");
                UDPOutputInit(settings.output_endpoint);
                OutputHandler += (o, e) => ToUDPOutput(e.Line);
            }

            if ((settings.InvidvidualEndpoints != null) && (settings.InvidvidualEndpoints.Count > 0))
            {
                foreach (var item in settings.InvidvidualEndpoints)
                {
                    LogInfo($"Initializing responder {item.Key} UDP output on {item.Value}...");
                    SetResponderIndividualUDPChannel(item.Key, item.Value);
                }
            }
        }

        private void SubscribeToEvents()
        {
            if (azmPort == null)
                return;

            azmPort.DetectedChanged += (o, e) =>
            {
                LogInfo($"AZM Detected={azmPort.Detected}");

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
                    LogInfo(string.Format(CultureInfo.InvariantCulture,
                        "Querying to start polling (AddrMask={0}, Salinity={1:F01} PSU, MaxDist={2:F01} m)",
                        AddressMask, Salinity_PSU, MaxDist_m));

                    azmPort.Query_BaseStart(AddressMask, Salinity_PSU, MaxDist_m);
                    prevRemAckTS = DateTime.Now;
                    polling_started_received = false;
                }

                if (azmPort.IsDeviceInfoValid)
                {
                    DeviceType = azmPort.DeviceType;
                    DeviceSerialNumber = azmPort.SerialNumber;
                    DeviceVersionInfo = $"{azmPort.SystemInfo} v{azmPort.SystemVersion}";
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
                    LogError($"IC_D2D_STRSTP caused a \"{e.ResultID}\" error, retrying...");
                    LogInfo(string.Format(CultureInfo.InvariantCulture, "Querying to start polling (AddrMask={0} ({1}), Salinity={2:F01} PSU, MaxDist={3:F01} m)",
                        AddressMask,
                        Convert.ToString(AddressMask, 2).PadLeft(16, '0'),
                        Salinity_PSU,
                        MaxDist_m));

                    azmPort.Query_BaseStart(AddressMask, Salinity_PSU, MaxDist_m);
                    prevRemAckTS = DateTime.Now;
                    polling_started_received = false;
                }
            };
            azmPort.IsActiveChanged += (o, e) => LogInfo($"Active={azmPort.IsActive}");
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
                    LogError("Remote action timeout (Short-term power shutdown?) , restarting polling...");                    
                    azmPort.Query_BaseStart(AddressMask, Salinity_PSU, MaxDist_m);
                }

            };
            azmPort.STRSTPReceived += (o, e) =>
            {
                InterrogationActive = e.AddrMask != 0;

                if (e.AddrMask == 0)
                {
                    LogInfo("Interrogation paused...");
                    polling_started_received = false;
                }
                else
                {
                    LogInfo(string.Format(CultureInfo.InvariantCulture,
                        "Polling started (AddrMask={0} ({1}), Salinity={2:F01} PSU, SoundSpeed={3}, MaxDist={4:F01} m)",
                        e.AddrMask,
                        Convert.ToString(e.AddrMask, 2).PadLeft(16, '0'),
                        e.Sty_PSU,
                        double.IsNaN(e.SoundSpeed_mps) ? "Auto" : string.Format(CultureInfo.InvariantCulture, "{0:F01} m/s", e.SoundSpeed_mps),
                        e.MaxDist_m));

                    polling_started_received = true;
                    prevRemAckTS = DateTime.Now;
                }
            };
            azmPort.RSTSReceived += (o, e) => RSTSReceivedHandler?.Invoke(o, e);
        }




        private bool IsAUX1UsedNotNullAndActive() =>
            IsUseAUX1 && (aux1Port != null) && aux1Port.IsActive;

        private bool IsAUX2UsedNotNullAndActive() =>
            IsUseAUX2 && (aux2Port != null) && aux2Port.IsActive;

        private bool IsAUX2UsedNotNullAndNotActive() =>
            IsUseAUX2 && (aux2Port != null) && !aux2Port.IsActive;

        private bool IsSerialOutputUsedNotNullAndNotOpen() =>
            IsUseSerialOutput && (serialOutput != null) && !serialOutput.IsOpen;

        private bool IsSerialOutputUsedNotNullAndOpen() =>
            IsUseSerialOutput && (serialOutput != null) && serialOutput.IsOpen;
        private bool IsAZMPortNotNullAndNotActive() =>
            (azmPort != null) && !azmPort.IsActive;

        private bool IsAZMPortNotNullAndActive() =>
            (azmPort != null) && azmPort.IsActive;

        private bool IsStationLocationAndHeadingValid() =>
            state.Lat_deg.IsInitializedAndNotObsolete &&
            state.Lon_deg.IsInitializedAndNotObsolete &&
            state.Heading_deg.IsInitializedAndNotObsolete;



        public bool Connect()
        {
            bool result = true;

            if (IsAUX1UsedNotNullAndActive())
                aux1Port?.Stop();

            if (IsAUX2UsedNotNullAndActive())
                aux2Port?.Stop();

            if (IsSerialOutputUsedNotNullAndNotOpen())
            {
                try
                {
                    serialOutput?.Open();
                }
                catch (Exception ex)
                {
                    LogError($"Failed to open SERIAL_OUT ({serialOutput?.PortName}): {ex.Message}");
                }
            }

            if (IsAZMPortNotNullAndNotActive())
            {
                azmPort?.Start();
                polling_started_received = false;
            }

            return result;
        }

        public bool Disconnect()
        {
            bool result = true;

            if (IsAZMPortNotNullAndActive())
            {
                azmPort?.Query_BaseStop();
                azmPort?.Stop();

                if (IsAUX1UsedNotNullAndActive())
                    aux1Port?.Stop();

                if (IsAUX2UsedNotNullAndActive())
                    aux2Port?.Stop();

                if (IsSerialOutputUsedNotNullAndOpen())
                {
                    try
                    {
                        serialOutput?.Close();
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to close SERIAL_OUT ({serialOutput?.PortName}): {ex.Message}");
                    }
                }

            }

            return result;
        }


        public void AUX1Init(BaudRate baudrate, bool alternative = false)
        {
            if (!IsUseAUX1)
            {
                IsUseAUX1 = true;

                aux1Port = new GNSSWrapper(baudrate, alternative)
                {
                    IsLogIncoming = true,
                    IsTryAlways = true,
                    MagneticOnly = false
                };

                aux1Port.DetectedChanged += (o, e) =>
                {
                    if (aux1Port.Detected)
                    {
                        if (IsAUX2UsedNotNullAndNotActive())
                        {
                            aux2Port.ProposedPortName = aux2PreferredPortName;
                            aux2Port?.Start();
                        }
                    }

                };

                aux1Port.HeadingUpdated += (o, e) => state.Heading_deg.Value = aux1Port.Heading;

                aux1Port.LocationUpdated += (o, e) =>
                {
                    state.Lat_deg.Value = aux1Port.Latitude;
                    state.Lon_deg.Value = aux1Port.Longitude;

                    if (!double.IsNaN(aux1Port.CourseOverGround))
                        state.Course_deg.Value = aux1Port.CourseOverGround;

                    if (!double.IsNaN(aux1Port.GroundSpeed))
                        state.Speed_mps.Value = aux1Port.GroundSpeed;
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

                aux2Port.HeadingUpdated += (o, e) => state.Heading_deg.Value = aux2Port.Heading;
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
                LogError($"Failed to init SERIAL_OUT ({portName}): {ex.Message}");
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
                LogError($"Failed to init UDP_OUT ({udpoutputEndpoint.Address}:{udpoutputEndpoint.Port}): {ex.Message}");
            }
        }

        public string GetStationParametersToStringFormat()
        {
            return state.GetStationParametersToStringFormat();
        }



        public bool SetResponderIndividualUDPChannel(REMOTE_ADDR_Enum raddr, IPEndPoint rEndPoint)
        {
            bool result = false;
            var r = remotes.GetOrAdd(raddr, addr => new ResponderBeacon(addr));

            try
            {
                r.InitIUDPOutput(rEndPoint);
                result = true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to set UDP channel for {raddr}: {ex.Message}");
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
                LogInfo($"{udpOutput?.Address}:{udpOutput?.Port} (UDP_OUT) << {line}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to write to UDP_OUT ({udpOutput?.Address}:{udpOutput?.Port}): {ex.Message}");
            }
        }

        public void ToSerialOutput(string line)
        {
            try
            {
                serialOutput?.SendData(line);
                LogInfo($"{serialOutput?.PortName} (SERIAL_OUT) << {line}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to write to SERIAL_OUT ({serialOutput?.PortName}): {ex.Message}");
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
                    LogError($"Failed to pause interrogation: {e.Message}");
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
                    LogError($"Failed to resume interrogation: {e.Message}");
                }
            }

            return result;
        }


        public bool LocationOverrideEnable(double lt_deg, double ln_deg, double hdn_deg)
        {
            if (_disposed) return false;

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
            if (_disposed) return false;

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
           
            var remote = remotes.GetOrAdd(address, addr => new ResponderBeacon(addr));

            remote.IsTimeout = true;
            remote.Timeouts++;

            if (remote.Azimuth_deg.IsInitialized &&
                remote.SRangeProjection_m.IsInitialized)
            {
                OutputHandler?.Rise(this, new StringEventArgs(remote.ToString()));
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
                    LogError($"Failed to send via IUDP for {raddr}: {ex.Message}");
                }

                if (sent)
                    LogInfo($"{value.UDPEndpointDescription} (IUDP_{raddr}) << {nline}");
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

            var remote = remotes.GetOrAdd(e.Address, addr => new ResponderBeacon(addr));

            remote.IsTimeout = false;
            remote.SuccededRequests++;

            if (AZM.IsUserDataReqCode(e.RequestCode))
            {
                CREQResultHandler?.Invoke(this, new CREQResultEventArgs(e.Address, e.RequestCode, e.ResponseCode));
            }

            if (AZM.IsErrorCode(e.ResponseCode))
            {
                CDS_RESP_CODES_Enum rError = (CDS_RESP_CODES_Enum)Enum.ToObject(typeof(CDS_RESP_CODES_Enum), e.ResponseCode);

                if ((rError == CDS_RESP_CODES_Enum.CDS_ERR_BAT_LOW) ||
                    (rError == CDS_RESP_CODES_Enum.CDS_RSYS_STRT))
                    remote.Message.Value = rError.ToString().Replace("CDS", "").Replace('_', ' ');
                else
                    remote.Message.Value = string.Format("{0} caused {1}", e.RequestCode, rError);

            }
            else if (e.RequestCode == CDS_REQ_CODES_Enum.CDS_REQ_VCC)
            {
                remote.VCC_V.Value = (double)(e.ResponseCode) * (AZM.ABS_MAX_VCC_V - AZM.ABS_MIN_VCC_V) / AZM.CRANGE + AZM.ABS_MIN_VCC_V;
            }
            else if (e.RequestCode == CDS_REQ_CODES_Enum.CDS_REQ_TMP)
            {
                remote.WaterTemp_C.Value = (double)(e.ResponseCode) * (AZM.ABS_MAX_TEMP_C - AZM.ABS_MIN_TEMP_C) / AZM.CRANGE + AZM.ABS_MIN_TEMP_C;
            }


            if (!double.IsNaN(e.MSR_dB))
                remote.MSR_dB.Value = e.MSR_dB;

            if (!double.IsNaN(e.RemotesDepth_m))
            {
                remote.Depth_m.Value = e.RemotesDepth_m;
                remote.Z_m.Value = e.RemotesDepth_m;
            }

            bool ignoreRange = false;

            if (!double.IsNaN(e.PropTime_s))
            {
                remote.PTime_s.Value = e.PropTime_s;

                if (IsRecalculateRange)
                {
                    ignoreRange = true;
                    is_srp = true;

                    if (DeviceType == AZM_DEVICE_TYPE_Enum.DT_LBL_TSV)
                    {
                        if (IsPTimeAdjustment)
                        {
                            remote.SRange_m.Value = 
                                (e.PropTime_s - PTimePerAddrAdjustment_s  - PTimePerAddrAdjustment2_s * (int)e.Address) * wpManager.SoundSpeed;
                        }
                        else
                            remote.SRange_m.Value = e.PropTime_s * wpManager.SoundSpeed;
                    }
                    else
                    {
                        remote.SRange_m.Value = e.PropTime_s * wpManager.SoundSpeed;
                    }

                    if (state.StDepth_m.IsInitialized && remote.Depth_m.IsInitialized)
                    {
                        remote.SRangeProjection_m.Value =
                            AZMMath.TryCalculateSlantRangeProjection(state.StDepth_m.Value, remote.Depth_m.Value, remote.SRange_m.Value);                        
                    }
                    else
                    {
                        remote.SRangeProjection_m.Value = remote.SRange_m.Value;
                    }
                }
            }

            if (!ignoreRange && !double.IsNaN(e.SlantRange_m))
                remote.SRange_m.Value = e.SlantRange_m;

            if (!ignoreRange)
            {
                if (!double.IsNaN(e.SlantRangeProjection_m))
                {
                    remote.SRangeProjection_m.Value = e.SlantRangeProjection_m;
                    is_srp = true;
                }
                else
                {
                    if (!double.IsNaN(e.SlantRange_m))
                    {
                        remote.SRangeProjection_m.Value = e.SlantRange_m;
                        is_srp = true;
                    }
                }
            }            
        }


        private void ProcessRemoteUSBL(NDTAReceivedEventArgs e)
        {
            var remote = remotes.GetOrAdd(e.Address, addr => new ResponderBeacon(addr));

            bool is_a = false;

            ProcessCommonItems(e, out bool is_srp);

            if (!double.IsNaN(e.HAngle_deg))
            {
                remote.Azimuth_deg.Value = e.HAngle_deg;
                is_a = true;
            }

            if (!double.IsNaN(e.VAngle_deg))
                remote.Elevation_deg.Value = e.VAngle_deg;

            if (is_a && is_srp)
            {
                if (IsStationLocationAndHeadingValid())
                {
                    AZMMath.PolarCS_ShiftRotate(state.Heading_deg.Value, phi_deg,
                        remote.Azimuth_deg.Value,
                        remote.SRangeProjection_m.Value,
                        x_offset_m, y_offset_m,
                        out double a_azm, out double a_rng);

                    AZMMath.CalculateAbsLocationDirectGeodetic(
                        Algorithms.Deg2Rad(state.Lat_deg.Value),
                        Algorithms.Deg2Rad(state.Lon_deg.Value),
                        Algorithms.Deg2Rad(a_azm), a_rng,
                        out double rlat_rad,
                        out double rlon_rad,
                        out double _);

                    DateTime ts = DateTime.Now;

                    remote.DHFilterState ??= 
                        new DHTrackFilter(usbl_dhfilter_fifo_size, usbl_dhfilter_maxspeed_mps, usbl_dhfilter_threshold_m);

                    if (remote.DHFilterState.Process(rlat_rad, rlon_rad, 0, ts,
                            out rlat_rad, out rlon_rad, out _, out _))
                    {
                        remote.TFilterState ??= 
                            new TrackMovingAverageSmoother(usbl_sfilter_fifo_size, usbl_sfilter_threshold_m);

                        double rdpt_m = remote.Depth_m.IsInitialized ? remote.Depth_m.Value : 0;

                        remote.TFilterState?.Process(
                            rlat_rad, rlon_rad, rdpt_m, DateTime.Now,
                            out rlat_rad, out rlon_rad, out _, out _);

                        remote.AAzimuth_deg.Value = a_azm;
                        remote.RAzimuth_deg.Value = Algorithms.Wrap360(a_azm + 180);
                        remote.ADistance_m.Value = a_rng;

                        double rlat_deg = Algorithms.Rad2Deg(rlat_rad);
                        double rlon_deg = Algorithms.Rad2Deg(rlon_rad);

                        remote.Lat_deg.Value = rlat_deg;
                        remote.Lon_deg.Value = rlon_deg;
                    }
                }
                else
                {
                    remote.RAzimuth_deg.Value = Algorithms.Wrap360(remote.Azimuth_deg.Value + 180);
                }
            }

            OutputHandler?.Rise(this, new StringEventArgs(remote.ToString()));
        }       

        public bool Set3RespondersGeographicCoordinates(double r1lon, double r1lat, double r2lon, double r2lat, double r3lon, double r3lat)
        {
            if (!AZM.IsLonDeg(r1lon) || !AZM.IsLatDeg(r1lat) ||
                !AZM.IsLonDeg(r2lon) || !AZM.IsLatDeg(r2lat) ||
                !AZM.IsLonDeg(r3lon) || !AZM.IsLatDeg(r3lat))
                return false;

            lblProcessor.RefPointLat = r1lat;
            lblProcessor.RefPointLon = r1lon;

            double rplat_rad = Algorithms.Deg2Rad(r1lat);
            double rplon_rad = Algorithms.Deg2Rad(r1lon);

            double r1x = 0; double r1y = 0;

            Algorithms.GetDeltasByGeopoints_WGS84(rplat_rad, rplon_rad, Algorithms.Deg2Rad(r2lat), Algorithms.Deg2Rad(r2lon), out double r2x, out double r2y);
            Algorithms.GetDeltasByGeopoints_WGS84(rplat_rad, rplon_rad, Algorithms.Deg2Rad(r3lat), Algorithms.Deg2Rad(r3lon), out double r3x, out double r3y);

            Set3RespondersLocalCoordinates(r1x, r1y, r2x, r2y, r3x, r3y);

            return true;
        }

        public bool Discard3RespondersCoordinates()
        {
            lblProcessor.RefPointLat = double.NaN;
            lblProcessor.RefPointLon = double.NaN;

            var r1 = remotes.GetOrAdd(REMOTE_ADDR_Enum.REM_ADDR_1, addr => new ResponderBeacon(addr));

            r1.X_m.DeInit();
            r1.Y_m.DeInit();

            var r2 = remotes.GetOrAdd(REMOTE_ADDR_Enum.REM_ADDR_2, addr => new ResponderBeacon(addr));

            r2.X_m.DeInit();
            r2.Y_m.DeInit();

            var r3 = remotes.GetOrAdd(REMOTE_ADDR_Enum.REM_ADDR_3, addr => new ResponderBeacon(addr));

            r3.X_m.DeInit();
            r3.Y_m.DeInit();


            return true;
        }

        public bool Set3RespondersLocalCoordinates(double r1x, double r1y, double r2x, double r2y, double r3x, double r3y)
        {
            if (double.IsNaN(r1x) || double.IsNaN(r1y) || 
                double.IsNaN(r2x) || double.IsNaN(r2y) || 
                double.IsNaN(r3x) || double.IsNaN(r3y))
                return false;

            var r1 = remotes.GetOrAdd(REMOTE_ADDR_Enum.REM_ADDR_1, addr => new ResponderBeacon(addr));

            r1.X_m.Value = r1x;
            r1.Y_m.Value = r1y;

            var r2 = remotes.GetOrAdd(REMOTE_ADDR_Enum.REM_ADDR_2, addr => new ResponderBeacon(addr));

            r2.X_m.Value = r2x;
            r2.Y_m.Value = r2y;

            var r3 = remotes.GetOrAdd(REMOTE_ADDR_Enum.REM_ADDR_3, addr => new ResponderBeacon(addr));

            r3.X_m.Value = r3x;
            r3.Y_m.Value = r3y;

            return true;
        }        

        private void ProcessRemoteLBL(NDTAReceivedEventArgs e)
        {
            var remote = remotes.GetOrAdd(e.Address, addr => new ResponderBeacon(addr));

            ProcessCommonItems(e, out bool is_srp);

            if (is_srp)
            {
                if (remote.X_m.IsInitialized &&
                    remote.Y_m.IsInitialized &&
                    remote.Z_m.IsInitialized)
                {
                    lblProcessor.UpdatePoint(e.Address,
                        remote.X_m.Value,
                        remote.Y_m.Value,
                        remote.Z_m.Value,
                        remote.SRangeProjection_m.Value);

                    if (lblProcessor.CanFormNavigationBase())
                    {
                        var basepoints = lblProcessor.GetValidPointsForSolver();

                        double x_prev = state.X_m.IsInitialized ? state.X_m.Value : double.NaN;
                        double y_prev = state.Y_m.IsInitialized ? state.Y_m.Value : double.NaN;

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

                        Algorithms.TOA_NLM2D_Solve(basepoints.ToArray(), x_prev, y_prev, state.Z_m.Value,
                            Algorithms.NLM_DEF_IT_LIMIT, Algorithms.NLM_DEF_PREC_THRLD, 1.0,
                            out double x_curr, out double y_curr, out double rerr, out int itcnt);

                        if (rerr <= lbl_rerr_threshold_m)
                        {
                            double res_x = x_curr;
                            double res_y = y_curr;
                            double res_z = state.Z_m.Value;

                            bool point_accepted = true;

                            if (lblDHFilter != null)
                            {
                                if (lblDHFilter.Process(
                                    res_x, res_y, res_z, DateTime.Now, 
                                    out res_x, out res_y, out _, out _))
                                {
                                    lblSFilter?.Process(res_x, res_y, res_z, DateTime.Now,
                                        out res_x, out res_y, out _, out _);
                                }
                                else
                                {
                                    point_accepted = false;
                                }
                            }

                            if (point_accepted)
                            {
                                state.X_m.Value = res_x;
                                state.Y_m.Value = res_y;
                                state.Rerr_m.Value = rerr;

                                if (lblProcessor.IsRefPoint)
                                {
                                    Algorithms.GeopointOffsetByDeltas_WGS84(
                                        Algorithms.Deg2Rad(lblProcessor.RefPointLat), Algorithms.Deg2Rad(lblProcessor.RefPointLon),
                                        res_y, res_x, out double lat_rad, out double lon_rad);

                                    state.Lat_deg.Value = Algorithms.Rad2Deg(lat_rad);
                                    state.Lon_deg.Value = Algorithms.Rad2Deg(lon_rad);
                                }
                            }
                        }
                    }
                }
            }
            
            OutputHandler?.Rise(this, new StringEventArgs(remote.ToString()));
        }
        
        private void ProcessStationLocalParameters(NDTAReceivedEventArgs e)
        {
            if (!double.IsNaN(e.LocTemp_C))
            {
                state.WaterTemp_C.Value = e.LocTemp_C;
                wpManager.Temperature = e.LocTemp_C;
            }

            if (!double.IsNaN(e.LocPrs_mBar))
            {
                state.StPressure_mBar.Value = e.LocPrs_mBar;
                wpManager.Pressure = e.LocPrs_mBar;

                if (state.WaterTemp_C.IsInitialized)
                {
                    double waterDensity_kgm3 =
                        PHX.Water_density_calc(state.WaterTemp_C.Value,
                                               e.LocPrs_mBar,
                                               Salinity_PSU);

                    state.StDepth_m.Value = PHX.Depth_by_pressure_calc(e.LocPrs_mBar,
                        PHX.PHX_ATM_PRESSURE_MBAR, waterDensity_kgm3, PHX.PHX_GRAVITY_ACC_MPS2);

                    state.Z_m.Value = state.StDepth_m.Value;
                }
            }

            if (!double.IsNaN(e.LocPitch_deg))
                state.StPitch_deg.Value = e.LocPitch_deg;
            if (!double.IsNaN(e.LocRoll_deg))
                state.StRoll_deg.Value = e.LocRoll_deg;

            OutputHandler?.Rise(this, new StringEventArgs(state.StationParametersToString()));

            if (IsStationNMEAOutputEnabled)
                OutputHandler?.Rise(this, 
                    new StringEventArgs(
                        Utils.BuildRMCGGAMTWNMEAMessages(state.Lat_deg, state.Lon_deg, DateTime.UtcNow, state.StDepth_m, state.WaterTemp_C)));
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
                else if (splits[1] == "(BPS)")
                {
                    if (aux1Port == null)
                        AUX1Init(BaudRate.baudRate9600, true);

                    aux1Port?.EmulateInput(splits[2]);
                }
                else if (splits[1] == "(AZM)")
                {
                    azmPort?.EmulateInput(splits[2]);
                }
            }
        }


        private void LogError(string message, Exception? ex = null)
        {
            LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, ex ?? new Exception(message)));
        }

        private void LogInfo(string message)
        {
            LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.INFO, message));
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            pTimer?.Stop();
            pTimer?.Dispose();

            azmPort?.Dispose();
            aux1Port?.Dispose();
            aux2Port?.Dispose();
            serialOutput?.Dispose();
            udpOutput?.Dispose();

            _disposed = true;
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
