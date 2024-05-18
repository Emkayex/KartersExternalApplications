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

    [CliOption(Description = "Draws a debug box around the original boost meter.")]
    public bool DrawDebugBox { get; set; } = false;

    [CliOption]
    public BoostBarStyle BoostBarStyle { get; set; } = BoostBarStyle.ArcsSameAngles;

    public void Run()
    {
        var meterApp = new CNKStyleBoostMeter(MirrorMeter)
        {
            DrawDebugBox = DrawDebugBox,
            ConfigBoostBarStyle = BoostBarStyle
        };
        meterApp.StartCaptureAndOverlay();
        Console.ReadLine();
        meterApp.StopCaptureAndOverlay();
    }
}
