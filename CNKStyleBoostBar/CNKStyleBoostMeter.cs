using GameOverlay.Drawing;
using GameOverlay.Windows;
using WindowCapture;
using WindowCapture.window_capture;

using PixelColor = (byte r, byte g, byte b, byte a);

namespace CNKStyleBoostBar;
public class CNKStyleBoostMeter
{
    public const float MinValueForBoost = 0.5f;
    public const float MaxValueForBoost = 1.0f;

    private static readonly PixelColor GrayTuple = (0x5F, 0x5E, 0x5F, 0xFF);
    private static readonly PixelColor RedTuple = (0xF5, 0x00, 0x00, 0xFF);
    private static readonly double[] AreaSamplePercentages = [0.1, 0.5, 0.9];

    public readonly DisplayInformation DisplayInfo;

    public BoostBarStyle ConfigBoostBarStyle { get; set; } = BoostBarStyle.ArcsSameAngles;
    public string BoostMeterColor1 { get; set; } = "00FF00";
    public string BoostMeterColor2 { get; set; } = "FFD800";
    public string BoostMeterColor3 { get; set; } = "FF6A00";
    public string BoostMeterColor4 { get; set; } = "FF0000";

    public float ArcStyleStartAngle { get; set; } = -30f;
    public float ArcStyleEndAngle { get; set; } = 45f;
    public float ThresholdPercentForColor3 { get; set; } = 0.80f;
    public float ThresholdPercentForColor4 { get; set; } = -0.95f;

    private readonly HashSet<int> CustomBrushCreationRequests = [];
    private readonly Dictionary<int, SolidBrush> CustomBrushes = [];

    private readonly GraphicsWindow Window;
    private BrushCollection? Brushes = null;

    private readonly FrameCapture Capturer;
    private readonly BoostMeterData MeterData;

    public CNKStyleBoostMeter()
    {
        // Create the DisplayInformation object with placeholder values that will be overwritten once the first frame is captured
        DisplayInfo = new()
        {
            SystemWidth = 1920,
            SystemHeight = 1080,
            RenderWidth = 1920,
            RenderHeight = 1080
        };

        // Configure the overlay classes
        var gfx = new Graphics
        {
            MeasureFPS = false,
            PerPrimitiveAntiAliasing = false,
            TextAntiAliasing = false
        };

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

        // Create the object used to track boost meter data
        MeterData = new(GetBoostMeterColor, () => Brushes, () => DisplayInfo);

        // Start the frame capturer that also records current boost values
        Capturer = new();
        Capturer.FrameReady += OnFrameCaptured;
        Capturer.StartCapture("TheKarters2");

        // Start the overlay
        Task.Run(StartOverlay);
    }

    private void OnFrameCaptured(object? sender, FrameCapturedEventArgs e)
    {
        // Use only the lower portion of the screen from 75% width and height to the edges to reduce the number of pixels to process
        var areaLeftBound = (int)(e.Width * 0.75);
        var areaTopBound = (int)(e.Height * 0.75);

        // Iterate through all of the pixels and find the boundaries of gray and red pixels from the boost bars
        // The values are stored in the array as R, G, B, A, R, G, B, A, ...
        // Therefore, pixels must be processed as groups of four bytes
        var leftMost = (int)e.Width;
        var rightMost = areaLeftBound;
        var topMost = (int)e.Height;
        var bottomMost = areaTopBound;
        for (var xRaw = areaLeftBound; xRaw < e.Width; xRaw++)
        {
            for (var yRaw = areaTopBound; yRaw < e.Height; yRaw++)
            {
                // For the given pixel identified by (xRaw, yRaw), calculate the array index if all RGBA values were stored as 32-bit uints
                // Then multiply the index by four to get the correct index for the R byte
                var indexRaw = (yRaw * e.Width) + xRaw;
                var rIndex = (int)(indexRaw * 4);
                var r = e.RgbaValues[rIndex];
                var g = e.RgbaValues[rIndex + 1];
                var b = e.RgbaValues[rIndex + 2];
                var a = e.RgbaValues[rIndex + 3];
                if (IsGray(r, g, b, a) || IsRed(r, g, b, a))
                {
                    leftMost = Math.Min(xRaw, leftMost);
                    rightMost = Math.Max(xRaw, rightMost);
                    topMost = Math.Min(yRaw, topMost);
                    bottomMost = Math.Max(yRaw, bottomMost);
                }
            }
        }

        // If the selected area width or height is not positive, clear the latest boost values and then return
        var width = rightMost - leftMost;
        var height = bottomMost - topMost;
        if ((width <= 0) || (height <= 0))
        {
            for (var i = 0; i < MeterData.BoostAmounts.Length; i++)
            {
                MeterData.BoostAmounts[i] = 0;
            }
            return;
        }

        // The bars can be sampled at the 10%, 50%, and 90% X-positions within the selected area to count the red and gray pixels
        // A black bar is missing where the fill bar cuts off, but that will be counted as a red pixel instead of a gray pixel
        for (var i = 0; i < MeterData.BoostAmounts.Length; i++)
        {
            var percent = AreaSamplePercentages[i];
            var xRaw = (int)(leftMost + (width * percent));

            var grayCount = 0;
            var redCount = 0;
            for (var yRaw = topMost; yRaw < topMost + height; yRaw++)
            {
                // Calculate the index of the red byte for an RGBA value
                var indexRaw = (yRaw * e.Width) + xRaw;
                var rIndex = (int)(indexRaw * 4);

                // Retrieve the RGBA values and check if the pixel is gray
                // Anything other than gray will be counted as red since those should be the only two colors present besides the black bar
                var r = e.RgbaValues[rIndex];
                var g = e.RgbaValues[rIndex + 1];
                var b = e.RgbaValues[rIndex + 2];
                var a = e.RgbaValues[rIndex + 3];
                if (IsGray(r, g, b, a))
                {
                    grayCount++;
                }
                else
                {
                    redCount++;
                }
            }

            // Calculate the portion that's filled and store it to the appropriate array position
            var portionFilled = 1.0f * redCount / (redCount + grayCount);
            MeterData.BoostAmounts[i] = portionFilled;
        }
    }

    private static bool IsGray(byte r, byte g, byte b, byte a) => IsPixelColor(r, g, b, a, GrayTuple);
    private static bool IsRed(byte r, byte g, byte b, byte a) => IsPixelColor(r, g, b, a, RedTuple);
    private static bool IsPixelColor(byte r, byte g, byte b, byte a, PixelColor color) => (r == color.r) && (g == color.g) && (b == color.b) && (a == color.a);

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

    public void StartOverlay()
    {
        Window.Create();
        Window.Join();
    }

    public void StopOverlay()
    {
        Window.Dispose();
        Window.Join();
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

            // Draw the current boost amount on the screen
            var (boostNum, boostValue) = MeterData.GetBoostNumberAndValue();
            if ((boostNum is not null) && (boostValue is not null))
            {
                for (var drawBoostNum = 0; drawBoostNum <= 2; drawBoostNum++)
                {
                    MeterData.DrawBoostBar(gfx, ConfigBoostBarStyle, drawBoostNum, DisplayInfo.RenderWidth / 2, DisplayInfo.RenderHeight / 2);
                }
            }
        }
    }

    private void Window_DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
    {
        Brushes?.Dispose();
        Capturer.Dispose();
    }
}
