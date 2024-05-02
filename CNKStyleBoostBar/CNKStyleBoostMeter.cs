using GameOverlay.Drawing;
using GameOverlay.Windows;

namespace CNKStyleBoostBar;
public class CNKStyleBoostMeter
{
    public const float MinValueForBoost = 0.5f;
    public const float MaxValueForBoost = 1.0f;

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

        // Start the overlay
        Task.Run(StartOverlay);
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
                    MeterData.DrawBoostBar(gfx, ConfigBoostBarStyle, drawBoostNum, MeterData.DrawX, MeterData.DrawY);
                }
            }
        }
    }

    private void Window_DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
    {
        Brushes?.Dispose();
    }
}
