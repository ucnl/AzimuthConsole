// AzimuthConsole/CalibrationManager.cs
using System.Globalization;
using AZMLib;
using UCNLDrivers.uAux;

namespace AzimuthConsole;

public enum CalibrationState
{
    Idle,
    Moving,
    Measuring,
    Completed,
    Failed
}

public class CalibrationDataPoint
{
    public double TargetAngle_deg { get; set; }
    public double HAngle_deg { get; set; }
    public double VAngle_deg { get; set; }
    public double PTime_s { get; set; }
    public double SlantRange_m { get; set; }
    public double SlantRangeProjection_m { get; set; }
    public double StationDepth_m { get; set; }
    public double MSR_dB { get; set; }
}

public class CalibrationManager
{
    private readonly string _calDataPath;
    private readonly uAuxRadantPort _rotator;
    private readonly AZMManager _azmManager;
    private readonly Action<string> _log;

    private CalibrationState _state = CalibrationState.Idle;
    public CalibrationState State => _state;

    private double _stepAngle_deg = 15;
    public double StepAngle_deg => _stepAngle_deg;

    private int _measurementsPerPoint = 10;

    private double _currentTargetAngle_deg;
    private int _currentMeasurementCount;

    public double CurrentRotatorAngle => _rotator.CurrentAngle;
    public int CollectedPoints => _calibrationPairs.Count;
    public int TotalPoints => (int)(360.0 / _stepAngle_deg);


    private readonly double _stationOvLat;
    private readonly double _stationOvLon;


    private readonly List<CalibrationDataPoint> _rawData = new();
    private readonly List<(double EncoderAngle, double MeasuredAzimuth)> _calibrationPairs = new();

    public IReadOnlyList<(double EncoderAngle, double MeasuredAzimuth)> CalibrationPairs => _calibrationPairs;

    public CalibrationManager(uAuxRadantPort rotator, AZMManager azmManager,
        Action<string> log, string basePath, double stOvLat = double.NaN, double stOvLon = double.NaN)
    {
        _rotator = rotator ?? throw new ArgumentNullException(nameof(rotator));
        _azmManager = azmManager ?? throw new ArgumentNullException(nameof(azmManager));
        _log = log;

        _stationOvLat = stOvLat;
        _stationOvLon = stOvLon;

        _azmManager.USBLRawDataHandler += OnUSBLRawData;
        _azmManager.USBLRawDataEventEnabled = true;
        _rotator.WaitingToFinishRotationChanged += OnWaitingChanged;

        _calDataPath = Path.Combine(basePath, "caldata");
        Directory.CreateDirectory(_calDataPath);
    }

    public void Start(double startAngle, double stepAngle, int measurementsPerPoint)
    {
        if (_state != CalibrationState.Idle)
        {
            _log("Calibration already in progress");
            return;
        }

        if (_rotator.Status != AuxStatus.Detected)
        {
            _log("Antenna rotator is not connected");
            _state = CalibrationState.Failed;
            return;
        }

        // Останавливаем опрос перед началом калибровки
        _azmManager.PauseInterrogation();
        _log("Interrogation paused for calibration");

        _stepAngle_deg = stepAngle;
        _measurementsPerPoint = measurementsPerPoint;
        _rawData.Clear();
        _calibrationPairs.Clear();

        _log($"Starting calibration from {startAngle:F1}° with step {stepAngle:F1}°");
        MoveToAngle(startAngle);
    }

    public void Start(double stepAngle, int measurementsPerPoint)
    {
        Start(0.0, stepAngle, measurementsPerPoint);
    }

    public void Stop()
    {
        _state = CalibrationState.Idle;
        _rotator.RequestStop();

        // Отключаем LocationOverride
        _azmManager.DisableLocationOverride();

        // Возобновляем опрос
        if (!_azmManager.InterrogationActive)
        {
            _azmManager.ResumeInterrogation();
            _log("Interrogation resumed");
        }

        _log("Calibration stopped");
    }

    private void MoveToAngle(double angle)
    {
        if (_rotator.Status != AuxStatus.Detected)
        {
            _log("Antenna rotator is not connected");
            _state = CalibrationState.Failed;
            return;
        }

        _state = CalibrationState.Moving;
        _currentTargetAngle_deg = angle;
        _log($"Moving to {angle:F1}°");

        if (!_rotator.RequestSetAngle(angle))
        {
            _log($"Failed to set angle {angle:F1}°");
            _state = CalibrationState.Failed;
        }
    }

    private void OnWaitingChanged(object? sender, EventArgs e)
    {
        if (_state != CalibrationState.Moving) return;

        if (_rotator.Status != AuxStatus.Detected)
        {
            _log("Antenna rotator disconnected during rotation");
            _state = CalibrationState.Failed;
            return;
        }

        if (!_rotator.WaitingToFinishRotation)
        {
            if (Math.Abs(_rotator.CurrentAngle - _currentTargetAngle_deg) < 1.0)
            {
                _state = CalibrationState.Measuring;
                _currentMeasurementCount = 0;

                // Устанавливаем LocationOverride: координаты пирса + угол энкодера как heading
                _azmManager.OverrideLocation(_stationOvLat, _stationOvLon, _rotator.CurrentAngle);

                // Запускаем опрос для сбора измерений
                _azmManager.ResumeInterrogation();

                string locInfo = (!double.IsNaN(_stationOvLat) && !double.IsNaN(_stationOvLon))
                ? $"at {_stationOvLat:F6}°, {_stationOvLon:F6}°"
                : "in relative mode";

                _log($"Measuring at {_currentTargetAngle_deg:F1}° " +
                     $"(encoder: {_rotator.CurrentAngle:F1}°, {locInfo}) " +
                     $"- {_measurementsPerPoint} samples");
            }
            else
            {
                _log($"Failed to reach target angle. Current={_rotator.CurrentAngle:F1}°, Target={_currentTargetAngle_deg:F1}°");
                _state = CalibrationState.Failed;
            }
        }
    }

    private void OnUSBLRawData(object? sender, USBLRawDataEventArgs e)
    {
        if (_state != CalibrationState.Measuring) return;

        _rawData.Add(new CalibrationDataPoint
        {
            TargetAngle_deg = _currentTargetAngle_deg,
            HAngle_deg = e.HAngle_deg,
            VAngle_deg = e.VAngle_deg,
            PTime_s = e.PTime_s,
            SlantRange_m = e.SlantRange_m,
            SlantRangeProjection_m = e.SlantRangeProjection_m,
            StationDepth_m = e.StationDepth_m,
            MSR_dB = e.MSR_dB
        });

        _currentMeasurementCount++;

        if (_currentMeasurementCount >= _measurementsPerPoint)
        {
            // Останавливаем опрос на время поворота
            _azmManager.PauseInterrogation();

            var pointsForAngle = _rawData
                .Where(p => Math.Abs(p.TargetAngle_deg - _currentTargetAngle_deg) < 0.1)
                .ToList();

            if (pointsForAngle.Count > 0)
            {
                double avgHAngle = pointsForAngle.Average(p => p.HAngle_deg);
                // Сохраняем реальный угол энкодера, а не целевой
                _calibrationPairs.Add((_rotator.CurrentAngle, avgHAngle));
                _log($"Encoder {_rotator.CurrentAngle:F1}° → avg azimuth = {avgHAngle:F2}°");
            }

            double nextAngle = _currentTargetAngle_deg + _stepAngle_deg;
            if (nextAngle >= 360.0 - _stepAngle_deg / 2.0)
            {
                Complete();
            }
            else
            {
                MoveToAngle(nextAngle);
            }
        }
    }

    private void Complete()
    {
        _state = CalibrationState.Completed;

        // Отключаем LocationOverride
        _azmManager.DisableLocationOverride();

        // Возобновляем опрос в обычном режиме
        if (!_azmManager.InterrogationActive)
        {
            _azmManager.ResumeInterrogation();
            _log("Interrogation resumed");
        }

        _log($"Calibration completed. {_calibrationPairs.Count} points collected.");
        SaveCalibrationData();
    }

    private void SaveCalibrationData()
    {
        try
        {
            string fileName = Path.Combine(_calDataPath,
                $"cal_raw_{_azmManager.DeviceSerialNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            using var writer = new StreamWriter(fileName);
            writer.WriteLine("EncoderAngle_deg,MeasuredAzimuth_deg");
            foreach (var (enc, azm) in _calibrationPairs.OrderBy(p => p.EncoderAngle))
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:F3},{1:F3}", enc, azm));
            }
            _log($"Raw calibration data saved to {fileName}");
        }
        catch (Exception ex)
        {
            _log($"Failed to save calibration data: {ex.Message}");
        }
    }
}