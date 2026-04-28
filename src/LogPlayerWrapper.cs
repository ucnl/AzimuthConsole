using UCNLDrivers;

namespace AzimuthConsole;

internal class LogPlayerWrapper : ILogPlayer
{
    private readonly LogPlayer _player = new();

    public event EventHandler<StringEventArgs>? NewLineHandler;
    public event EventHandler? PlaybackFinishedHandler;

    public LogPlayerWrapper()
    {
        _player.NewLogLineHandler += (o, e) =>
        {
            if (e.Line.StartsWith("INFO"))
            {
                int idx = e.Line.IndexOf(' ');
                if (idx >= 0)
                {
                    NewLineHandler?.Invoke(this, new StringEventArgs(e.Line.Substring(idx).Trim()));
                }
            }
        };
        
        _player.LogPlaybackFinishedHandler += (o, e) => PlaybackFinishedHandler?.Invoke(this, EventArgs.Empty);
    }

    public bool StartLogPlayBack(bool isInstant, string fileName)
    {
        if (_player.IsRunning || !File.Exists(fileName))
            return false;

        try
        {
            if (isInstant)
                _player.PlaybackInstant(fileName);
            else
                _player.Playback(fileName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool StopLogPlayBack()
    {
        if (_player.IsRunning)
        {
            _player.RequestToStop();
            return true;
        }
        return false;
    }
}