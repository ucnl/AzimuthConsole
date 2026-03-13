using AzimuthConsole.AZM;
using System.Text.Json;
using System.Text.Json.Serialization;
using UCNLNav;

namespace AzimuthConsole.Web
{
    /// <summary>
    /// Сервис для преобразования данных AZMCombiner в формат для веб-интерфейса
    /// </summary>
    public class WebDataConverter
    {
        private readonly AZMCombiner _combiner;
        private readonly JsonSerializerOptions _jsonOptions;

        public WebDataConverter(AZMCombiner combiner)
        {
            _combiner = combiner ?? throw new ArgumentNullException(nameof(combiner));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new SafeDoubleJsonConverter() },
                WriteIndented = false
            };
        }

        /// <summary>
        /// Преобразует текущее состояние комбайнера в WebUIData
        /// </summary>
        public WebUIData ConvertToWebData(string? lastLine = null)
        {
            var data = new WebUIData
            {
                Timestamp = DateTime.UtcNow,
                RecentLogs = new List<LogEntry>(),
                Beacons = new List<BeaconInfo>(),
                LocalDevice = new DeviceInfo(),
                SystemInfo = new SystemInfo()
            };

            try
            {
                // Определяем режим работы
                data.Mode = _combiner.DeviceType switch
                {
                    AZM_DEVICE_TYPE_Enum.DT_USBL_TSV => "usbl",
                    AZM_DEVICE_TYPE_Enum.DT_LBL_TSV => "lbl",
                    _ => "unknown"
                };

                // Данные локального устройства
                ConvertLocalDevice(data);

                // Данные маяков
                ConvertBeacons(data);

                // Системная информация
                ConvertSystemInfo(data);

                // Добавляем последний лог
                if (!string.IsNullOrEmpty(lastLine))
                {
                    data.RecentLogs.Add(new LogEntry
                    {
                        Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                        Message = lastLine.Length > 100 ? lastLine.Substring(0, 100) + "..." : lastLine,
                        Type = DetermineLogType(lastLine)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebDataConverter] Error converting data: {ex.Message}");
            }

            return data;
        }

        /// <summary>
        /// Преобразует данные и возвращает JSON
        /// </summary>
        public string ConvertToJson(string? lastLine = null)
        {
            var data = ConvertToWebData(lastLine);
            return JsonSerializer.Serialize(data, _jsonOptions);
        }

        private void ConvertLocalDevice(WebUIData data)
        {
            if (_combiner.state == null) return;

            var state = _combiner.state;

            // Метрические координаты
            if (state.X_m?.IsInitializedAndNotObsolete == true)
                data.LocalDevice.X = SafeRound(state.X_m.Value, 2);

            if (state.Y_m?.IsInitializedAndNotObsolete == true)
                data.LocalDevice.Y = SafeRound(state.Y_m.Value, 2);

            if (state.Z_m?.IsInitializedAndNotObsolete == true)
                data.LocalDevice.Z = SafeRound(state.Z_m.Value, 2);

            // Географические координаты
            if (state.Lat_deg?.IsInitializedAndNotObsolete == true)
                data.LocalDevice.Latitude = SafeRound(state.Lat_deg.Value, 6);

            if (state.Lon_deg?.IsInitializedAndNotObsolete == true)
                data.LocalDevice.Longitude = SafeRound(state.Lon_deg.Value, 6);

            // Ориентация
            if (state.Heading_deg?.IsInitializedAndNotObsolete == true)
            {
                data.LocalDevice.Heading = SafeRound(state.Heading_deg.Value, 1);
                data.LocalDevice.HasHeading = true;
            }

            if (state.StPitch_deg?.IsInitializedAndNotObsolete == true)
                data.LocalDevice.Pitch = SafeRound(state.StPitch_deg.Value, 1);

            if (state.StRoll_deg?.IsInitializedAndNotObsolete == true)
                data.LocalDevice.Roll = SafeRound(state.StRoll_deg.Value, 1);

            if (state.Course_deg?.IsInitializedAndNotObsolete == true)
                data.LocalDevice.Course = SafeRound(state.Course_deg.Value, 1);

            if (state.Speed_mps?.IsInitializedAndNotObsolete == true)
                data.LocalDevice.Speed = SafeRound(state.Speed_mps.Value, 2);

            // Параметры среды
            if (state.StDepth_m?.IsInitializedAndNotObsolete == true)
                data.LocalDevice.Depth = SafeRound(state.StDepth_m.Value, 1);

            if (state.WaterTemp_C?.IsInitializedAndNotObsolete == true)
                data.LocalDevice.Temperature = SafeRound(state.WaterTemp_C.Value, 1);

            if (state.StPressure_mBar?.IsInitializedAndNotObsolete == true)
                data.LocalDevice.Pressure = SafeRound(state.StPressure_mBar.Value, 1);

            if (state.Rerr_m?.IsInitializedAndNotObsolete == true)
                data.LocalDevice.RError = SafeRound(state.Rerr_m.Value, 2);

            // Возраст данных
            data.LocalDevice.DataAge = GetMinAge(state);
        }

        private void ConvertBeacons(WebUIData data)
        {
            if (_combiner.remotes == null || _combiner.remotes.Count == 0) return;

            foreach (var kvp in _combiner.remotes)
            {
                var beacon = kvp.Value;
                if (beacon == null) continue;

                var beaconInfo = new BeaconInfo
                {
                    Address = (int)beacon.Address + 1,
                    IsTimeout = beacon.IsTimeout
                };

                // Полярные координаты
                if (beacon.SRangeProjection_m?.IsInitializedAndNotObsolete == true)
                    beaconInfo.Distance = SafeRound(beacon.SRangeProjection_m.Value, 1);

                if (beacon.Azimuth_deg?.IsInitializedAndNotObsolete == true)
                    beaconInfo.Azimuth = SafeRound(beacon.Azimuth_deg.Value, 1);

                if (beacon.Elevation_deg?.IsInitializedAndNotObsolete == true)
                    beaconInfo.Elevation = SafeRound(beacon.Elevation_deg.Value, 1);

                // Абсолютные координаты
                if (beacon.ADistance_m?.IsInitializedAndNotObsolete == true)
                    beaconInfo.AbsoluteDistance = SafeRound(beacon.ADistance_m.Value, 1);

                if (beacon.AAzimuth_deg?.IsInitializedAndNotObsolete == true)
                    beaconInfo.AbsoluteAzimuth = SafeRound(beacon.AAzimuth_deg.Value, 1);

                if (beacon.RAzimuth_deg?.IsInitializedAndNotObsolete == true)
                    beaconInfo.ReverseAzimuth = SafeRound(beacon.RAzimuth_deg.Value, 1);

                // Декартовы координаты
                if (beacon.X_m?.IsInitializedAndNotObsolete == true)
                    beaconInfo.X = SafeRound(beacon.X_m.Value, 2);

                if (beacon.Y_m?.IsInitializedAndNotObsolete == true)
                    beaconInfo.Y = SafeRound(beacon.Y_m.Value, 2);

                if (beacon.Z_m?.IsInitializedAndNotObsolete == true)
                    beaconInfo.Z = SafeRound(beacon.Z_m.Value, 2);

                // Географические координаты
                if (beacon.Lat_deg?.IsInitializedAndNotObsolete == true)
                    beaconInfo.Latitude = SafeRound(beacon.Lat_deg.Value, 6);

                if (beacon.Lon_deg?.IsInitializedAndNotObsolete == true)
                    beaconInfo.Longitude = SafeRound(beacon.Lon_deg.Value, 6);

                // Дополнительные параметры
                if (beacon.Depth_m?.IsInitializedAndNotObsolete == true)
                    beaconInfo.Depth = SafeRound(beacon.Depth_m.Value, 1);

                if (beacon.MSR_dB?.IsInitializedAndNotObsolete == true)
                    beaconInfo.SignalLevel = SafeRound(beacon.MSR_dB.Value, 1);

                if (beacon.VCC_V?.IsInitializedAndNotObsolete == true)
                    beaconInfo.Battery = SafeRound(beacon.VCC_V.Value, 2);

                if (beacon.WaterTemp_C?.IsInitializedAndNotObsolete == true)
                    beaconInfo.Temperature = SafeRound(beacon.WaterTemp_C.Value, 1);

                if (beacon.PTime_s?.IsInitializedAndNotObsolete == true)
                    beaconInfo.PropagationTime = SafeRound(beacon.PTime_s.Value, 4);

                // Статистика
                if (beacon.TotalRequests > 0)
                {
                    beaconInfo.SuccessRate = SafeRound(100.0 * beacon.SuccededRequests / beacon.TotalRequests, 1);
                    beaconInfo.TotalRequests = beacon.TotalRequests;
                }

                if (beacon.Message?.IsInitializedAndNotObsolete == true)
                    beaconInfo.Message = beacon.Message.Value;

                // Тип координат
                beaconInfo.CoordinateType = DetermineCoordinateType(beacon);

                // Возраст данных
                beaconInfo.DataAge = GetMaxAge(beacon);

                // Добавляем только если есть координаты
                if (beaconInfo.X != null || beaconInfo.Y != null ||
                    beaconInfo.Latitude != null || beaconInfo.Longitude != null ||
                    beaconInfo.Distance != null)
                {
                    data.Beacons.Add(beaconInfo);
                }
            }
        }

        private void ConvertSystemInfo(WebUIData data)
        {
            data.SystemInfo = new SystemInfo
            {
                DeviceType = _combiner.DeviceType.ToString(),
                SerialNumber = _combiner.DeviceSerialNumber ?? "",
                Version = _combiner.DeviceVersionInfo ?? "",
                ConnectionActive = _combiner.ConnectionActive,
                InterrogationActive = _combiner.InterrogationActive,
                AzimuthDetected = _combiner.AZMDetected,
                Aux1Detected = _combiner.AUX1Detected,
                Aux2Detected = _combiner.AUX2Detected,
                LocationOverride = _combiner.LocationOverrideEnabled
            };
        }

        private double? SafeRound(double? value, int decimals)
        {
            if (!value.HasValue) return null;
            if (double.IsNaN(value.Value) || double.IsInfinity(value.Value)) return null;
            return Math.Round(value.Value, decimals);
        }

        private double? GetMinAge(AZMTranscieverState state)
        {
            var ages = new List<double>();

            AddAgeIfValid(ages, state.Lat_deg);
            AddAgeIfValid(ages, state.Lon_deg);
            AddAgeIfValid(ages, state.Heading_deg);
            AddAgeIfValid(ages, state.StDepth_m);
            AddAgeIfValid(ages, state.WaterTemp_C);

            return ages.Count > 0 ? Math.Round(ages.Min(), 1) : (double?)null;
        }

        private double? GetMaxAge(ResponderBeacon beacon)
        {
            var ages = new List<double>();

            AddAgeIfValid(ages, beacon.SRangeProjection_m);
            AddAgeIfValid(ages, beacon.Azimuth_deg);
            AddAgeIfValid(ages, beacon.Lat_deg);
            AddAgeIfValid(ages, beacon.Lon_deg);
            AddAgeIfValid(ages, beacon.Depth_m);
            AddAgeIfValid(ages, beacon.MSR_dB);
            AddAgeIfValid(ages, beacon.VCC_V);

            return ages.Count > 0 ? Math.Round(ages.Min(), 1) : (double?)null;
        }

        private void AddAgeIfValid(List<double> ages, AgingValue<double> value)
        {
            if (value != null && value.IsInitialized && !value.IgnoreAge)
            {
                ages.Add(value.Age.TotalSeconds);
            }
        }

        private string DetermineCoordinateType(ResponderBeacon beacon)
        {
            if (beacon.Lat_deg?.IsInitializedAndNotObsolete == true &&
                beacon.Lon_deg?.IsInitializedAndNotObsolete == true)
                return "geographic";

            if (beacon.X_m?.IsInitializedAndNotObsolete == true &&
                beacon.Y_m?.IsInitializedAndNotObsolete == true)
                return "cartesian";

            if (beacon.Azimuth_deg?.IsInitializedAndNotObsolete == true &&
                beacon.SRangeProjection_m?.IsInitializedAndNotObsolete == true)
                return "polar";

            return "unknown";
        }

        private string DetermineLogType(string line)
        {
            if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase)) return "error";
            if (line.Contains("WARN", StringComparison.OrdinalIgnoreCase)) return "warning";
            if (line.Contains("FAIL", StringComparison.OrdinalIgnoreCase)) return "error";
            return "info";
        }
    }

    /// <summary>
    /// Конвертер для безопасной сериализации double значений
    /// </summary>
    public class SafeDoubleJsonConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.GetDouble();
        }

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteNumberValue(value);
            }
        }
    }
}
