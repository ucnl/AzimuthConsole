using System.Globalization;
using System.Text;
using UCNLNav;
using UCNLNav.TrackFilters;

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


        List<IAging> avalues;

        public DHTrackFilter DHFilterState { get; set; }
        public TrackMovingAverageSmoother TFilterState { get; set; }

        public int TotalRequests => SuccededRequests + Timeouts;
        public int SuccededRequests { get; set; }
        public int Timeouts { get; set; }

        public string SuccessfulRequestStatistics => TotalRequests > 0 ? $"{100.0 * SuccededRequests / TotalRequests:F01}% ({SuccededRequests}/{TotalRequests})" : "- - -";

        public ResponderBeacon(REMOTE_ADDR_Enum id)
        {
            Address = id;

            SRange_m = new AgingValue<double>(3, 32, AZM.meters1dec_fmtr);
            SRange_m.Name = nameof(SRange_m);
            SRange_m.IgnoreAge = true;

            Azimuth_deg = new AgingValue<double>(3, 32, AZM.degrees1dec_fmtr);
            Azimuth_deg.Name = nameof(Azimuth_deg);
            Azimuth_deg.IgnoreAge = true; ;

            PTime_s = new AgingValue<double>(3, 32, x => string.Format(CultureInfo.InvariantCulture, "{0:F04}", x));
            PTime_s.Name = nameof(PTime_s);
            PTime_s.IgnoreAge = true;

            MSR_dB = new AgingValue<double>(3, 32, AZM.db_fmtr);
            MSR_dB.Name = nameof(MSR_dB);

            Depth_m = new AgingValue<double>(3, 32, AZM.meters1dec_fmtr);
            Depth_m.Name = nameof(Depth_m);
            SRangeProjection_m = new AgingValue<double>(3, 32, AZM.meters1dec_fmtr);
            SRangeProjection_m.Name = nameof(SRangeProjection_m);

            ADistance_m = new AgingValue<double>(3, 32, AZM.meters1dec_fmtr);
            ADistance_m.Name = nameof(ADistance_m);
            AAzimuth_deg = new AgingValue<double>(3, 32, AZM.degrees1dec_fmtr);
            AAzimuth_deg.Name = nameof(AAzimuth_deg);
            Elevation_deg = new AgingValue<double>(3, 32, AZM.degrees1dec_fmtr);
            Elevation_deg.Name = nameof(Elevation_deg);

            VCC_V = new AgingValue<double>(3, 300, AZM.v1dec_fmt);
            VCC_V.Name = nameof(VCC_V);
            WaterTemp_C = new AgingValue<double>(3, 300, AZM.degC_fmtr);
            WaterTemp_C.Name = nameof(WaterTemp_C);
            Lat_deg = new AgingValue<double>(3, 32, AZM.latlon_fmtr);
            Lat_deg.Name = nameof(Lat_deg);
            Lat_deg.IgnoreAge = true;
            Lon_deg = new AgingValue<double>(3, 32, AZM.latlon_fmtr);
            Lon_deg.Name = nameof(Lon_deg);
            RAzimuth_deg = new AgingValue<double>(3, 32, AZM.degrees1dec_fmtr);
            RAzimuth_deg.Name = nameof(RAzimuth_deg);

            Message = new AgingValue<string>(3, 32, x => x);
            Message.Name = nameof(Message);

            IsTimeout = false;

            avalues = new List<IAging>() { SRange_m, Azimuth_deg, PTime_s, MSR_dB, Depth_m, SRangeProjection_m,
                                           ADistance_m, AAzimuth_deg, Elevation_deg, VCC_V, WaterTemp_C,
                                           Lat_deg, Lon_deg, RAzimuth_deg, Message };
        }


        public string GetToStringFormat()
        {
            StringBuilder sb = new StringBuilder();

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
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("@AZMREM,{0},", Address);

            foreach (IAging a in avalues)
            {
                Utils.AppendAgingValue(sb, a);
            }

            sb.AppendFormat("{0},", IsTimeout);

            return sb.ToString();
        }
    }
}
