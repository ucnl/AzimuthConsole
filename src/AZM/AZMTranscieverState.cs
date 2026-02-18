using System.Globalization;
using System.Text;
using UCNLNav;

namespace AzimuthConsole.AZM
{
    public class AZMTranscieverState
    {
        public AgingValue<double> StPressure_mBar { get; } = new AgingValue<double>(int.MaxValue, 10, AZM.mBar_fmtr);
        public AgingValue<double> StDepth_m { get; } = new AgingValue<double>(int.MaxValue, 10, AZM.meters1dec_fmtr);
        public AgingValue<double> WaterTemp_C { get; } = new AgingValue<double>(int.MaxValue, 10, AZM.degC_fmtr);
        public AgingValue<double> StPitch_deg { get; } = new AgingValue<double>(int.MaxValue, 10, AZM.degrees1dec_fmtr);
        public AgingValue<double> StRoll_deg { get; } = new AgingValue<double>(int.MaxValue, 10, AZM.degrees1dec_fmtr);

        public AgingValue<double> Lat_deg { get; } = new AgingValue<double>(int.MaxValue, 10, AZM.latlon_fmtr);
        public AgingValue<double> Lon_deg { get; } = new AgingValue<double>(int.MaxValue, 10, AZM.latlon_fmtr);
        public AgingValue<double> Course_deg { get; } = new AgingValue<double>(int.MaxValue, 10, AZM.degrees1dec_fmtr);
        public AgingValue<double> Speed_mps { get; } = new AgingValue<double>(int.MaxValue, 10, x => string.Format(CultureInfo.InvariantCulture, "{0:F01}", x / 3.6));
        public AgingValue<double> Heading_deg { get; } = new AgingValue<double>(int.MaxValue, 10, AZM.degrees1dec_fmtr);

        public AgingValue<double> X_m { get; } = new AgingValue<double>(int.MaxValue, 10, AZM.meters3dec_fmtr);
        public AgingValue<double> Y_m { get; } = new AgingValue<double>(int.MaxValue, 10, AZM.meters3dec_fmtr);
        public AgingValue<double> Z_m { get; } = new AgingValue<double>(int.MaxValue, 10, AZM.meters3dec_fmtr);
        public AgingValue<double> Rerr_m { get; } = new AgingValue<double>(int.MaxValue, 10, AZM.meters3dec_fmtr);

        readonly List<IAging> stationParams;

        public AZMTranscieverState()
        {            
            stationParams = [ StPressure_mBar, StDepth_m, WaterTemp_C, 
                              StPitch_deg, StRoll_deg, 
                              Lat_deg, Lon_deg, Course_deg, Speed_mps, Heading_deg,
                              X_m, Y_m, Z_m, Rerr_m ];         

            ConfigureAgingValues();
        }

        private void ConfigureAgingValues()
        {            
            var allValues = GetAllAgingValues();
            foreach (var value in allValues)
            {
                value.IgnoreAge = true;
                value.Name = GetPropertyNameForValue(value);
            }
            
            StRoll_deg.IgnoreAge = false;
            Speed_mps.IgnoreAge = false;
            Heading_deg.IgnoreAge = false;            
            Rerr_m.IgnoreAge = false;

        }
        public IEnumerable<AgingValue<double>> GetAllAgingValues()
        {
            return GetType()
                .GetProperties()
                .Where(p => p.PropertyType.IsGenericType &&
                             p.PropertyType.GetGenericTypeDefinition() == typeof(AgingValue<>))
                .Select(p => (AgingValue<double>)p.GetValue(this))
                .Where(v => v != null);
        }
        private string GetPropertyNameForValue(AgingValue<double> value)
        {
            return GetType()
                .GetProperties()
                .First(p => ReferenceEquals(p.GetValue(this), value))
                .Name;
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

        public string StationParametersToString()
        {
            StringBuilder sb = new();

            sb.Append("@AZMLOC,");

            foreach (IAging avalue in stationParams)
            {
                Utils.AppendAgingValue(sb, avalue);
            }

            return sb.ToString();
        }
    }
}
