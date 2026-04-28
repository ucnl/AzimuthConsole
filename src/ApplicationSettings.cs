using System.Net;
using UCNLDrivers;

namespace AzimuthConsole
{
    public class ApplicationSettings : SimpleSettingsContainer
    {
        public bool webServerEnabled = true;

        public bool rctrl_enabled = false;
        public IPEndPoint rctrl_in_endpoint = new(IPAddress.Broadcast, 28127);
        public IPEndPoint rctrl_out_endpoint = new(IPAddress.Broadcast, 28129);

        public bool antennaRotatorEnabled = false;
        public string antennaRotatorPortName = string.Empty;
        public bool antennaRotatorPortFixed = false;
        public BaudRate antennaRotatorPortBaudrate = BaudRate.baudRate9600;

        public string antennaCalibrationTableFile = string.Empty;

        public double calibrationDefaultLat = 48.524167;
        public double calibrationDefaultLon = 44.515644;
        public double calibrationDefaultHdg = 0.0;

        public override void SetDefaults()
        {
            webServerEnabled = true;

            rctrl_enabled = false;
            rctrl_in_endpoint = new(IPAddress.Broadcast, 28127);
            rctrl_out_endpoint = new(IPAddress.Broadcast, 28129);

            antennaRotatorEnabled = false;
            antennaRotatorPortName = string.Empty;
            antennaRotatorPortBaudrate = BaudRate.baudRate9600;

            antennaCalibrationTableFile = string.Empty;

            calibrationDefaultLat = 48.524167;
            calibrationDefaultLon = 44.515644;
            calibrationDefaultHdg = 0.0;
        }
    }
}
