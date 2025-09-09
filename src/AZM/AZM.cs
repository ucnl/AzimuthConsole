using System.Globalization;

namespace AzimuthConsole.AZM
{
    #region Custom enums
    public enum ICs
    {
        IC_D2H_ACK,
        IC_D2D_STRSTP,
        IC_D2D_RSTS,
        IC_D2H_NDTA,
        IC_H2D_DPTOVR,
        IC_D2H_RUCMD,
        IC_D2H_RBCAST,
        IC_H2D_CREQ,
        IC_D2D_CSET,
        IC_H2D_DINFO_GET,
        IC_D2H_DINFO,
        IC_ANY,
        IC_INVALID
    }

    public enum REMOTE_ADDR_Enum
    {
        REM_ADDR_1 = 0,
        REM_ADDR_2 = 1,
        REM_ADDR_3 = 2,
        REM_ADDR_4 = 3,
        REM_ADDR_5 = 4,
        REM_ADDR_6 = 5,
        REM_ADDR_7 = 6,
        REM_ADDR_8 = 7,
        REM_ADDR_9 = 8,
        REM_ADDR_10 = 9,
        REM_ADDR_11 = 10,
        REM_ADDR_12 = 11,
        REM_ADDR_13 = 12,
        REM_ADDR_14 = 13,
        REM_ADDR_15 = 14,
        REM_ADDR_16 = 15,
        REM_ADDR_INVALID
    }

    public enum NDTA_Status_Enum
    {
        NDTA_LOC_ONLY = 0,
        NDTA_REMR = 1,
        NDTA_REMT = 2,
        NDTA_REMB = 3,
        NDTA_INVALID
    }

    public enum IC_RESULT_Enum
    {
        IC_RES_OK = 0,
        IC_RES_INVALID_SYNTAX = 1,
        IC_RES_UNSUPPORTED_CMD = 2,
        IC_RES_ARGUMENT_OUT_OF_RANGE = 3,
        IC_RES_INVALID_OPERATION = 4,
        IC_RES_VALUE_UNAVAILABLE = 5,
        IC_RES_TX_BUSY = 6,
        IC_RES_RX_BUSY = 7,
        IC_RES_INVALID
    }

    public enum CDS_REQ_CODES_Enum
    {
        CDS_REQ_DPT = 0,
        CDS_REQ_TMP = 1,
        CDS_REQ_VCC = 2,
        CDS_REQ_USER_CMD_27 = 3,
        CDS_REQ_USER_CMD_26 = 4,
        CDS_REQ_USER_CMD_25 = 5,
        CDS_REQ_USER_CMD_24 = 6,
        CDS_REQ_USER_CMD_23 = 7,
        CDS_REQ_USER_CMD_22 = 8,
        CDS_REQ_USER_CMD_21 = 9,
        CDS_REQ_USER_CMD_20 = 10,
        CDS_REQ_USER_CMD_19 = 11,
        CDS_REQ_USER_CMD_18 = 12,
        CDS_REQ_USER_CMD_17 = 13,
        CDS_REQ_USER_CMD_16 = 14,
        CDS_REQ_USER_CMD_15 = 15,
        CDS_REQ_USER_CMD_14 = 16,
        CDS_REQ_USER_CMD_13 = 17,
        CDS_REQ_USER_CMD_12 = 18,
        CDS_REQ_USER_CMD_11 = 19,
        CDS_REQ_USER_CMD_10 = 20,
        CDS_REQ_USER_CMD_9 = 21,
        CDS_REQ_USER_CMD_8 = 22,
        CDS_REQ_USER_CMD_7 = 23,
        CDS_REQ_USER_CMD_6 = 24,
        CDS_REQ_USER_CMD_5 = 25,
        CDS_REQ_USER_CMD_4 = 26,
        CDS_REQ_USER_CMD_3 = 27,
        CDS_REQ_USER_CMD_2 = 28,
        CDS_REQ_USER_CMD_1 = 29,
        CDS_REQ_USER_CMD_0 = 30,
        CDS_REQ_INVALID
    }

    public enum CDS_RESP_CODES_Enum
    {
        CDS_ERR_RES_0 = 500,
        CDS_ERR_RES_1 = 501,
        CDS_ERR_RES_2 = 502,
        CDS_ERR_RES_3 = 503,
        CDS_ERR_RES_4 = 504,
        CDS_ACK = 505,
        CDS_ERR_NAVAIL = 506,
        CDS_ERR_NSUPP = 507,
        CDS_ERR_BAT_LOW = 508,
        CDS_RSYS_STRT = 509,
        CDS_INVALID
    }

    public enum CDS_RBCAST_CODES_Enum
    {
        CDS_BCAST_FUNC_0 = 497,
        CDS_BCAST_FUNC_1 = 498,
        CDS_BCAST_FUNC_2 = 499,
        CDS_BCAST_FUNC_3 = 500,
        CDS_BCAST_FUNC_4 = 501,
        CDS_BCAST_STY_SET_0 = 502,
        CDS_BCAST_STY_SET_5 = 503,
        CDS_BCAST_STY_SET_10 = 504,
        CDS_BCAST_STY_SET_15 = 505,
        CDS_BCAST_STY_SET_20 = 506,
        CDS_BCAST_STY_SET_25 = 507,
        CDS_BCAST_STY_SET_30 = 508,
        CDS_BCAST_STY_SET_35 = 509,
        CDS_BCAST_STY_SET_40 = 510,
        CDS_BCAST_INVALID
    }

    public enum AZM_DEVICE_TYPE_Enum
    {
        DT_BASE = 0,
        DT_REMOTE = 1,
        DT_INVALID
    }

    public enum AZM_PTS_TYPE_Enum
    {
        PTS_NO_SENSOR = 0,
        PTS_4LD_100_BAR = 1,   // 01
        PTS_5803_30_BAR = 2,   // 10
        PTS_5837_30_BAR = 3,   // 11
        PTS_INVALID
    }

    #endregion

    public static class AZM
    {
        #region Properties

        public static readonly int ABS_MAX_RANGE_M = 5500;
        public static readonly int ABS_MIN_RANGE_M = 500;

        public static readonly double ABS_MAX_VCC_V = 30.0;
        public static readonly double ABS_MIN_VCC_V = 0.0;

        public static readonly double ABS_MAX_TEMP_C = -10.0;
        public static readonly double ABS_MIN_TEMP_C = 80.0;

        public static readonly double CRANGE = 499;



        static readonly Dictionary<string, ICs> ICsIdxArray = new Dictionary<string, ICs>()
        {
            { "0", ICs.IC_D2H_ACK },
            { "1", ICs.IC_D2D_STRSTP },
            { "2", ICs.IC_D2D_RSTS },
            { "3", ICs.IC_D2H_NDTA },
            { "4", ICs.IC_H2D_DPTOVR },
            { "5", ICs.IC_D2H_RUCMD },
            { "6", ICs.IC_D2H_RBCAST },
            { "7", ICs.IC_H2D_CREQ },
            { "8", ICs.IC_D2D_CSET },
            { "?", ICs.IC_H2D_DINFO_GET },

            { "!", ICs.IC_D2H_DINFO },
            { "-", ICs.IC_ANY },
        };

        public static readonly Func<object, IC_RESULT_Enum> O2_IC_RESULT_Enum = o => o == null ? IC_RESULT_Enum.IC_RES_INVALID : (IC_RESULT_Enum)(int)o;
        public static readonly Func<object, REMOTE_ADDR_Enum> O2_REMOTE_ADDR_Enum = o => o == null ? REMOTE_ADDR_Enum.REM_ADDR_INVALID : (REMOTE_ADDR_Enum)((int)o);
        public static readonly Func<object, NDTA_Status_Enum> O2_NDTA_Status_Enum = o => o == null ? NDTA_Status_Enum.NDTA_INVALID : (NDTA_Status_Enum)(int)o;
        public static readonly Func<object, CDS_REQ_CODES_Enum> O2_CDS_REQ_CODES_Enum = o => o == null ? CDS_REQ_CODES_Enum.CDS_REQ_INVALID : (CDS_REQ_CODES_Enum)(int)o;
        public static readonly Func<object, CDS_RBCAST_CODES_Enum> O2_CDS_RBCAST_CODES_Enum = o => o == null ? CDS_RBCAST_CODES_Enum.CDS_BCAST_INVALID : (CDS_RBCAST_CODES_Enum)(int)o;
        public static readonly Func<object, AZM_DEVICE_TYPE_Enum> O2_AZM_DEVICE_TYPE_Enum = o => o == null ? AZM_DEVICE_TYPE_Enum.DT_INVALID : (AZM_DEVICE_TYPE_Enum)(int)o;
        public static readonly Func<object, AZM_PTS_TYPE_Enum> O2_AZM_PTS_TYPE_Enum = o => o == null ? AZM_PTS_TYPE_Enum.PTS_INVALID : (AZM_PTS_TYPE_Enum)(int)o;


        public static readonly Func<object, string> O2S = o => o == null ? string.Empty : (string)o;
        public static readonly Func<object, double> O2D = o => o == null ? double.NaN : (double)o;
        public static readonly Func<object, int> O2S32 = o => o == null ? -1 : (int)o;
        public static readonly Func<object, UInt16> O2U16 = o => o == null ? UInt16.MinValue : (UInt16)(int)o;


        public static readonly Func<double, string> meters1dec_fmtr = o => string.Format(CultureInfo.InvariantCulture, "{0:F01}", o);
        public static readonly Func<double, string> degrees1dec_fmtr = o => string.Format(CultureInfo.InvariantCulture, "{0:F01}", o);
        public static readonly Func<double, string> latlon_fmtr = o => string.Format(CultureInfo.InvariantCulture, "{0:F06}", o);
        public static readonly Func<double, string> db_fmtr = o => string.Format(CultureInfo.InvariantCulture, "{0:F01}", o);
        public static readonly Func<double, string> degC_fmtr = o => string.Format(CultureInfo.InvariantCulture, "{0:F01}", o);
        public static readonly Func<double, string> mBar_fmtr = o => string.Format(CultureInfo.InvariantCulture, "{0:F01}", o);
        public static readonly Func<double, string> v1dec_fmt = o => string.Format(CultureInfo.InvariantCulture, "{0:F01}", o);

        public static readonly Func<double, double, double, bool> IsInRangeInclusive = (v, min, max) => (v >= min) && (v <= max);
        public static readonly Func<double, bool> IsLatDeg = v => IsInRangeInclusive(v, -90, 90);
        public static readonly Func<double, bool> IsLonDeg = v => IsInRangeInclusive(v, -180, 180);
        public static readonly Func<double, bool> IsHdnDeg = v => IsInRangeInclusive(v, 0, 360);


        #endregion

        #region Methods

        public static IEnumerable<REMOTE_ADDR_Enum> GetAddrsByMask(ushort mask)
        {
            List<REMOTE_ADDR_Enum> result = new List<REMOTE_ADDR_Enum>();

            for (ushort i = 0; i < mask; i++)
            {
                if ((mask & (1 << i)) != 0)
                    result.Add((REMOTE_ADDR_Enum)i);
            }

            return result;
        }

        public static string Addr2Str(REMOTE_ADDR_Enum addr)
        {
            return ((int)addr + 1).ToString();
        }

        public static bool IsErrorCode(int rc_resp_code)
        {
            return rc_resp_code >= (int)CDS_RESP_CODES_Enum.CDS_ERR_RES_0;
        }

        public static bool IsUserDataReqCode(CDS_REQ_CODES_Enum rc_req_code)
        {
            return ((int)rc_req_code >= (int)CDS_REQ_CODES_Enum.CDS_REQ_USER_CMD_27) && ((int)rc_req_code <= (int)CDS_REQ_CODES_Enum.CDS_REQ_USER_CMD_0);
        }

        public static ICs ICsByMessageID(string msgID)
        {
            if (ICsIdxArray.ContainsKey(msgID))
                return ICsIdxArray[msgID];
            else
                return ICs.IC_INVALID;
        }

        public static string BCDVersionToStr(int versionData)
        {
            return string.Format("{0}.{1}", (versionData >> 0x08).ToString(), (versionData & 0xff).ToString("X2"));
        }

        #endregion
    }
}
