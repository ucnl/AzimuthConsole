using System.Globalization;
using System.Net;
using System.Text;
using UCNLDrivers;
using UCNLNav;
using UCNLNav.TrackFilters;
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
        public bool ConnectionActive { get => azmPort.IsActive; }

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

        public bool AZMDetected { get { return (azmPort != null) && (azmPort.Detected); } }
        public string AZMPreferredPortName
        {
            get => azmPort.ProposedPortName;
            set => azmPort.ProposedPortName = value;
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

        List<IAging> stationParams;

        readonly AZMPort azmPort;
        uGNSSSerialPort aux1Port;
        uMagneticCompassPort aux2Port;
        NMEASerialPort serialOutput;
        UDPTranslator udpOutput;

        Dictionary<REMOTE_ADDR_Enum, ResponderBeacon> remotes;

        bool polling_started_received = false;
        DateTime prevRemAckTS = DateTime.Now;

        System.Timers.Timer pTimer;

        public AZMCombiner(ushort addrMask, double salinity_PSU, double maxDist_m, double hdn_adj_deg, double gnss_x_offset_m, double gnss_y_offset_m)
        {
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

            remotes = new Dictionary<REMOTE_ADDR_Enum, ResponderBeacon>();

            stPressure_mBar = new AgingValue<double>(3, 10, AZM.mBar_fmtr);
            stPressure_mBar.IgnoreAge = true;
            stPressure_mBar.Name = nameof(stPressure_mBar);

            stDepth_m = new AgingValue<double>(3, 10, AZM.meters1dec_fmtr);
            stDepth_m.IgnoreAge = true;
            stDepth_m.Name = nameof(stDepth_m);

            waterTemp_C = new AgingValue<double>(3, 10, AZM.degC_fmtr);
            waterTemp_C.IgnoreAge = true;
            waterTemp_C.Name = nameof(waterTemp_C);

            stPitch_deg = new AgingValue<double>(3, 10, AZM.degrees1dec_fmtr);
            stPitch_deg.IgnoreAge = true;
            stPitch_deg.Name = nameof(stPitch_deg);

            stRoll_deg = new AgingValue<double>(3, 10, AZM.degrees1dec_fmtr);
            stRoll_deg.Name = nameof(stRoll_deg);

            lat_deg = new AgingValue<double>(3, 10, AZM.latlon_fmtr);
            lat_deg.IgnoreAge = true;
            lat_deg.Name = nameof(lat_deg);
            lon_deg = new AgingValue<double>(3, 10, AZM.latlon_fmtr);
            lon_deg.IgnoreAge = true;
            lon_deg.Name = nameof(lon_deg);
            course_deg = new AgingValue<double>(3, 10, AZM.degrees1dec_fmtr);
            course_deg.IgnoreAge = true;
            course_deg.Name = nameof(course_deg);
            speed_mps = new AgingValue<double>(3, 10, x => string.Format(CultureInfo.InvariantCulture, "{0:F01}", x / 3.6));
            speed_mps.Name = nameof(speed_mps);

            heading_deg = new AgingValue<double>(3, 10, AZM.degrees1dec_fmtr);
            heading_deg.Name = nameof(heading_deg);

            stationParams = new List<IAging>() { stPressure_mBar, stDepth_m, waterTemp_C, stPitch_deg, stRoll_deg, lat_deg, lon_deg, course_deg, speed_mps, heading_deg };



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
                    if (IsUseAUX1 && !aux1Port.IsActive)
                    {
                        aux1Port.ProposedPortName = aux1PreferredPortName;
                        aux1Port.Start();
                    }
                }

            };
            azmPort.DeviceInfoValidChanged += (o, e) =>
            {
                if (azmPort.IsDeviceInfoValid && (azmPort.DeviceType == AZM_DEVICE_TYPE_Enum.DT_BASE))
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
                        string.Format("Querying to start polling (AddrMask={0}, Salinity={1:F01} PSU, MaxDist={2:F01} m)", addrMask, salinity_PSU, maxDist_m)));
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
                    ProcessRemote(e);
                    prevRemAckTS = DateTime.Now;
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
                            "Polling started (AddrMask={0}, Salinity={1:F01} PSU, SoundSpeed={2}, MaxDist={3:F01} m)",
                            e.AddrMask,
                            e.Sty_PSU,
                            double.IsNaN(e.SoundSpeed_mps) ? "Auto" : string.Format(CultureInfo.InvariantCulture, "{0:F01} m/s", e.SoundSpeed_mps),
                            e.MaxDist_m)));

                    polling_started_received = true;
                    prevRemAckTS = DateTime.Now;
                }
            };

            pTimer = new System.Timers.Timer();
            pTimer.Interval = 1000;
            pTimer.AutoReset = true;

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

            if (IsUseAUX1 && aux1Port.IsActive)
                aux1Port.Stop();

            if (IsUseAUX2 && aux2Port.IsActive)
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

            if (!azmPort.IsActive)
            {
                azmPort.Start();
                polling_started_received = false;
            }

            return result;
        }

        public bool Disconnect()
        {
            bool result = true;

            if (azmPort.IsActive)
            {
                azmPort.Query_BaseStop();
                azmPort.Stop();

                if (IsUseAUX1 && aux1Port.IsActive)
                    aux1Port.Stop();

                if (IsUseAUX2 && aux2Port.IsActive)
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
                    IsTryAlways = true
                };

                aux1Port.MagneticOnly = false;
                aux1Port.DetectedChanged += (o, e) =>
                {
                    if (aux1Port.Detected)
                    {
                        if (IsUseAUX2 && !aux2Port.IsActive)
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

                aux1Port.LogEventHandler += (o, e) => LogEventHandler.Rise(o, e);
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
                aux2Port.LogEventHandler += (o, e) => LogEventHandler.Rise(o, e);
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

        public void ToUDPOutput(string line)
        {
            try
            {
                udpOutput.Send(line);
                LogEventHandler.Rise(this,
                    new LogEventArgs(LogLineType.INFO,
                    string.Format("{0}:{1} ({2}) << {3}", udpOutput.Address, udpOutput.Port, "UDP_OUT", line)));
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
                serialOutput.SendData(line);
                LogEventHandler.Rise(this,
                    new LogEventArgs(LogLineType.INFO,
                    string.Format("{0} ({1}) << {2}", serialOutput.PortName, "SERIAL_OUT", line)));
            }
            catch (Exception ex)
            {
                LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, ex));
            }
        }


        public bool PauseInterrogation()
        {
            bool result = false;

            try
            {
                result = azmPort.Query_BaseStart(0, Salinity_PSU, MaxDist_m);
            }
            catch (Exception e)
            {
                LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, e));
            }

            return result;
        }

        public bool ResumeInterrogation()
        {
            bool result = false;

            try
            {
                prevRemAckTS = DateTime.Now;
                result = azmPort.Query_BaseStart(AddressMask, Salinity_PSU, MaxDist_m);
            }
            catch (Exception e)
            {
                LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, e));
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
            return azmPort.Query_CREQ(remoteAddr, dataID);
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

        private void CalcAbsLocation(double olat_rad, double olon_rad,
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

        private void ProcessRemote(NDTAReceivedEventArgs e)
        {
            if (!remotes.ContainsKey(e.Address))
                remotes.Add(e.Address, new ResponderBeacon(e.Address));

            bool is_a = false;
            bool is_srp = false;

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

            if (!double.IsNaN(e.PropTime_s))
                remotes[e.Address].PTime_s.Value = e.PropTime_s;

            if (!double.IsNaN(e.SlantRange_m))
                remotes[e.Address].SRange_m.Value = e.SlantRange_m;

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

            if (!double.IsNaN(e.RemotesDepth_m))
                remotes[e.Address].Depth_m.Value = e.RemotesDepth_m;

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
                        out double razm_rad);

                    DateTime ts = DateTime.Now;

                    if (remotes[e.Address].DHFilterState == null)
                        remotes[e.Address].DHFilterState = new DHTrackFilter(8, 1, 5);

                    if (remotes[e.Address].DHFilterState.Process(rlat_rad, rlon_rad, 0, ts,
                            out rlat_rad, out rlon_rad, out _, out ts))
                    {

                        if (remotes[e.Address].TFilterState == null)
                            remotes[e.Address].TFilterState = new TrackMovingAverageSmoother(4, 20);


                        double rdpt_m = remotes[e.Address].Depth_m.IsInitialized ? remotes[e.Address].Depth_m.Value : 0;

                        remotes[e.Address].TFilterState.Process(
                            rlat_rad, rlon_rad, rdpt_m, DateTime.Now,
                            out rlat_rad, out rlon_rad, out rdpt_m, out _);


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
            StringBuilder sb = new StringBuilder();

            sb.Append("@AZMLOC,");

            foreach (IAging avalue in stationParams)
            {
                Utils.AppendAgingValueDesciption(sb, avalue);
            }

            return sb.ToString();
        }

        private string StationParametersToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("@AZMLOC,");

            foreach (IAging avalue in stationParams)
            {
                Utils.AppendAgingValue(sb, avalue);
            }

            return sb.ToString();
        }

        private void ProcessStationLocalParameters(NDTAReceivedEventArgs e)
        {
            if (!double.IsNaN(e.LocTemp_C))
                waterTemp_C.Value = e.LocTemp_C;

            if (!double.IsNaN(e.LocPrs_mBar))
            {
                stPressure_mBar.Value = e.LocPrs_mBar;

                if (waterTemp_C.IsInitialized)
                {
                    double waterDensity_kgm3 =
                        PHX.Water_density_calc(waterTemp_C.Value,
                                               e.LocPrs_mBar,
                                               Salinity_PSU);

                    stDepth_m.Value = PHX.Depth_by_pressure_calc(e.LocPrs_mBar,
                        PHX.PHX_ATM_PRESSURE_MBAR, waterDensity_kgm3, PHX.PHX_GRAVITY_ACC_MPS2);
                }
            }

            if (!double.IsNaN(e.LocPitch_deg))
                stPitch_deg.Value = e.LocPitch_deg;
            if (!double.IsNaN(e.LocRoll_deg))
                stRoll_deg.Value = e.LocRoll_deg;

            OutputHandler?.Rise(this, new StringEventArgs(StationParametersToString()));
        }

        public EventHandler<LogEventArgs> LogEventHandler;
        public EventHandler InterrogationActiveChangedHandler;
        public EventHandler<StringEventArgs> OutputHandler;
        public EventHandler<CREQResultEventArgs> CREQResultHandler;
        public EventHandler DetectedChangedHandler;
        public EventHandler ActiveChangedHandler;
    }
}
