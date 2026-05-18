// AzimuthConsole/ApplicationSettings.cs
using System.Net;
using UCNLDrivers;

namespace AzimuthConsole
{
    public class ApplicationSettings : SimpleSettingsContainer
    {
        // Веб-сервер
        public bool WebServerEnabled = true;

        // Калибровка — значения по умолчанию для LHOV
        public double CalibrationDefaultLat = 48.524167;
        public double CalibrationDefaultLon = 44.515644;
        public double CalibrationDefaultHdg = 0.0;

        // Таблица калибровки антенны
        public string AntennaCalibrationTableFile = string.Empty;

        // Режим логирования в консоль
        public ConsoleLogMode ConsoleLogMode = ConsoleLogMode.Normal;

        public override void SetDefaults()
        {
            WebServerEnabled = true;

            CalibrationDefaultLat = 48.524167;
            CalibrationDefaultLon = 44.515644;
            CalibrationDefaultHdg = 0.0;

            AntennaCalibrationTableFile = string.Empty;

            ConsoleLogMode = ConsoleLogMode.Normal;
        }
    }

    public enum ConsoleLogMode
    {
        Normal,     // INFO + ERROR
        ErrorsOnly, // только ERROR
        Silent      // ничего
    }
}