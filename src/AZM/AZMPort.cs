using UCNLDrivers;
using UCNLNMEA;

namespace AzimuthConsole.AZM
{
    #region Miscelaneous EventArgs
    public class ACKReceivedEventArgs : EventArgs
    {
        // $PAZM0,[cmdID],result

        #region Properties
        public ICs SentenceID { get; private set; }
        public IC_RESULT_Enum ResultID { get; private set; }

        #endregion

        #region Constructor

        public ACKReceivedEventArgs(ICs sntID, IC_RESULT_Enum resID)
        {
            SentenceID = sntID;
            ResultID = resID;
        }

        #endregion
    }

    public class STRSTPReceivedEventArgs : EventArgs
    {
        // $PAZM1,[addrMask],[sty_PSU],[soundSpeed_mps],[maxDist_m]

        #region Properties

        public ushort AddrMask { get; private set; }
        public double Sty_PSU { get; private set; }
        public double SoundSpeed_mps { get; private set; }
        public double MaxDist_m { get; private set; }

        #endregion

        #region Constructor

        public STRSTPReceivedEventArgs(ushort addrMask, double sty_PSU, double soundSpeed_mps, double maxDist_m)
        {
            AddrMask = addrMask;
            Sty_PSU = sty_PSU;
            SoundSpeed_mps = soundSpeed_mps;
            MaxDist_m = maxDist_m;
        }

        #endregion
    }

    public class RSTSReceivedEventArgs : EventArgs
    {
        // $PAZM2,[addr],[sty_PSU]
        #region Properties

        public REMOTE_ADDR_Enum Addr { get; private set; }
        public double Sty_PSU { get; private set; }

        public bool IsStyPresent { get => !double.IsNaN(Sty_PSU); }

        #endregion

        #region Constructor

        public RSTSReceivedEventArgs(REMOTE_ADDR_Enum addr, double sty_PSU)
        {
            Addr = addr;
            Sty_PSU = sty_PSU;
        }

        #endregion
    }

    public class NDTAReceivedEventArgs : EventArgs
    {
        // $PAZM3,status,[addr],[rq_code],[rs_code],[msr_dB],[p_time],[s_range],[p_range],[r_dpt],[a],[e],[lprs],[ltmp],[lhdn],[lpts],[lrol]

        #region Properties        

        public NDTA_Status_Enum Status { get; private set; }

        public REMOTE_ADDR_Enum Address { get; private set; }

        public CDS_REQ_CODES_Enum RequestCode { get; private set; }

        public int ResponseCode { get; private set; }

        public double MSR_dB { get; private set; }

        public double PropTime_s { get; private set; }

        public double SlantRange_m { get; private set; }

        public double SlantRangeProjection_m { get; private set; }

        public double RemotesDepth_m { get; private set; }

        public double HAngle_deg { get; private set; }

        public double VAngle_deg { get; private set; }

        public double LocPrs_mBar { get; private set; }

        public double LocTemp_C { get; private set; }

        public double LocHeading_deg { get; private set; }

        public double LocPitch_deg { get; private set; }

        public double LocRoll_deg { get; private set; }

        #endregion

        #region Constructor

        public NDTAReceivedEventArgs(NDTA_Status_Enum status, REMOTE_ADDR_Enum addr, CDS_REQ_CODES_Enum reqCode,
            int resCode, double msr_dB, double p_time, double s_range, double p_range, double r_dpt,
            double a, double e, double lprs, double ltmp, double lhdn, double lpts, double lrol)
        {
            Status = status;
            Address = addr;
            RequestCode = reqCode;
            ResponseCode = resCode;
            MSR_dB = msr_dB;
            PropTime_s = p_time;
            SlantRange_m = s_range;
            SlantRangeProjection_m = p_range;
            RemotesDepth_m = r_dpt;
            HAngle_deg = a;
            VAngle_deg = e;
            LocPrs_mBar = lprs;
            LocTemp_C = ltmp;
            LocHeading_deg = lhdn;
            LocPitch_deg = lpts;
            LocRoll_deg = lrol;
        }

        #endregion
    }

    public class RUCMDReceivedEventArgs : EventArgs
    {
        // $PAZM5,cmdID
        #region Properties

        public CDS_REQ_CODES_Enum CmdID { get; private set; }

        #endregion

        #region Constructor

        public RUCMDReceivedEventArgs(CDS_REQ_CODES_Enum cmdID)
        {
            CmdID = cmdID;
        }

        #endregion
    }

    public class RBCASTReceivedEventArgs : EventArgs
    {
        // $PAZM6,cmdID
        #region Properties

        public CDS_RBCAST_CODES_Enum CmdID { get; private set; }

        #endregion

        #region Constructor

        public RBCASTReceivedEventArgs(CDS_RBCAST_CODES_Enum cmdID)
        {
            CmdID = cmdID;
        }


        #endregion
    }

    public class CSETReceivedEventArgs : EventArgs
    {
        // $PAZM8,dataID,dataVal,reserved
        #region Properties

        public CDS_REQ_CODES_Enum UserDataID { get; private set; }
        public int UserDataValue { get; private set; }

        #endregion

        #region Constructor

        public CSETReceivedEventArgs(CDS_REQ_CODES_Enum udid, int udval)
        {
            UserDataID = udid;
            UserDataValue = udval;
        }


        #endregion
    }

    #endregion

    public class AZMPort : uSerialPort
    {
        #region Properties

        static bool nmeaSingleton = false;

        bool isWaitingLocal = false;
        public bool IsWaitingLocal
        {
            get { return isWaitingLocal; }
            private set
            {
                isWaitingLocal = value;
                IsWaitingLocalChangedEventHandler.Rise(this, new EventArgs());
            }
        }

        ICs? lastQueryID = ICs.IC_INVALID;


        public bool IsDeviceInfoValid { get; private set; }
        public AZM_DEVICE_TYPE_Enum DeviceType { get; private set; }
        public REMOTE_ADDR_Enum RemoteAddress { get; private set; }
        public ushort AddressMask { get; private set; }
        public AZM_PTS_TYPE_Enum PTSType { get; private set; }
        public string SerialNumber { get; private set; }
        public string SystemInfo { get; private set; }
        public string SystemVersion { get; private set; }
        public int ChannelID { get; private set; }

        #endregion

        #region Constructor

        public AZMPort(BaudRate baudRate)
            : base(baudRate)
        {
            base.PortDescription = "AZM";
            base.IsLogIncoming = true;
            base.IsTryAlways = true;

            SerialNumber = string.Empty;
            SystemInfo = string.Empty;
            SystemVersion = string.Empty;


            NMEAInit();
        }

        #endregion

        #region Methods

        #region Private

        private static void NMEAInit()
        {
            if (!nmeaSingleton)
            {
                nmeaSingleton = true;
                NMEAParser.AddManufacturerToProprietarySentencesBase(ManufacturerCodes.AZM);

                //IC_D2H_ACK              '0' // $PAZM0,[cmdID],result
                NMEAParser.AddProprietarySentenceDescription(ManufacturerCodes.AZM, "0", "x,x");

                //IC_D2D_STRSTP           '1' // $PAZM1,[addrMask],[sty_PSU],[soundSpeed_mps],[maxDist_m]
                NMEAParser.AddProprietarySentenceDescription(ManufacturerCodes.AZM, "1", "x,x.x,x.x,x.x");

                //IC_D2D_RSTS             '2' // $PAZM2,[addr],[sty_PSU]
                NMEAParser.AddProprietarySentenceDescription(ManufacturerCodes.AZM, "2", "x,x.x");

                //IC_D2H_NDTA             '3' // $PAZM3,status,[addr],[rq_code],[rs_code],[msr_dB],[p_time],[s_range],[p_range],[r_dpt],[a],[e],[lprs],[ltmp],[lhdn],[lpts],[lrol]
                NMEAParser.AddProprietarySentenceDescription(ManufacturerCodes.AZM, "3", "x,x,x,x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x");

                //IC_H2D_DPTOVR           '4' // $PAZM4,depth_m
                NMEAParser.AddProprietarySentenceDescription(ManufacturerCodes.AZM, "4", "x.x");

                //IC_D2H_RUCMD            '5' // $PAZM5,cmdID
                NMEAParser.AddProprietarySentenceDescription(ManufacturerCodes.AZM, "5", "x");

                //IC_D2H_RBCAST           '6' // $PAZM6,cmdID
                NMEAParser.AddProprietarySentenceDescription(ManufacturerCodes.AZM, "6", "x");

                //IC_H2D_DINFO_GET        '?' // $PAZM?,[reserved]
                NMEAParser.AddProprietarySentenceDescription(ManufacturerCodes.AZM, "?", "x");

                //IC_D2H_DINFO            '!' // $PAZM!,d_type,address,serialNumber,sys_info,sys_version,pts_type,dl_ch_id,ul_ch_id
                NMEAParser.AddProprietarySentenceDescription(ManufacturerCodes.AZM, "!", "x,x,c--c,c--c,x,x,x,x");


                //IC_H2D_CREQ             '7' // $PAZM7,[addr],user_data_id
                NMEAParser.AddProprietarySentenceDescription(ManufacturerCodes.AZM, "7", "x,x");

                //IC_H2D_CSET             '8' // $PAZM8,user_data_id,user_data_value,[reserved]
                NMEAParser.AddProprietarySentenceDescription(ManufacturerCodes.AZM, "8", "x,x,x");
            }
        }

        private bool TrySend(string message, ICs queryID)
        {
            bool result = detected && !IsWaitingLocal;

            if (result)
            {
                try
                {
                    Send(message);

                    if (queryID == ICs.IC_D2D_RSTS)
                        StartTimer(3000);
                    else
                        StartTimer(1000);

                    IsWaitingLocal = true;

                    lastQueryID = queryID;
                    result = true;
                }
                catch (Exception ex)
                {
                    LogEventHandler.Rise(this, new LogEventArgs(LogLineType.ERROR, ex));
                }
            }

            return result;
        }

        #region Parsers

        private void Parse_ACK(object[] parameters)
        {
            // $PAZM0,[cmdID],result
            try
            {
                ICs sntID = AZM.ICsByMessageID(parameters[0].ToString());
                IC_RESULT_Enum resID = AZM.O2_IC_RESULT_Enum(parameters[1]);

                StopTimer();
                IsWaitingLocal = false;

                ACKReceived.Rise(this, new ACKReceivedEventArgs(sntID, resID));
            }
            catch (Exception ex)
            {
                LogEventHandler.Rise(this, new LogEventArgs(LogLineType.ERROR, ex));
            }
        }

        private void Parse_RSTS(object[] parameters)
        {
            // $PAZM2,[addr],[sty_PSU]
            try
            {
                StopTimer();
                IsWaitingLocal = false;

                REMOTE_ADDR_Enum addr = AZM.O2_REMOTE_ADDR_Enum(parameters[0]);
                double sty_PSU = AZM.O2D(parameters[1]);

                RSTSReceived.Rise(this, new RSTSReceivedEventArgs(addr, sty_PSU));
            }
            catch (Exception ex)
            {
                LogEventHandler.Rise(this, new LogEventArgs(LogLineType.ERROR, ex));
            }
        }

        private void Parse_STRSTP(object[] parameters)
        {
            // $PAZM1,[addrMask],[sty_PSU],[soundSpeed_mps],[maxDist_m]
            try
            {
                StopTimer();
                IsWaitingLocal = false;

                ushort addrMask = AZM.O2U16(parameters[0]);
                double sty_PSU = AZM.O2D(parameters[1]);
                double soundSpeed_mps = AZM.O2D(parameters[2]);
                double maxDist_m = AZM.O2D(parameters[3]);

                STRSTPReceived.Rise(this, new STRSTPReceivedEventArgs(addrMask, sty_PSU, soundSpeed_mps, maxDist_m));
            }
            catch (Exception ex)
            {
                LogEventHandler.Rise(this, new LogEventArgs(LogLineType.ERROR, ex));
            }
        }

        private void Parse_NDTA(object[] parameters)
        {
            // status,[addr],[rq_code],[rs_code],[msr_dB],[p_time],[s_range],[p_range],[r_dpt],[a],[e],[lprs],[ltmp],[lhdn],[lptc],[lrol]

            StopTimer();
            if (IsActive)
                StartTimer(3000);

            try
            {
                NDTA_Status_Enum status = AZM.O2_NDTA_Status_Enum(parameters[0]);
                REMOTE_ADDR_Enum addr = AZM.O2_REMOTE_ADDR_Enum(parameters[1]);
                CDS_REQ_CODES_Enum req_code = AZM.O2_CDS_REQ_CODES_Enum(parameters[2]);
                int res_code = AZM.O2S32(parameters[3]);
                double msr_dB = AZM.O2D(parameters[4]);
                double p_time_s = AZM.O2D(parameters[5]);
                double s_range_m = AZM.O2D(parameters[6]);
                double p_range_m = AZM.O2D(parameters[7]);
                double r_dpt_m = AZM.O2D(parameters[8]);
                double a_deg = AZM.O2D(parameters[9]);
                double e_deg = AZM.O2D(parameters[10]);
                double lprs_mBar = AZM.O2D(parameters[11]);
                double ltmp_C = AZM.O2D(parameters[12]);
                double lhdn_deg = AZM.O2D(parameters[13]);
                double lptc_deg = AZM.O2D(parameters[14]);
                double lrol_deg = AZM.O2D(parameters[15]);

                NDTAReceived.Rise(this,
                    new NDTAReceivedEventArgs(status, addr, req_code, res_code,
                    msr_dB, p_time_s,
                    s_range_m, p_range_m, r_dpt_m,
                    a_deg, e_deg,
                    lprs_mBar, ltmp_C, lhdn_deg, lptc_deg, lrol_deg));
            }
            catch (Exception ex)
            {
                LogEventHandler.Rise(this, new LogEventArgs(LogLineType.ERROR, ex));
            }
        }

        private void Parse_RUCMD(object[] parameters)
        {
            // $PAZM5,cmdID
            try
            {
                CDS_REQ_CODES_Enum cmdID = AZM.O2_CDS_REQ_CODES_Enum(parameters[0]);
                RUCMDReceived.Rise(this, new RUCMDReceivedEventArgs(cmdID));
            }
            catch (Exception ex)
            {
                LogEventHandler.Rise(this, new LogEventArgs(LogLineType.ERROR, ex));
            }
        }

        private void Parse_RBCAST(object[] parameters)
        {
            // $PAZM6,cmdID
            try
            {
                CDS_RBCAST_CODES_Enum cmdID = AZM.O2_CDS_RBCAST_CODES_Enum(parameters[0]);
                RBCASTReceived.Rise(this, new RBCASTReceivedEventArgs(cmdID));
            }
            catch (Exception ex)
            {
                LogEventHandler.Rise(this, new LogEventArgs(LogLineType.ERROR, ex));
            }
        }

        private void Parse_DINFO(object[] parameters)
        {
            // $PAZM!,d_type,address,serialNumber,sys_info,sys_version,pts_type,dl_ch_id,ul_ch_id
            try
            {
                DeviceType = AZM.O2_AZM_DEVICE_TYPE_Enum(parameters[0]);

                AddressMask = 0;
                RemoteAddress = REMOTE_ADDR_Enum.REM_ADDR_INVALID;

                if (DeviceType == AZM_DEVICE_TYPE_Enum.DT_USBL_TSV)
                    AddressMask = AZM.O2U16(parameters[1]);
                else if (DeviceType == AZM_DEVICE_TYPE_Enum.DT_REMOTE)
                    RemoteAddress = AZM.O2_REMOTE_ADDR_Enum(parameters[1]);

                SerialNumber = AZM.O2S(parameters[2]);
                SystemInfo = AZM.O2S(parameters[3]);
                SystemVersion = AZM.BCDVersionToStr(AZM.O2S32(parameters[4]));
                PTSType = AZM.O2_AZM_PTS_TYPE_Enum(parameters[5]);

                ChannelID = AZM.O2S32(parameters[6]);

                IsDeviceInfoValid = (DeviceType != AZM_DEVICE_TYPE_Enum.DT_INVALID) &&
                                    (!string.IsNullOrEmpty(SerialNumber));

                DeviceInfoValidChanged.Rise(this, new EventArgs());
            }
            catch (Exception ex)
            {
                LogEventHandler.Rise(this, new LogEventArgs(LogLineType.ERROR, ex));
            }
        }


        private void Parse_CSET(object[] parameters)
        {
            // $PAZM8,dataID,dataValue,reserved

            StopTimer();
            IsWaitingLocal = false;

            try
            {
                CDS_REQ_CODES_Enum rcode = AZM.O2_CDS_REQ_CODES_Enum(parameters[0]);
                int value = AZM.O2S32(parameters[1]);

                CSETReceived.Rise(this, new CSETReceivedEventArgs(rcode, value));
            }
            catch (Exception ex)
            {
                LogEventHandler.Rise(this, new LogEventArgs(LogLineType.ERROR, ex));
            }
        }

        #endregion

        #endregion

        #region Public

        public bool Query_DINFO()
        {
            StopTimer();
            // $PAZM?,[reserved]
            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "?", [0]);
            return TrySend(msg, ICs.IC_H2D_DINFO_GET);
        }


        public bool Query_BaseStop()
        {
            return Query_STRSTP(0, double.NaN, double.NaN, double.NaN);
        }

        public bool Query_BaseStart(ushort addrMask, double sty_PSU, double maxDist_m)
        {
            return Query_STRSTP(addrMask, sty_PSU, double.NaN, maxDist_m);
        }

        public bool Query_BaseStart(ushort addrMask, double maxDist_m)
        {
            return Query_STRSTP(addrMask, double.NaN, double.NaN, maxDist_m);
        }

        public bool Query_STRSTP(ushort addrMask, double sty_PSU, double soundSpeed_mps, double maxDist_m)
        {
            // $PAZM1,[addrMask],[sty_PSU],[soundSpeed_mps],[maxDist_m]

            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "1",
                [
                    addrMask > 0 ? (int)addrMask : null,
                    !double.IsNaN(sty_PSU) ? sty_PSU : null,
                    !double.IsNaN(soundSpeed_mps) ? soundSpeed_mps : null,
                    !double.IsNaN(maxDist_m) ? maxDist_m : null ]);

            return TrySend(msg, ICs.IC_D2D_STRSTP);
        }

        public bool Query_RemoteStySet(double sty_PSU)
        {
            return Query_RSTS(0, sty_PSU);
        }

        public bool Query_RemoteAddrSet(REMOTE_ADDR_Enum addr)
        {
            return Query_RSTS(addr, double.NaN);
        }

        public bool Query_RSTS(REMOTE_ADDR_Enum addr, double sty_PSU)
        {
            // $PAZM2,[addr],[sty_PSU]

            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "2",
                [
                    addr == REMOTE_ADDR_Enum.REM_ADDR_INVALID ? null : (int)addr,
                    !double.IsNaN(sty_PSU) ? sty_PSU : null ]);

            return TrySend(msg, ICs.IC_D2D_RSTS);
        }


        public bool Query_CREQ(REMOTE_ADDR_Enum addr, CDS_REQ_CODES_Enum rcode)
        {
            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "7",
                [
                    addr == REMOTE_ADDR_Enum.REM_ADDR_INVALID ? null : (int)addr,
                    (int)rcode
                ]);

            return TrySend(msg, ICs.IC_H2D_CREQ);
        }

        public bool Query_CSET_Write(CDS_REQ_CODES_Enum did, int value)
        {
            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "8",
                [
                    (int)did,
                    value,
                    null
                ]);

            return TrySend(msg, ICs.IC_D2D_CSET);
        }

        public bool Query_CSET_Read(CDS_REQ_CODES_Enum did)
        {
            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "8",
               [
                    (int)did,
                    null,
                    null,
               ]);

            return TrySend(msg, ICs.IC_D2D_CSET);
        }

        #endregion

        #endregion

        #region uSerialPort

        public override void InitQuerySend()
        {
            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "?", [0]);
            Send(msg);
        }

        public override void OnClosed()
        {
            StopTimer();
            IsDeviceInfoValid = false;
            isWaitingLocal = false;
        }

        public override void ProcessIncoming(NMEASentence sentence)
        {
            if (sentence is NMEAProprietarySentence)
            {
                NMEAProprietarySentence? pSentence = (sentence as NMEAProprietarySentence);

                if (pSentence?.Manufacturer == ManufacturerCodes.AZM)
                {
                    if ((!detected) && ("0123568!".Contains(pSentence.SentenceIDString)))
                    {
                        detected = true;
                        StopTimer();
                    }

                    if (pSentence.SentenceIDString == "0")
                        Parse_ACK(pSentence.parameters);
                    else if (pSentence.SentenceIDString == "1")
                        Parse_STRSTP(pSentence.parameters);
                    else if (pSentence.SentenceIDString == "2")
                        Parse_RSTS(pSentence.parameters);
                    else if (pSentence.SentenceIDString == "3")
                        Parse_NDTA(pSentence.parameters);
                    else if (pSentence.SentenceIDString == "5")
                        Parse_RUCMD(pSentence.parameters);
                    else if (pSentence.SentenceIDString == "6")
                        Parse_RBCAST(pSentence.parameters);
                    else if (pSentence.SentenceIDString == "8")
                        Parse_CSET(pSentence.parameters);
                    else if (pSentence.SentenceIDString == "!")
                        Parse_DINFO(pSentence.parameters);
                }
            }
        }

        #endregion

        #region Events

        public EventHandler IsWaitingLocalChangedEventHandler;
        public EventHandler? IsWaitingRemoteChangedEventHandler;

        public EventHandler<ACKReceivedEventArgs> ACKReceived;
        public EventHandler<RSTSReceivedEventArgs> RSTSReceived;
        public EventHandler<STRSTPReceivedEventArgs> STRSTPReceived;
        public EventHandler<NDTAReceivedEventArgs> NDTAReceived;
        public EventHandler<RUCMDReceivedEventArgs> RUCMDReceived;
        public EventHandler<RBCASTReceivedEventArgs> RBCASTReceived;
        public EventHandler<CSETReceivedEventArgs> CSETReceived;

        public EventHandler DeviceInfoValidChanged;

        #endregion
    }
}