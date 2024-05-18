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

    [CliOption(Description = "Changes the shape of the boost meters drawn on screen.")]
    public BoostBarStyle BoostBarStyle { get; set; } = BoostBarStyle.ArcsSameAngles;

    [CliOption(Description = "The boost meter color when the minimum amount for a boost hasn't been reached yet (RGB hex).")]
    public string BoostMeterColor1 { get; set; } = "00FF00";

    [CliOption(Description = "The boost meter color when the minimum amount for a boost has been reached (RGB hex).")]
    public string BoostMeterColor2 { get; set; } = "FFD800";

    [CliOption(Description = "The boost meter color when the first threshold within the valid boost window has been reached (RGB hex).")]
    public string BoostMeterColor3 { get; set; } = "FF6A00";

    [CliOption(Description = "The boost meter color when the second threshold within the valid boost window has been reached (RGB hex).")]
    public string BoostMeterColor4 { get; set; } = "FF0000";

    public void Run()
    {
        // Create and configure the application
        var meterApp = new CNKStyleBoostMeter(MirrorMeter)
        {
            DrawDebugBox = DrawDebugBox,
            ConfigBoostBarStyle = BoostBarStyle,
            BoostMeterColor1 = BoostMeterColor1,
            BoostMeterColor2 = BoostMeterColor2,
            BoostMeterColor3 = BoostMeterColor3,
            BoostMeterColor4 = BoostMeterColor4
        };

        // Run the overlay application until the user presses Enter in the console window
        meterApp.StartCaptureAndOverlay();
        Console.ReadLine();
        meterApp.StopCaptureAndOverlay();
    }
}
