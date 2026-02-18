using System.Text;
using UCNLNav;
using UCNLNMEA;

namespace AzimuthConsole.AZM
{
    public static class Utils
    {
        public static void AppendAgingValue(StringBuilder sb, IAging avalue)
        {
            if (avalue.IgnoreAge)
            {
                if (avalue.IsInitializedAndNotObsolete)
                {
                    sb.Append(FormattableString.Invariant($"{avalue},"));
                }
                else
                {
                    sb.Append(",");
                }
            }
            else
            {
                if (avalue.IsInitializedAndNotObsolete)
                {
                    sb.Append(FormattableString.Invariant($"{avalue},{avalue.Age.TotalSeconds:F1},"));
                }
                else
                {
                    sb.Append(",,");
                }
            }
        }

        public static void AppendAgingValueDesciption(StringBuilder sb, IAging value)
        {
            sb.AppendFormat("{0},", value.Name);
            if (!value.IgnoreAge)
                sb.Append("age,");
        }

        public static string BuildRMCGGAMTWNMEAMessages(AgingValue<double> lat, AgingValue<double> lon, DateTime dt, AgingValue<double> dpt, AgingValue<double> wtemp)
        {
            StringBuilder sb = new();

            var ltCardinal = lat.IsInitializedAndNotObsolete ? (lat.Value > 0 ? "N" : "S") : null;
            var lnCardinal = lon.IsInitializedAndNotObsolete ? (lon.Value > 0 ? "E" : "W") : null;

            bool location_valid = lat.IsInitializedAndNotObsolete && lon.IsInitializedAndNotObsolete;

            sb.Append(
                NMEAParser.BuildSentence(
                    TalkerIdentifiers.GN,
                    SentenceIdentifiers.RMC,
                    [
                        dt,
                        location_valid ? "Valid" : "Invalid",
                        location_valid ? lat.Value : null, ltCardinal,
                        location_valid ? lon.Value : null, lnCardinal,
                        null,
                        null,
                        dt,
                        null,
                        null,
                        location_valid ? "A" : "V",
                    ]));

            sb.Append(
                NMEAParser.BuildSentence(
                    TalkerIdentifiers.GN,
                    SentenceIdentifiers.GGA,
                    [
                        dt,
                        location_valid ? lat.Value : null, ltCardinal,
                        location_valid ? lon.Value : null, lnCardinal,
                        "GPS fix",
                        4,
                        null,
                        dpt.IsInitializedAndNotObsolete ? -dpt.Value : null,
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
                        wtemp.IsInitialized ? wtemp.Value : null,
                    ]));

            return sb.ToString();
        }
    }
}
