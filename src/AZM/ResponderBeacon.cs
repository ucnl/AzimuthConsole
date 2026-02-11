using System.Globalization;
using System.Net;
using System.Text;
using UCNLDrivers;
using UCNLNav;
using UCNLNav.TrackFilters;
using UCNLNMEA;

namespace AzimuthConsole.AZM
{
    public class ResponderBeacon
    {
        public REMOTE_ADDR_Enum Address { get; }
        public AgingValue<double> SRange_m { get; }
        public AgingValue<double> Depth_m { get; }
        public AgingValue<double> SRangeProjection_m { get; }
        public AgingValue<double> ADistance_m { get; }
        public AgingValue<double> Azimuth_deg { get; }
        public AgingValue<double> AAzimuth_deg { get; }
        public AgingValue<double> Elevation_deg { get; }
        public AgingValue<double> RAzimuth_deg { get; }
        public AgingValue<double> PTime_s { get; }
        public AgingValue<double> MSR_dB { get; }
        public AgingValue<double> VCC_V { get; }
        public AgingValue<double> WaterTemp_C { get; }
        public AgingValue<double> Lat_deg { get; }
        public AgingValue<double> Lon_deg { get; }
        public bool IsTimeout { get; set; }
        public AgingValue<string> Message { get; }

        public AgingValue<double> X_m { get; }
        public AgingValue<double> Y_m { get; }
        public AgingValue<double> Z_m { get; }


        List<IAging> avalues;

        public DHTrackFilter? DHFilterState { get; set; }
        public TrackMovingAverageSmoother? TFilterState { get; set; }        

        public int TotalRequests => SuccededRequests + Timeouts;
        public int SuccededRequests { get; set; }
        public int Timeouts { get; set; }


        bool isUDPInitialized = false;
        public bool IsIUDPInitialized
        {
            get => isUDPInitialized && (UDPOutput != null);
        }

        UDPTranslator? UDPOutput;

        public string UDPEndpointDescription
        {
            get => IsIUDPInitialized ? $"{UDPOutput?.Address}:{UDPOutput?.Port}" : string.Empty;
        }

        public string SuccessfulRequestStatistics => TotalRequests > 0 ? $"{100.0 * SuccededRequests / TotalRequests:F01}% ({SuccededRequests}/{TotalRequests})" : "- - -";

        public ResponderBeacon(REMOTE_ADDR_Enum id)
        {
            Address = id;

            SRange_m = new AgingValue<double>(int.MaxValue, 32, AZM.meters1dec_fmtr);
            SRange_m.Name = nameof(SRange_m);
            SRange_m.IgnoreAge = true;

            Azimuth_deg = new AgingValue<double>(int.MaxValue, 32, AZM.degrees1dec_fmtr);
            Azimuth_deg.Name = nameof(Azimuth_deg);
            Azimuth_deg.IgnoreAge = true; ;

            PTime_s = new AgingValue<double>(int.MaxValue, 32, x => string.Format(CultureInfo.InvariantCulture, "{0:F04}", x));
            PTime_s.Name = nameof(PTime_s);
            PTime_s.IgnoreAge = true;

            MSR_dB = new AgingValue<double>(int.MaxValue, 32, AZM.db_fmtr);
            MSR_dB.Name = nameof(MSR_dB);

            Depth_m = new AgingValue<double>(int.MaxValue, 32, AZM.meters1dec_fmtr);
            Depth_m.Name = nameof(Depth_m);
            SRangeProjection_m = new AgingValue<double>(int.MaxValue, 32, AZM.meters1dec_fmtr);
            SRangeProjection_m.Name = nameof(SRangeProjection_m);

            ADistance_m = new AgingValue<double>(int.MaxValue, 32, AZM.meters1dec_fmtr);
            ADistance_m.Name = nameof(ADistance_m);
            AAzimuth_deg = new AgingValue<double>(int.MaxValue, 32, AZM.degrees1dec_fmtr);
            AAzimuth_deg.Name = nameof(AAzimuth_deg);
            Elevation_deg = new AgingValue<double>(int.MaxValue, 32, AZM.degrees1dec_fmtr);
            Elevation_deg.Name = nameof(Elevation_deg);

            VCC_V = new AgingValue<double>(int.MaxValue, 300, AZM.v1dec_fmt);
            VCC_V.Name = nameof(VCC_V);
            WaterTemp_C = new AgingValue<double>(int.MaxValue, 300, AZM.degC_fmtr);
            WaterTemp_C.Name = nameof(WaterTemp_C);
            Lat_deg = new AgingValue<double>(int.MaxValue, 32, AZM.latlon_fmtr);
            Lat_deg.Name = nameof(Lat_deg);
            Lat_deg.IgnoreAge = true;
            Lon_deg = new AgingValue<double>(int.MaxValue, 32, AZM.latlon_fmtr);
            Lon_deg.Name = nameof(Lon_deg);
            RAzimuth_deg = new AgingValue<double>(int.MaxValue, 32, AZM.degrees1dec_fmtr);
            RAzimuth_deg.Name = nameof(RAzimuth_deg);

            X_m = new AgingValue<double>(100000, 100000, AZM.meters3dec_fmtr);
            X_m.IgnoreAge = true;
            X_m.Name = nameof(X_m);
            Y_m = new AgingValue<double>(100000, 100000, AZM.meters3dec_fmtr);
            Y_m.IgnoreAge = true;
            Y_m.Name = nameof(Y_m);
            Z_m = new AgingValue<double>(100000, 100000, AZM.meters1dec_fmtr);
            Z_m.IgnoreAge = true;
            Z_m.Name = nameof(Z_m);            

            Message = new AgingValue<string>(3, 32, x => x);
            Message.Name = nameof(Message);

            IsTimeout = false;

            avalues = [ SRange_m, Azimuth_deg, PTime_s, MSR_dB, Depth_m, SRangeProjection_m,
                        ADistance_m, AAzimuth_deg, Elevation_deg, VCC_V, WaterTemp_C,
                        Lat_deg, Lon_deg, RAzimuth_deg, Message, X_m, Y_m, Z_m ];

            DHFilterState = null;
            TFilterState = null;
        }


        public void InitIUDPOutput(IPEndPoint ep)
        {
            isUDPInitialized = false;
            UDPOutput = new UDPTranslator(ep.Port, ep.Address);
            isUDPInitialized = true;
        }

        public void DeInitUDPOutput()
        {
            isUDPInitialized = false;
        }




        public string GetToStringFormat()
        {
            StringBuilder sb = new();

            sb.Append("@AZMREM,rem_addr,");

            foreach (IAging value in avalues)
            {
                Utils.AppendAgingValueDesciption(sb, value);
            }

            sb.Append("IsTimeout");

            return sb.ToString();
        }

        public override string ToString()
        {
            StringBuilder sb = new();

            sb.AppendFormat("@AZMREM,{0},", (int)Address);

            foreach (IAging a in avalues)
            {
                Utils.AppendAgingValue(sb, a);
            }

            sb.AppendFormat("{0},", IsTimeout);

            return sb.ToString();
        }

        public string ToNMEAStrings()
        {
            StringBuilder sb = new();

            var ltCardinal = Lat_deg.IsInitializedAndNotObsolete ? (Lat_deg.Value > 0 ? "N" : "S") : string.Empty;
            var lnCardinal = Lon_deg.IsInitializedAndNotObsolete ? (Lon_deg.Value > 0 ? "E" : "W") : string.Empty;

            bool location_valid = Lat_deg.IsInitializedAndNotObsolete && Lon_deg.IsInitializedAndNotObsolete;

            sb.Append(
                NMEAParser.BuildSentence(
                    TalkerIdentifiers.GN,
                    SentenceIdentifiers.RMC,
                    [
                        DateTime.UtcNow,
                        location_valid ? "Valid" : "Invalid",
                        location_valid ? Lat_deg.Value : null, ltCardinal,
                        location_valid ? Lon_deg.Value : null, lnCardinal,
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
                        location_valid ? Lat_deg.Value : null, ltCardinal,
                        location_valid ? Lon_deg.Value : null, lnCardinal,
                        "GPS fix",
                        4,
                        null,
                        Depth_m.IsInitializedAndNotObsolete ? -Depth_m.Value : null,
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
                        WaterTemp_C.IsInitializedAndNotObsolete ? WaterTemp_C.Value : null,
                    ]));

            return sb.ToString();
        }

        public void SendToIUDP(string data)
        {
            if (IsIUDPInitialized)
                UDPOutput?.Send(data);
        }
    }
}
