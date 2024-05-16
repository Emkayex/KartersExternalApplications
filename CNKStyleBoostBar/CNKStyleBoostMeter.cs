using System.Diagnostics;
using GameOverlay.Drawing;
using GameOverlay.Windows;
using WindowCapture;
using Windows.Win32;
using Windows.Win32.Foundation;
using PixelColor = (byte r, byte g, byte b, byte a);

namespace CNKStyleBoostBar;
public class CNKStyleBoostMeter
{
    public const string DefaultWindowName = "TheKarters2";
    public const float MinValueForBoost = 0.5f;
    public const float MaxValueForBoost = 1.0f;

    private static readonly PixelColor GrayTuple = (0x5F, 0x5E, 0x5F, 0xFF);
    private static readonly PixelColor RedTuple = (0xF5, 0x00, 0x00, 0xFF);
    private static readonly double[] AreaSamplePercentages = [0.1, 0.5, 0.9];

    /// <summary>The width of the colored area of the boost bar at 1080P (minus 1 for safety).</summary>
    private const int BaseBoostBarWidth = 27;

    /// <summary>The height of the colored area of the boost bar at 1080P (minus 1 for safety).</summary>
    private const int BaseBoostBarHeight = 111;

    public readonly DisplayInformation DisplayInfo;

    public BoostBarStyle ConfigBoostBarStyle { get; set; } = BoostBarStyle.ArcsSameAngles;
    public string BoostMeterColor1 { get; set; } = "00FF00";
    public string BoostMeterColor2 { get; set; } = "FFD800";
    public string BoostMeterColor3 { get; set; } = "FF6A00";
    public string BoostMeterColor4 { get; set; } = "FF0000";

    public float ArcStyleStartAngle { get; set; } = -30f;
    public float ArcStyleEndAngle { get; set; } = 45f;
    public float ThresholdPercentForColor3 { get; set; } = 0.80f;
    public float ThresholdPercentForColor4 { get; set; } = 0.95f;

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

    public CNKStyleBoostMeter()
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
        MeterData = new(GetBoostMeterColor, () => Brushes, () => DisplayInfo);
    }

    public void StartCaptureAndOverlay(string windowName = DefaultWindowName)
    {
        // Start the frame capturer that also records current boost values
        Capturer = new();
        Capturer.FrameReady += OnFrameCaptured;
        Capturer.StartCapture(windowName);
        WindowName = windowName;

        // Wait until one frame is captured as indicate by the DisplayInfo width/height not being 0 (only one needs to be checked)
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

    private void OnFrameCaptured(object? sender, FrameCapturedEventArgs e)
    {
        // Make sure the overlay window is always over the game window
        FitOverlayToWindow();

        // Update the most recent height and width of the window
        DisplayInfo.RenderWidth = DisplayInfo.SystemWidth = (int)e.Width;
        DisplayInfo.RenderHeight = DisplayInfo.SystemHeight = (int)e.Height;

        // Get the bounds of the screen containing the boost meters
        var (leftMost, topMost, rightMost, bottomMost) = FindBoostMeterScreenBounds(e.RgbaValues, (int)e.Width, (int)e.Height);
        DebugLeft = leftMost;
        DebugRight = rightMost;
        DebugTop = topMost;
        DebugBottom = bottomMost;

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
            var portionFilled = 1f * redCount / (redCount + grayCount);
            MeterData.BoostAmounts[i] = portionFilled;
        }
    }

    private static bool IsRedOrGray(byte r, byte g, byte b, byte a) => IsGray(r, g, b, a) || IsRed(r, g, b, a);
    private static bool IsGray(byte r, byte g, byte b, byte a) => IsPixelColor(r, g, b, a, GrayTuple);
    private static bool IsRed(byte r, byte g, byte b, byte a) => IsPixelColor(r, g, b, a, RedTuple);
    private static bool IsPixelColor(byte r, byte g, byte b, byte a, PixelColor color) => (r == color.r) && (g == color.g) && (b == color.b) && (a == color.a);

    private (int leftMost, int topMost, int rightMost, int bottomMost) FindBoostMeterScreenBounds(byte[] rgbaValues, int width, int height)
    {
        // Use only the lower portion of the screen from 50% width and height to the edges to reduce the number of pixels to process
        var areaLeftBound = (int)(width * 0.5);
        var areaTopBound = (int)(height * 0.5);

        // Calculate the search step size based on the display size
        var uiScale = DisplayInfo.GetUIScaleY(height);
        var sampleStepSizeX = (int)Math.Floor(BaseBoostBarWidth * uiScale * 0.5f);
        var sampleStepSizeY = (int)Math.Floor(BaseBoostBarHeight * uiScale * 0.5f);

        // Sample every M pixels horizontally and N pixels vertically where M is less than the width of one boost bar and N is less than the height
        // This will ensure that the sampling will find at least one red or gray pixel from each boost bar
        // From there, a search can be conducted outward to find the bounds of the boost bars
        // This should significantly reduce the amount of processing necessary to find the bounds of the boost bars since only a small fraction of pixels are tested
        var xCoords = Enumerable.Range(areaLeftBound, width - areaLeftBound).Where(x => (x % sampleStepSizeX) == 0);
        var yCoords = Enumerable.Range(areaTopBound, height - areaTopBound).Where(y => (y % sampleStepSizeY) == 0);
        var xAndYLists = new (IEnumerable<int>, IEnumerable<int>)[] { (xCoords, yCoords) };
        var (leftMost, topMost, rightMost, bottomMost) = FindBoostMeterBoundsWithinArea(rgbaValues, width, xAndYLists);

        // Check to make sure the width and height are valid otherwise return immediately
        var areaWidth = rightMost - leftMost;
        var areaHeight = bottomMost - topMost;
        if ((areaWidth <= 0) || (areaHeight <= 0))
        {
            return (leftMost, topMost, rightMost, bottomMost);
        }

        // Expand the search area out by one increment value in all directions and find the bounds checking all pixels in the newly added areas
        var newLeftCoords = Enumerable.Range(leftMost - sampleStepSizeX, sampleStepSizeX + 1);
        var newRightCoords = Enumerable.Range(rightMost, sampleStepSizeX + 1);
        var newTopCoords = Enumerable.Range(topMost - sampleStepSizeY, sampleStepSizeY + 1);
        var newBottomCoords = Enumerable.Range(bottomMost, sampleStepSizeY + 1);

        var topLeftSquare = (newLeftCoords, newTopCoords);
        var topRightSquare = (newRightCoords, newTopCoords);
        var bottomLeftSquare = (newLeftCoords, newBottomCoords);
        var bottomRightSquare = (newRightCoords, newBottomCoords);
        var squaresToCheck = new (IEnumerable<int>, IEnumerable<int>)[] { topLeftSquare, topRightSquare, bottomLeftSquare, bottomRightSquare };

        var (newLeftMost, newTopMost, newRightMost, newBottomMost) = FindBoostMeterBoundsWithinArea(rgbaValues, width, squaresToCheck);

        leftMost = Math.Min(leftMost, newLeftMost);
        rightMost = Math.Max(rightMost, newRightMost);
        topMost = Math.Min(topMost, newTopMost);
        bottomMost = Math.Max(bottomMost, newBottomMost);
        return (leftMost, topMost, rightMost, bottomMost);
    }

    private static (int leftMost, int topMost, int rightMost, int bottomMost) FindBoostMeterBoundsWithinArea(byte[] rgbaValues, int width, IEnumerable<(IEnumerable<int>, IEnumerable<int>)> xAndYLists)
    {
        // Set the starting values for the bounds to coordinates beyond what is possible and in the wrong directions (negative width and height)
        // This will ensure that any pixels that are found will override the starting values
        var leftMost = int.MaxValue / 4;;
        var rightMost = int.MinValue / 4;
        var topMost = int.MaxValue / 4;
        var bottomMost = int.MinValue / 4;

        foreach (var (xList, yList) in xAndYLists)
        {
            foreach (var xRaw in xList)
            {
                foreach (var yRaw in yList)
                {
                    // The values for each pixel are stored in the array as R, G, B, A, R, G, B, A, ...
                    // Therefore, pixels must be processed as groups of four bytes
                    // For the given pixel identified by (xRaw, yRaw), calculate the array index if all RGBA values were stored as 32-bit uints
                    // Then multiply the index by four to get the correct index for the R byte
                    var indexRaw = (yRaw * width) + xRaw;
                    var rIndex = indexRaw * 4;
                    if (rIndex >= rgbaValues.Length)
                    {
                        continue;
                    }

                    var r = rgbaValues[rIndex];
                    var g = rgbaValues[rIndex + 1];
                    var b = rgbaValues[rIndex + 2];
                    var a = rgbaValues[rIndex + 3];

                    if (IsRedOrGray(r, g, b, a))
                    {
                        leftMost = Math.Min(xRaw, leftMost);
                        rightMost = Math.Max(xRaw, rightMost);
                        topMost = Math.Min(yRaw, topMost);
                        bottomMost = Math.Max(yRaw, bottomMost);
                    }
                }
            }
        }

        return (leftMost, topMost, rightMost, bottomMost);
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

            // TODO: Remove this debug drawing call once all bugs are worked out with windowed mode
            gfx.DrawRectangle(Brushes.Yellow, DebugLeft, DebugTop, DebugRight, DebugBottom, 3f);

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
        Capturer?.Dispose();
    }
}
