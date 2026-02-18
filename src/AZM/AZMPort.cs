using UCNLDrivers;
using UCNLNMEA;

namespace AzimuthConsole.AZM
{
    public class ACKReceivedEventArgs : EventArgs
    {
        // $PAZM0,[cmdID],result

        public ICs SentenceID { get; private set; }
        public IC_RESULT_Enum ResultID { get; private set; }

        public ACKReceivedEventArgs(ICs sntID, IC_RESULT_Enum resID)
        {
            SentenceID = sntID;
            ResultID = resID;
        }
    }

    public class STRSTPReceivedEventArgs : EventArgs
    {
        // $PAZM1,[addrMask],[sty_PSU],[soundSpeed_mps],[maxDist_m]
        public ushort AddrMask { get; private set; }
        public double Sty_PSU { get; private set; }
        public double SoundSpeed_mps { get; private set; }
        public double MaxDist_m { get; private set; }

        public STRSTPReceivedEventArgs(ushort addrMask, double sty_PSU, double soundSpeed_mps, double maxDist_m)
        {
            AddrMask = addrMask;
            Sty_PSU = sty_PSU;
            SoundSpeed_mps = soundSpeed_mps;
            MaxDist_m = maxDist_m;
        }
    }

    public class RSTSReceivedEventArgs : EventArgs
    {
        // $PAZM2,[addr],[sty_PSU]
        public REMOTE_ADDR_Enum Addr { get; private set; }
        public double Sty_PSU { get; private set; }

        public bool IsStyPresent { get => !double.IsNaN(Sty_PSU); }

        public RSTSReceivedEventArgs(REMOTE_ADDR_Enum addr, double sty_PSU)
        {
            Addr = addr;
            Sty_PSU = sty_PSU;
        }
    }

    public class NDTAReceivedEventArgs : EventArgs
    {
        // $PAZM3,status,[addr],[rq_code],[rs_code],[msr_dB],[p_time],[s_range],[p_range],[r_dpt],[a],[e],[lprs],[ltmp],[lhdn],[lpts],[lrol]

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
    }

    public class RUCMDReceivedEventArgs : EventArgs
    {
        // $PAZM5,cmdID
        public CDS_REQ_CODES_Enum CmdID { get; private set; }

        public RUCMDReceivedEventArgs(CDS_REQ_CODES_Enum cmdID)
        {
            CmdID = cmdID;
        }
    }

    public class RBCASTReceivedEventArgs : EventArgs
    {
        // $PAZM6,cmdID
        public CDS_RBCAST_CODES_Enum CmdID { get; private set; }

        public RBCASTReceivedEventArgs(CDS_RBCAST_CODES_Enum cmdID)
        {
            CmdID = cmdID;
        }
    }

    public class CSETReceivedEventArgs : EventArgs
    {
        // $PAZM8,dataID,dataVal,reserved
        public CDS_REQ_CODES_Enum UserDataID { get; private set; }
        public int UserDataValue { get; private set; }

        public CSETReceivedEventArgs(CDS_REQ_CODES_Enum udid, int udval)
        {
            UserDataID = udid;
            UserDataValue = udval;
        }
    }

    public class AZMPort : uSerialPort
    {
        static bool nmeaSingleton = false;

        bool isWaitingLocal = false;
        public bool IsWaitingLocal
        {
            get => isWaitingLocal; 
            private set
            {
                isWaitingLocal = value;
                IsWaitingLocalChangedEventHandler?.Invoke(this, new EventArgs());
            }
        }

        ICs? lastQueryID = ICs.IC_INVALID;


        public bool IsDeviceInfoValid { get; private set; } = false;
        public AZM_DEVICE_TYPE_Enum DeviceType { get; private set; }
        public REMOTE_ADDR_Enum RemoteAddress { get; private set; }
        public ushort AddressMask { get; private set; }
        public AZM_PTS_TYPE_Enum PTSType { get; private set; }
        public string SerialNumber { get; private set; } = string.Empty;
        public string SystemInfo { get; private set; } = string.Empty;
        public string SystemVersion { get; private set; } = string.Empty;
        public int ChannelID { get; private set; }

        private const int DefaultTimeoutMs = 1000;
        private const int RSTSTimeoutMs = 3000;

        public AZMPort(BaudRate baudRate)
            : base(baudRate)
        {
            base.PortDescription = "AZM";
            base.IsLogIncoming = true;
            base.IsTryAlways = true;

            NMEAInit();
        }

        private static void NMEAInit()
        {
            if (nmeaSingleton)
                return;

            nmeaSingleton = true;

            NMEAParser.AddManufacturerToProprietarySentencesBase(ManufacturerCodes.AZM);

            var snts = new[]
            {
                ("0", "x,x"),                   // IC_D2H_ACK       '0'  $PAZM0,[cmdID],result
                ("1", "x,x.x,x.x,x.x"),         // IC_D2D_STRSTP    '1'  $PAZM1,[addrMask],[sty_PSU],[soundSpeed_mps],[maxDist_m]
                ("2", "x,x.x"),                 // IC_D2D_RSTS      '2'  $PAZM2,[addr],[sty_PSU]
                ("3", "x,x,x,x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x,x.x"), //IC_D2H_NDTA  '3' $PAZM3,status,[addr],[rq_code],[rs_code],[msr_dB],[p_time],[s_range],[p_range],[r_dpt],[a],[e],[lprs],[ltmp],[lhdn],[lpts],[lrol]
                ("4", "x.x"),                   // IC_H2D_DPTOVR    '4'  $PAZM4,depth_m
                ("5", "x"),                     // IC_D2H_RUCMD     '5'  $PAZM5,cmdID
                ("6", "x"),                     // IC_D2H_RBCAST    '6'  $PAZM6,cmdID
                ("7", "x,x"),                   // IC_H2D_CREQ      '7'  $PAZM7,[addr],user_data_id
                ("8", "x,x,x"),                 // IC_H2D_CSET      '8'  $PAZM8,user_data_id,user_data_value,[reserved]
                ("?", "x"),                     // IC_H2D_DINFO_GET '?'  $PAZM?,[reserved]
                ("!", "x,x,c--c,c--c,x,x,x,x"), // IC_D2H_DINFO     '!' $PAZM!,d_type,address,serialNumber,sys_info,sys_version,pts_type,dl_ch_id,ul_ch_id
            };

            foreach (var (id, format) in snts)
            {
                NMEAParser.AddProprietarySentenceDescription(ManufacturerCodes.AZM, id, format);
            }
        }

        private bool TrySend(string message, ICs queryID)
        {
            if (!detected || IsWaitingLocal)
                return false;

            try
            {
                Send(message);

                var timeout = queryID == ICs.IC_D2D_RSTS ? RSTSTimeoutMs : DefaultTimeoutMs;
                StartTimer(timeout);

                IsWaitingLocal = true;
                lastQueryID = queryID;
                    
                return true;
            }
            catch (Exception ex)
            {
                LogEventHandler?.Invoke(this, new LogEventArgs(LogLineType.ERROR, ex));
                return false;
            }
        }

        private void SafeParse(Action parseAction, string sentenceType)
        {
            try
            {
                parseAction();
            }
            catch (Exception ex)
            {
                LogError($"Error parsing {sentenceType}", ex);
            }
        }

        private void Parse_ACK(object[] parameters) => SafeParse(() =>
        {
            // $PAZM0,[cmdID],result
            
            ICs sntID = AZM.ICsByMessageID(parameters[0].ToString());
            IC_RESULT_Enum resID = AZM.O2_IC_RESULT_Enum(parameters[1]);

            StopTimer();
            IsWaitingLocal = false;

            ACKReceived?.Invoke(this, new ACKReceivedEventArgs(sntID, resID));
        }, "AZM0");

        private void Parse_RSTS(object[] parameters) => SafeParse(() =>
        {
            // $PAZM2,[addr],[sty_PSU]

            StopTimer();
            IsWaitingLocal = false;

            REMOTE_ADDR_Enum addr = AZM.O2_REMOTE_ADDR_Enum(parameters[0]);
            double sty_PSU = AZM.O2D(parameters[1]);

            RSTSReceived?.Invoke(this, new RSTSReceivedEventArgs(addr, sty_PSU));
        }, "AZM2");

        private void Parse_STRSTP(object[] parameters) => SafeParse(() =>
        {
            // $PAZM1,[addrMask],[sty_PSU],[soundSpeed_mps],[maxDist_m]
            StopTimer();
            IsWaitingLocal = false;

            ushort addrMask = AZM.O2U16(parameters[0]);
            double sty_PSU = AZM.O2D(parameters[1]);
            double soundSpeed_mps = AZM.O2D(parameters[2]);
            double maxDist_m = AZM.O2D(parameters[3]);

            STRSTPReceived?.Invoke(this, new STRSTPReceivedEventArgs(addrMask, sty_PSU, soundSpeed_mps, maxDist_m));
        }, "AZM1");

        private void Parse_NDTA(object[] parameters) => SafeParse(() =>
        {
            // status,[addr],[rq_code],[rs_code],[msr_dB],[p_time],[s_range],[p_range],[r_dpt],[a],[e],[lprs],[ltmp],[lhdn],[lptc],[lrol]

            StopTimer();
            if (IsActive)
                StartTimer(3000);

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

                NDTAReceived?.Invoke(this,
                    new NDTAReceivedEventArgs(status, addr, req_code, res_code,
                    msr_dB, p_time_s,
                    s_range_m, p_range_m, r_dpt_m,
                    a_deg, e_deg,
                    lprs_mBar, ltmp_C, lhdn_deg, lptc_deg, lrol_deg));
        }, "AZM3");

        private void Parse_RUCMD(object[] parameters) => SafeParse(() =>
        {
            // $PAZM5,cmdID
            CDS_REQ_CODES_Enum cmdID = AZM.O2_CDS_REQ_CODES_Enum(parameters[0]);
            RUCMDReceived?.Invoke(this, new RUCMDReceivedEventArgs(cmdID));
        }, "AZM5");

        private void Parse_RBCAST(object[] parameters) => SafeParse(() =>
        {
            // $PAZM6,cmdID
            CDS_RBCAST_CODES_Enum cmdID = AZM.O2_CDS_RBCAST_CODES_Enum(parameters[0]);
            RBCASTReceived?.Invoke(this, new RBCASTReceivedEventArgs(cmdID));
        }, "AZM6");

        private void Parse_DINFO(object[] parameters) => SafeParse(() =>
        {
            // $PAZM!,d_type,address,serialNumber,sys_info,sys_version,pts_type,dl_ch_id,ul_ch_id
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

            DeviceInfoValidChanged?.Invoke(this, new EventArgs());
        }, "AZM!");

        private void Parse_CSET(object[] parameters) => SafeParse(() =>
        {
            // $PAZM8,dataID,dataValue,reserved

            StopTimer();
            IsWaitingLocal = false;

            CDS_REQ_CODES_Enum rcode = AZM.O2_CDS_REQ_CODES_Enum(parameters[0]);
            int value = AZM.O2S32(parameters[1]);

            CSETReceived?.Invoke(this, new CSETReceivedEventArgs(rcode, value));
        }, "AZM8");


        private void LogError(string context, Exception ex)
        {
            LogEventHandler?.Invoke(this,
                new LogEventArgs(LogLineType.ERROR,
                    $"AZMPort ({PortName}): {context} - {ex.Message}"));
        }


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


        public bool Query_STRSTP(ushort addrMask = 0, double sty_PSU = double.NaN,
            double soundSpeed_mps = double.NaN, double maxDist_m = double.NaN)
        {
            var msg = NMEAParser.BuildProprietarySentence(ManufacturerCodes.AZM, "1",
                new object?[]
                {
                    addrMask > 0 ? (int?)addrMask : null,
                    !double.IsNaN(sty_PSU) ? (double?)sty_PSU : null,
                    !double.IsNaN(soundSpeed_mps) ? (double?)soundSpeed_mps : null,
                    !double.IsNaN(maxDist_m) ? (double?)maxDist_m : null
                });

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
            if (sentence is not NMEAProprietarySentence pSentence ||
                pSentence.Manufacturer != ManufacturerCodes.AZM)
                return;

            if ((!detected) && ("0123568!".Contains(pSentence.SentenceIDString)))
            {
                detected = true;
                StopTimer();
            }

            switch (pSentence.SentenceIDString)
            {
                case "0": Parse_ACK(pSentence.parameters); break;
                case "1": Parse_STRSTP(pSentence.parameters); break;
                case "2": Parse_RSTS(pSentence.parameters); break;
                case "3": Parse_NDTA(pSentence.parameters); break;
                case "5": Parse_RUCMD(pSentence.parameters); break;
                case "6": Parse_RBCAST(pSentence.parameters); break;
                case "8": Parse_CSET(pSentence.parameters); break;
                case "!": Parse_DINFO(pSentence.parameters); break;
            }
        }

        public EventHandler? IsWaitingLocalChangedEventHandler;
        public EventHandler? IsWaitingRemoteChangedEventHandler;

        public EventHandler<ACKReceivedEventArgs>? ACKReceived;
        public EventHandler<RSTSReceivedEventArgs>? RSTSReceived;
        public EventHandler<STRSTPReceivedEventArgs>? STRSTPReceived;
        public EventHandler<NDTAReceivedEventArgs>? NDTAReceived;
        public EventHandler<RUCMDReceivedEventArgs>? RUCMDReceived;
        public EventHandler<RBCASTReceivedEventArgs>? RBCASTReceived;
        public EventHandler<CSETReceivedEventArgs>? CSETReceived;

        public EventHandler? DeviceInfoValidChanged;
    }
}