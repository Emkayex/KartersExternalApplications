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
    [CliOption(Description = "Mirrors the boost meter across the screen.")]
    public bool MirrorMeter { get; set; } = false;

    public void Run()
    {
        var meterApp = new CNKStyleBoostMeter
        {
            MirrorBoostMeter = MirrorMeter
        };
        meterApp.StartCaptureAndOverlay();
        Console.ReadLine();
        meterApp.StopCaptureAndOverlay();
    }
}
