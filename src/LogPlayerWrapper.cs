using AzimuthConsole.AZM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UCNLDrivers;

namespace AzimuthConsole
{
    internal class LogPlayerWrapper
    {
        LogPlayer player = new LogPlayer();

        public LogPlayerWrapper()
        {
            player.NewLogLineHandler += (o, e) =>
            {                
                if (e.Line.StartsWith("INFO"))
                {
                    int idx = e.Line.IndexOf(' ');
                    if (idx >= 0)
                    {
                        NewLineHandler?.Invoke(player, new StringEventArgs(e.Line.Substring(idx).Trim()));
                    }
                }
            };

        }

        public bool StartLogPlayBack(bool isInstant, string fileName)
        {
            if (player.IsRunning || !File.Exists(fileName))
                return false;
            else
            {
                bool ok = false;

                try
                {
                    if (isInstant)
                        player.PlaybackInstant(fileName);
                    else
                        player.Playback(fileName);
                    ok = true;
                }
                catch (Exception)
                {

                }

                return ok;
            }
        }
        public bool StopLogPlayBack()
        {
            if (player.IsRunning)
            {
                player.RequestToStop();
                return true;
            }
            else
                return false;
        }

        public EventHandler<StringEventArgs>? NewLineHandler;
        public EventHandler? PlaybackFinishedHandler;

    }
}
