using DotMake.CommandLine;

namespace CNKStyleBoostBar;
internal class Program
{
    private static void Main(string[] args)
    {
        Cli.Run<RootCommand>(args);
    }
}

[CliCommand]
internal class RootCommand
{
    public void Run()
    {
        var meterApp = new CNKStyleBoostMeter();
        meterApp.StartCaptureAndOverlay();
        Console.ReadLine();
        meterApp.StopCaptureAndOverlay();
    }
}
