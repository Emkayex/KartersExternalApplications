using System.Diagnostics;
using GameOverlay.Drawing;
using GameOverlay.Windows;
using WindowCapture;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace CNKStyleBoostBar;
public class CNKStyleBoostMeter
{
    public const string DefaultWindowName = "TheKarters2";
    public const float MinValueForBoost = 0.5f;
    public const float MaxValueForBoost = 1.0f;

    public readonly DisplayInformation DisplayInfo;

    public BoostBarStyle ConfigBoostBarStyle { get; set; } = BoostBarStyle.ArcsSameAngles;
    public string BoostMeterColor1 { get; set; } = "00FF00";
    public string BoostMeterColor2 { get; set; } = "FFD800";
    public string BoostMeterColor3 { get; set; } = "FF6A00";
    public string BoostMeterColor4 { get; set; } = "FF0000";

    public bool DrawDebugBox { get; set; } = false;

    public float ThresholdPercentForColor3 { get; set; } = 0.80f;
    public float ThresholdPercentForColor4 { get; set; } = 0.95f;

    public float OffsetX { get; set; } = 0f;
    public float OffsetY { get; set; } = 0f;

    private readonly HashSet<int> CustomBrushCreationRequests = [];
    private readonly Dictionary<int, SolidBrush> CustomBrushes = [];

    private GraphicsWindow? Window;
    private BrushCollection? Brushes = null;

    private FrameCapture? Capturer;
    private readonly BoostMeterData MeterData;

    private string WindowName = DefaultWindowName;

    private float DebugLeft;
    private float DebugRight;
    private float DebugTop;
    private float DebugBottom;

    private int FrameCount = 0;

    public CNKStyleBoostMeter(bool mirrorBoostMeter, float arcStartAngle, float arcEndAngle)
    {
        // Create the DisplayInformation object with placeholder values that will be overwritten once the first frame is captured
        DisplayInfo = new()
        {
            SystemWidth = 0,
            SystemHeight = 0,
            RenderWidth = 0,
            RenderHeight = 0
        };

        // Create the object used to track boost meter data
        MeterData = new(GetBoostMeterColor, () => Brushes, () => DisplayInfo)
        {
            ArcStartAngle = arcStartAngle,
            ArcEndAngle = arcEndAngle
        };

        if (mirrorBoostMeter)
        {
            MeterData.DriftDirection *= -1;
        }
    }

    public void StartCaptureAndOverlay(string windowName = DefaultWindowName)
    {
        // Start the frame capturer that also records current boost values
        Capturer = new();
        Capturer.BoostPercentagesReady += OnBoostPercentagesReady;
        Capturer.StartCapture(windowName);
        WindowName = windowName;

        // Wait until one frame is captured as indicated by the DisplayInfo width/height not being 0 (only one needs to be checked)
        while (DisplayInfo.SystemWidth <= 0)
        {
            Thread.Sleep(1);
        }

        Window = new GraphicsWindow(0, 0, DisplayInfo.SystemWidth, DisplayInfo.SystemHeight)
        {
            FPS = 60,
            IsTopmost = true,
            IsVisible = true
        };

        // Set the event callbacks
        #pragma warning disable CS8622
        Window.SetupGraphics += Window_SetupGraphics;
        Window.DrawGraphics += Window_DrawGraphics;
        Window.DestroyGraphics += Window_DestroyGraphics;
        #pragma warning restore CS8622

        Window.Create();
    }

    public void StopCaptureAndOverlay()
    {
        Capturer?.StopCapture();
        Window?.Dispose();
        Window?.Join();

        Capturer = null;
        Window = null;
        DisplayInfo.SystemWidth = DisplayInfo.SystemHeight = DisplayInfo.RenderWidth = DisplayInfo.RenderHeight = 0;
    }

    private void FitOverlayToWindow()
    {
        // Make sure the overlay window lines up with the same offset and dimensions as the game window
        if ((Window is not null) && OperatingSystem.IsWindowsVersionAtLeast(5))
        {
            var procs = Process.GetProcessesByName(WindowName);
            if (procs.Length > 0)
            {
                // There should only be one instance of the game window open, so select the first one and get a handle
                var hwnd = (HWND)procs[0].MainWindowHandle;
                PInvoke.GetWindowRect(hwnd, out var rect);

                Window.X = rect.X;
                Window.Y = rect.Y;
                Window.Width = rect.Width;
                Window.Height = rect.Height;
            }
        }
    }

    private void OnBoostPercentagesReady(object? sender, BoostPercentagesReadyEventArgs e)
    {
        // Update the most recent height and width of the window
        DisplayInfo.RenderWidth = DisplayInfo.SystemWidth = e.FrameWidth;
        DisplayInfo.RenderHeight = DisplayInfo.SystemHeight = e.FrameHeight;

        // Get the boost meter amounts from the capturer
        MeterData.BoostAmounts[0] = e.Boost1;
        MeterData.BoostAmounts[1] = e.Boost2;
        MeterData.BoostAmounts[2] = e.Boost3;
    }

    private SolidBrush GetBoostMeterColor(float boostBarValue)
    {
        // Get the hex code for the brush color
        var brushHex = BoostMeterColor1;
        if (boostBarValue >= MinValueForBoost)
        {
            brushHex = BoostMeterColor2;
        }
        if (boostBarValue >= (MaxValueForBoost * ThresholdPercentForColor3))
        {
            brushHex = BoostMeterColor3;
        }
        if (boostBarValue >= (MaxValueForBoost * ThresholdPercentForColor4))
        {
            brushHex = BoostMeterColor4;
        }

        // Convert the hex code to an integer to perform a lookup
        var brushInt = Convert.ToInt32(brushHex, 16);
        if (!CustomBrushes.TryGetValue(brushInt, out var brush))
        {
            // If the value isn't present in the dictionary, submit a request to overlay render loop to create a new brush
            // Attempting to use a Graphics object from here will crash the game
            lock (CustomBrushCreationRequests)
            {
                CustomBrushCreationRequests.Add(brushInt);
            }

            // Since the necessary brush isn't available yet, just return a default black brush
            brush = Brushes!.Black;
        }

        return brush;
    }

    private void Window_SetupGraphics(object sender, SetupGraphicsEventArgs e)
    {
        // Configure the collections used to draw on the screen
        var gfx = e.Graphics;
        if (e.RecreateResources)
        {
            Brushes?.Dispose();
        }

        Brushes ??= new BrushCollection(gfx);
    }

    private void Window_DrawGraphics(object sender, DrawGraphicsEventArgs e)
    {
        // Clear the screen before drawing any graphics
        var gfx = e.Graphics;
        gfx.ClearScene();

        // Make sure the overlay window is always over the game window
        FrameCount++;
        if (FrameCount >= 60)
        {
            FitOverlayToWindow();
            FrameCount = 0;
        }

        // Get the bounds of the screen containing the boost meters for the debug drawing
        DebugLeft = Capturer!.LeftSearchBound;
        DebugTop = Capturer.TopSearchBound;
        DebugRight = Capturer.RightSearchBound;
        DebugBottom = Capturer.BottomSearchBound;

        // Handle any brush creation requests
        if (CustomBrushCreationRequests.Count > 0)
        {
            lock (CustomBrushCreationRequests)
            {
                foreach (var brushInt in CustomBrushCreationRequests)
                {
                    // Parse the RGB values from the brush integer
                    var r = (brushInt >> (8 * 2)) & 0xFF;
                    var g = (brushInt >> 8) & 0xFF;
                    var b = brushInt & 0xFF;

                    var newBrush = gfx.CreateSolidBrush(r, g, b);
                    CustomBrushes.TryAdd(brushInt, newBrush);
                }

                // Clear out the brush creation requests set
                CustomBrushCreationRequests.Clear();
            }
        }

        // Only render the text if the brushes and fonts are available
        if (Brushes is not null)
        {
            // foreach (var (_, meterData) in PlayerBoostMeterData)
            // {
            //     gfx.DrawCrosshair(Brushes.Blue, new(meterData.DrawX, meterData.DrawY), 25f, 5f, CrosshairStyle.Plus);
            // }

            // If the debug option is enabled, draw a box around the original boost meters to see where image data is being read
            if (DrawDebugBox)
            {
                gfx.DrawRectangle(Brushes.Yellow, DebugLeft, DebugTop, DebugRight, DebugBottom, 3f);
            }

            // Draw the current boost amount on the screen
            var (boostNum, boostValue) = MeterData.GetBoostNumberAndValue();
            if ((boostNum is not null) && (boostValue is not null))
            {
                var adjOffsetX = OffsetX * MeterData.DriftDirection;
                for (var drawBoostNum = 0; drawBoostNum <= 2; drawBoostNum++)
                {
                    MeterData.DrawBoostBar(
                        gfx: gfx,
                        style: ConfigBoostBarStyle,
                        boostNum: drawBoostNum,
                        baseX: (DisplayInfo.RenderWidth / 2) + adjOffsetX,
                        baseY: DisplayInfo.RenderHeight / 2 + OffsetY
                    );
                }
            }
        }
    }

    private void Window_DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
    {
        Brushes?.Dispose();
        Capturer?.Dispose();
    }
}
