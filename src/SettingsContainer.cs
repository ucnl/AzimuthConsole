using System.Net;
using UCNLDrivers;

namespace AzimuthConsole
{
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

        #endregion
    }
}
