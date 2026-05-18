// Program.cs
using AzimuthConsole;

internal class Program
{
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
            await app.InitializeAsync(args);
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex}");
        }
        finally
        {
            await app.DisposeAsync();
        }
    }
}