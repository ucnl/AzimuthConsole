using AZMLib;
using System.Globalization;
using UCNLDrivers;

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

    private readonly uRadantPort _rotator;
    private readonly AZMCombiner _combiner;
    private readonly Action<string> _log;

    private CalibrationState _state = CalibrationState.Idle;
    public CalibrationState State => _state;

    public double StepAngle_deg 
    {
        get { return _stepAngle_deg; }
        private set
        {
            _stepAngle_deg = value;
        }
    }

    private double _stepAngle_deg = 15;
    private int _measurementsPerPoint = 10;

    private double _currentTargetAngle_deg;
    private int _currentMeasurementCount;

    public double CurrentRotatorAngle => _rotator.CurrentAngle;

    private readonly List<CalibrationDataPoint> _rawData = new();
    private readonly List<(double EncoderAngle, double MeasuredAzimuth)> _calibrationPairs = new();

    public CalibrationManager(uRadantPort rotator, AZMCombiner combiner, Action<string> log, string basePath)
    {
        _rotator = rotator;
        _combiner = combiner;
        _log = log;

        _combiner.USBLRawDataHandler += OnUSBLRawData;
        _combiner.USBLRawDataEventEnabled = true;
        _rotator.WaitingToFinishRotationChangedEventHandler += OnWaitingChanged;

        _calDataPath = Path.Combine(basePath, "caldata");
        Directory.CreateDirectory(_calDataPath);
    }

    public IReadOnlyList<(double EncoderAngle, double MeasuredAzimuth)> CalibrationPairs => _calibrationPairs;

    public void Start(double startAngle, double stepAngle, int measurementsPerPoint)
    {
        if (_state != CalibrationState.Idle)
        {
            _log("Calibration already in progress");
            return;
        }

        if (!_rotator.IsActive)
        {
            _log("Antenna rotator is not connected");
            _state = CalibrationState.Failed;
            return;
        }

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
        if (_combiner.InterrogationActive)
            _combiner.PauseInterrogation();

        _state = CalibrationState.Idle;
        _rotator.RequestStop();
        _log("Calibration stopped");
    }

    private void MoveToAngle(double angle)
    {
        if (!_rotator.IsActive)
        {
            _log("Antenna rotator is not connected");
            _state = CalibrationState.Failed;
            return;
        }

        if (_combiner.InterrogationActive)
            _combiner.PauseInterrogation();

        _combiner.LocationOverrideUpdateHeading(angle);

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
        if (!_rotator.IsActive && _state == CalibrationState.Moving)
        {
            _log("Antenna rotator disconnected during rotation");
            _state = CalibrationState.Failed;
            return;
        }


        if (!_rotator.WaitingToFinishRotation && _state == CalibrationState.Moving)
        {
            if (Math.Abs(_rotator.CurrentAngle - _currentTargetAngle_deg) < 1.0)
            {
                if (!_combiner.InterrogationActive)
                    _combiner.ResumeInterrogation();

                _state = CalibrationState.Measuring;
                _currentMeasurementCount = 0;
                _log($"Measuring at {_currentTargetAngle_deg:F1}° ({_measurementsPerPoint} samples)");
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
            var pointsForAngle = _rawData
                .Where(p => Math.Abs(p.TargetAngle_deg - _currentTargetAngle_deg) < 0.1)
                .ToList();

            if (pointsForAngle.Count > 0)
            {
                double avgHAngle = pointsForAngle.Average(p => p.HAngle_deg);
                _calibrationPairs.Add((_currentTargetAngle_deg, avgHAngle));
                _log($"Angle {_currentTargetAngle_deg:F1}° → avg azimuth = {avgHAngle:F2}°");
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
        _log($"Calibration completed. {_calibrationPairs.Count} points collected.");
        SaveCalibrationData();
    }

    private void SaveCalibrationData()
    {
        try
        {
            string fileName = Path.Combine(_calDataPath, $"cal_raw_{_combiner.DeviceSerialNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
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