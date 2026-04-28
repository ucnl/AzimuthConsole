
using AzimuthConsole;

internal class Program
{
    public enum ConsoleLogOptions
    {
        Enabled,
        Errors_only,
        Disabled,
        Invalid
    }    

    private static async Task Main(string[] args)
    {
        var isDaemonMode = args.Contains("daemon") || args.Contains("DAEMON");

        if (isDaemonMode)
        {
            Console.SetOut(StreamWriter.Null);
            Console.SetError(StreamWriter.Null);
        }

        var app = new ApplicationRuntime();

        try
        {
            app.Initialize(args);

            if (isDaemonMode)
                await app.RunDaemonMode();
            else
                await app.RunInteractiveMode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex}");
        }
        finally
        {
            app.Dispose();
        }
    }
}
