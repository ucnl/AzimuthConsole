using UCNLDrivers;

namespace AzimuthConsole;

public interface ILogPlayer
{
    bool StartLogPlayBack(bool isInstant, string fileName);
    bool StopLogPlayBack();
    event EventHandler<StringEventArgs>? NewLineHandler;
    event EventHandler? PlaybackFinishedHandler;
}