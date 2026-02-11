using UCNLNav;

namespace AzimuthConsole.AZM
{
    public readonly struct TimedTOABasePoint
    {
        public REMOTE_ADDR_Enum Id { get; }
        public TOABasePoint Point { get; }
        public DateTime Timestamp { get; }

        public TimedTOABasePoint(REMOTE_ADDR_Enum id, TOABasePoint point, DateTime timestamp)
        {
            Id = id;
            Point = point;
            Timestamp = timestamp;
        }

        public TimedTOABasePoint(REMOTE_ADDR_Enum id, double x, double y, double z, double d, DateTime timestamp)
        : this(id, new TOABasePoint { X = x, Y = y, Z = z, D = d }, timestamp)
        {            
        }

        public TimeSpan Age => DateTime.UtcNow - Timestamp;
    }

    public class LBLProcessor
    {
        private readonly Dictionary<REMOTE_ADDR_Enum, TimedTOABasePoint> _points = new();
        private TimeSpan _maxAllowedAge = TimeSpan.FromSeconds(10);

        public void UpdatePoint(REMOTE_ADDR_Enum id, TOABasePoint point)
        {
            var now = DateTime.UtcNow;
            _points[id] = new TimedTOABasePoint(id, point, now);
        }

        public void UpdatePoint(REMOTE_ADDR_Enum id, double x, double y, double z, double d)
        {
            _points[id] = new TimedTOABasePoint(id, x, y, z, d, DateTime.UtcNow);
        }

        public IEnumerable<TimedTOABasePoint> GetAllPoints() => _points.Values;

        public IEnumerable<TOABasePoint> GetValidPointsForSolver()
        {
            return _points
                .Where(kvp => kvp.Value.Age <= _maxAllowedAge)
                .Select(kvp => kvp.Value.Point);
        }

        public bool CanFormNavigationBase(int minPoints = 3)
        {
            return _points.Count(kvp => kvp.Value.Age <= _maxAllowedAge) >= minPoints;
        }

        public bool RemovePoint(REMOTE_ADDR_Enum id) => _points.Remove(id);

        public void Clear() => _points.Clear();

        public void SetMaxAge(TimeSpan maxAge) => _maxAllowedAge = maxAge;
    }
}
