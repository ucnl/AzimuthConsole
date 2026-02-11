using AzimuthConsole.AZM;
using System.Collections;
using System.Net;
using System.Text;
using UCNLDrivers;

namespace AzimuthConsole
{
    public enum LBLResponderCoordinatesModeEnum
    {
        None = 0,
        Cartesian = 1,
        Geographic = 2,
        Invalid
    }

    public class SettingsContainer : SimpleSettingsContainer
    {
        #region Properties

        public string azmPrefPortName = string.Empty;
        public BaudRate azmPortBaudrate = BaudRate.baudRate9600;

        public bool aux1Enabled = false;
        public string aux1PrefPortName = string.Empty;
        public BaudRate aux1PortBaudrate = BaudRate.baudRate9600;

        public bool aux2Enabled = false;
        public string aux2PrefPortName = string.Empty;
        public BaudRate aux2PortBaudrate = BaudRate.baudRate9600;

        public bool rctrl_enabled = false;
        public IPEndPoint rctrl_in_endpoint = new IPEndPoint(new IPAddress(new byte[] { 255, 255, 255, 255 }), 28127);
        public IPEndPoint rctrl_out_endpoint = new IPEndPoint(new IPAddress(new byte[] { 255, 255, 255, 255 }), 28129);

        public bool output_udp_enabled = false;
        public IPEndPoint output_endpoint = new IPEndPoint(new IPAddress(new byte[] { 255, 255, 255, 255 }), 28128);

        public bool outputSerialEnabled = false;
        public string outputSerialPortName = string.Empty;
        public BaudRate outputSerialPortBaudrate = BaudRate.baudRate9600;

        public UInt16 address_mask = 1;
        public int max_dist_m = 1000;

        public double sty_PSU = 0.0;
        public double antenna_x_offset_m = 0.0;
        public double antenna_y_offset_m = 0.0;
        public double antenna_angular_offset_deg = 0.0;

        public LBLResponderCoordinatesModeEnum LBLResponderCoordinatesMode = LBLResponderCoordinatesModeEnum.None;

        public double LBLModeR1X = double.NaN;
        public double LBLModeR1Y = double.NaN;
        public double LBLModeR2X = double.NaN;
        public double LBLModeR2Y = double.NaN;
        public double LBLModeR3X = double.NaN;
        public double LBLModeR3Y = double.NaN;

        public Dictionary<REMOTE_ADDR_Enum, IPEndPoint> InvidvidualEndpoints = new Dictionary<REMOTE_ADDR_Enum, IPEndPoint>();

        #endregion

        #region Constructor

        public override void SetDefaults()
        {
            azmPrefPortName = string.Empty;
            azmPortBaudrate = BaudRate.baudRate9600;

            aux1Enabled = false;
            aux1PrefPortName = string.Empty;
            aux1PortBaudrate = BaudRate.baudRate9600;

            aux2Enabled = false;
            aux2PrefPortName = string.Empty;
            aux2PortBaudrate = BaudRate.baudRate9600;

            rctrl_enabled = false;
            rctrl_in_endpoint = new IPEndPoint(new IPAddress(new byte[] { 255, 255, 255, 255 }), 28127);
            rctrl_out_endpoint = new IPEndPoint(new IPAddress(new byte[] { 255, 255, 255, 255 }), 28129);

            output_udp_enabled = false;
            output_endpoint = new IPEndPoint(new IPAddress(new byte[] { 255, 255, 255, 255 }), 28128);

            outputSerialEnabled = false;
            outputSerialPortName = string.Empty;
            outputSerialPortBaudrate = BaudRate.baudRate9600;

            address_mask = 1;

            max_dist_m = 1000;

            sty_PSU = 0.0;
            antenna_x_offset_m = 0.0;
            antenna_y_offset_m = 0.0;
            antenna_angular_offset_deg = 0.0;
        }

        private string FormatValue(object value)
        {
            if (value == null) return "null";

            if (value is Array array)
            {
                return string.Join(", ", array.Cast<object>().Select(FormatValue));
            }
            else if (IsGenericDictionary(value))
            {
                return FormatGenericDictionary(value);
            }
            else if (value is IDictionary nonGenericDict)
            {
                if (nonGenericDict.Count == 0) return "(empty)";

                var pairs = nonGenericDict.Cast<DictionaryEntry>()
                    .Select(entry => $"[{FormatValue(entry.Key)}={FormatValue(entry.Value)}]");
                return string.Join(", ", pairs);
            }
            else if (value.GetType().IsClass && !IsSystemType(value.GetType()))
            {
                return value.ToString() ?? "null";
            }
            else
            {
                return value.ToString() ?? "null";
            }
        }

        private bool IsGenericDictionary(object value)
        {
            var type = value.GetType();
            return type.IsGenericType &&
                   type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
        }

        private bool IsSystemType(Type type)
        {
            return type.Namespace?.StartsWith("System") == true;
        }

        private string FormatGenericDictionary(object dictionary)
        {
            try
            {
                var dictType = dictionary.GetType();
                var keyValuePairs = dictType.GetProperty("Keys")?.GetValue(dictionary) as System.Collections.IEnumerable;
                var values = dictType.GetProperty("Values")?.GetValue(dictionary) as System.Collections.IEnumerable;

                if (keyValuePairs == null || values == null) return "(invalid dictionary)";

                var keys = keyValuePairs.Cast<object>().ToList();
                var vals = values.Cast<object>().ToList();

                var pairs = new List<string>();
                for (int i = 0; i < keys.Count; i++)
                {
                    pairs.Add($"[{FormatValue(keys[i])}={FormatValue(vals[i])}]");
                }

                return pairs.Count > 0 ? string.Join(", ", pairs) : "(empty)";
            }
            catch
            {
                return "(error formatting dictionary)";
            }
        }


        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n");

            var fields = this.GetType().GetFields();

            foreach (var item in fields)
            {
                var value = item.GetValue(this);
                sb.AppendFormat("-- {0}: {1}\r\n", item.Name, FormatValue(value));
            }

            return sb.ToString();
        }

        #endregion
    }
}
