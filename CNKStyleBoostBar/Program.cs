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

    [CliOption(Description = $"When the boost bar reaches this percent of the maximum, it will change to {nameof(BoostMeterColor3)}.")]
    public float ThresholdPercentForColor3 { get; set; } = 80f;

    [CliOption(Description = $"When the boost bar reaches this percent of the maximum, it will change to {nameof(BoostMeterColor4)}.")]
    public float ThresholdPercentForColor4 { get; set; } = 95f;

    [CliOption(Description = "The arc style bars are drawn by sweeping from one angle to another. This sets the start angle in degrees.")]
    public float ArcStartAngle { get; set; } = -30f;

    [CliOption(Description = "The arc style bars are drawn by sweeping from one angle to another. This sets the end angle in degrees.")]
    public float ArcEndAngle { get; set; } = 45f;

    [CliOption(Description = "The number of pixels by which to offset the boost bars in the X direction. Flips automatically when mirrored.")]
    public float OffsetX { get; set; } = 0f;

    [CliOption(Description = "The number of pixels by which to offset the boost bars in the Y direction. Negative is up.")]
    public float OffsetY { get; set; } = 0f;

    public void Run()
    {
        // Create and configure the application
        var meterApp = new CNKStyleBoostMeter(MirrorMeter, ArcStartAngle, ArcEndAngle)
        {
            DrawDebugBox = DrawDebugBox,
            ConfigBoostBarStyle = BoostBarStyle,
            BoostMeterColor1 = BoostMeterColor1,
            BoostMeterColor2 = BoostMeterColor2,
            BoostMeterColor3 = BoostMeterColor3,
            BoostMeterColor4 = BoostMeterColor4,
            ThresholdPercentForColor3 = ThresholdPercentForColor3 / 100f,
            ThresholdPercentForColor4 = ThresholdPercentForColor4 / 100f,
            OffsetX = OffsetX,
            OffsetY = OffsetY
        };

        // Run the overlay application until the user presses Enter in the console window
        meterApp.StartCaptureAndOverlay();
        Console.ReadLine();
        meterApp.StopCaptureAndOverlay();
    }
}
